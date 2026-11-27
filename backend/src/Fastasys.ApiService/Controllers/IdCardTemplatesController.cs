using System.Linq.Expressions;
using System.Security.Claims;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fastasys.ApiService.Controllers;

// A template on the Infosys "ID skabeloner" page (id-templates). Width and Height are the background's size in pixels.
public record IdCardTemplateDto(int Id, string Name, int Width, int Height, bool HasBackground, IReadOnlyList<IdCardTemplateItemDto> Items);

// An item on a template; DataSource is set for text items only
public record IdCardTemplateItemDto(IdCardItemType Type, int X, int Y, int Width, int Height, int Rotation, IdCardDataSource? DataSource);

public record CreateIdCardTemplateDto(string? Name);

// Infosys's editor saves the whole template after every change: the name and all items
public record UpdateIdCardTemplateDto(string? Name, IReadOnlyList<IdCardTemplateItemDto>? Items);

// A new background as a data URL ("data:image/png;base64,..."), like Infosys's editor sends it
public record IdCardBackgroundDto(string? Image);

// "Kategori-skabeloner": a participant category and its template
public record CategoryIdCardTemplateDto(int CategoryId, string Name, int? TemplateId);

public record UpdateCategoryIdCardTemplateDto(int? TemplateId);

// Infosys IdTemplateController (the "ID skabeloner" page, which its menu shows to Infonaut and Admin). Here it takes the
// right to edit participants, like the rest of the ID cards.
[ApiController]
[Route("api/id-card-templates")]
[Authorize(Policy = "ParticipantManagement")]
public class IdCardTemplatesController : ControllerBase
{
    // Infosys idtemplates.name is VARCHAR(128)
    private const int MaxNameLength = 128;

    private readonly InfosysDbContext _db;
    private readonly ILogger<IdCardTemplatesController> _logger;

    public IdCardTemplatesController(InfosysDbContext db, ILogger<IdCardTemplatesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // Infosys IdTemplateModel::fetchTemplateData: every template with its items, in id order
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<IdCardTemplateDto>>> GetAll() =>
        Ok(await Templates(_ => true).ToListAsync());

    [HttpGet("{id:int}")]
    public async Task<ActionResult<IdCardTemplateDto>> Get(int id)
    {
        var template = await Templates(t => t.Id == id).FirstOrDefaultAsync();
        return template == null ? NotFound(new { message = "Template not found" }) : Ok(template);
    }

    [HttpGet("{id:int}/background")]
    public async Task<IActionResult> GetBackground(int id)
    {
        var background = await _db.IdCardTemplates
            .AsNoTracking()
            .Where(t => t.Id == id && t.Background != null)
            .Select(t => new { t.Background, t.BackgroundContentType })
            .FirstOrDefaultAsync();
        return background == null ? NotFound(new { message = "No background" }) : File(background.Background!, background.BackgroundContentType!);
    }

    // Infosys IdTemplateController::createTemplate: a new, empty template with the given name (the page trims it)
    [HttpPost]
    public async Task<ActionResult<IdCardTemplateDto>> Create([FromBody] CreateIdCardTemplateDto dto)
    {
        var name = dto.Name?.Trim();
        if (string.IsNullOrEmpty(name)) return BadRequest(new { message = "The template needs a name" });
        if (name.Length > MaxNameLength) return BadRequest(new { message = "The name is too long" });

        var template = new IdCardTemplate { Name = name };
        _db.IdCardTemplates.Add(template);
        await _db.SaveChangesAsync();

        _logger.LogInformation("ID card template {TemplateId} created by user {UserId}", template.Id, CurrentUserId());
        return CreatedAtAction(nameof(Get), new { id = template.Id }, ToDto(template));
    }

    // Infosys IdTemplateModel::updateTemplate: the name and all items (the items are replaced)
    [HttpPut("{id:int}")]
    public async Task<ActionResult<IdCardTemplateDto>> Update(int id, [FromBody] UpdateIdCardTemplateDto dto)
    {
        var (isValid, error) = ValidateUpdate(dto);
        if (!isValid) return BadRequest(new { message = error });

        var template = await _db.IdCardTemplates.Include(t => t.Items).FirstOrDefaultAsync(t => t.Id == id);
        if (template == null) return NotFound(new { message = "Template not found" });

        template.Name = dto.Name!;
        _db.IdCardTemplateItems.RemoveRange(template.Items);
        template.Items = dto.Items!.Select(i => new IdCardTemplateItem
        {
            Type = i.Type,
            X = i.X,
            Y = i.Y,
            Width = i.Width,
            Height = i.Height,
            Rotation = i.Rotation,
            // Infosys stores '' for photos and barcodes
            DataSource = i.Type == IdCardItemType.Text ? i.DataSource : null
        }).ToList();
        await _db.SaveChangesAsync();

        _logger.LogInformation("ID card template {TemplateId} updated by user {UserId}", id, CurrentUserId());
        return Ok(ToDto(template));
    }

    // Infosys IdTemplateModel::handleTemplateBackground: a PNG, JPEG or GIF image; the card gets its size
    [HttpPut("{id:int}/background")]
    [RequestSizeLimit(16 * 1024 * 1024)]
    public async Task<ActionResult<IdCardTemplateDto>> UpdateBackground(int id, [FromBody] IdCardBackgroundDto dto)
    {
        var bytes = ImageFiles.FromDataUrl(dto.Image);
        var image = bytes == null ? null : ImageFiles.Inspect(bytes);
        if (image == null) return BadRequest(new { message = "The background must be a PNG, JPEG or GIF image of at most 10 MB" });

        var template = await _db.IdCardTemplates.Include(t => t.Items).FirstOrDefaultAsync(t => t.Id == id);
        if (template == null) return NotFound(new { message = "Template not found" });

        template.Background = bytes;
        template.BackgroundContentType = image.ContentType;
        template.Width = image.Width;
        template.Height = image.Height;
        await _db.SaveChangesAsync();

        _logger.LogInformation("ID card template {TemplateId} got a new background from user {UserId}", id, CurrentUserId());
        return Ok(ToDto(template));
    }

    // Infosys idtemplate.js copyTemplateHandler: a new template "Copy of {name}" with the same background and items.
    // Infosys's copy pointed at the original's background file, so a new background on the original changed the copy
    // too; here the copy gets its own.
    [HttpPost("{id:int}/copy")]
    public async Task<ActionResult<IdCardTemplateDto>> Copy(int id)
    {
        var original = await _db.IdCardTemplates.AsNoTracking().Include(t => t.Items).FirstOrDefaultAsync(t => t.Id == id);
        if (original == null) return NotFound(new { message = "Template not found" });

        var name = "Copy of " + original.Name;
        var copy = new IdCardTemplate
        {
            Name = name.Length > MaxNameLength ? name[..MaxNameLength] : name,
            Background = original.Background,
            BackgroundContentType = original.BackgroundContentType,
            Width = original.Width,
            Height = original.Height,
            Items = original.Items.OrderBy(i => i.Id).Select(i => new IdCardTemplateItem
            {
                Type = i.Type,
                X = i.X,
                Y = i.Y,
                Width = i.Width,
                Height = i.Height,
                Rotation = i.Rotation,
                DataSource = i.DataSource
            }).ToList()
        };
        _db.IdCardTemplates.Add(copy);
        await _db.SaveChangesAsync();

        _logger.LogInformation("ID card template {TemplateId} copied to {CopyId} by user {UserId}", id, copy.Id, CurrentUserId());
        return CreatedAtAction(nameof(Get), new { id = copy.Id }, ToDto(copy));
    }

    // Infosys IdTemplateModel::deleteTemplate. The categories and participants that used it fall back: a category has
    // no template then, and a participant gets their category's (Infosys's foreign keys cascade).
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var template = await _db.IdCardTemplates.FindAsync(id);
        if (template == null) return NotFound(new { message = "Template not found" });

        foreach (var category in await _db.ParticipantCategories.Where(c => c.IdCardTemplateId == id).ToListAsync())
        {
            category.IdCardTemplateId = null;
        }

        foreach (var participant in await _db.Participants.Where(p => p.IdCardTemplateId == id).ToListAsync())
        {
            participant.IdCardTemplateId = null;
        }

        _db.IdCardTemplates.Remove(template);
        await _db.SaveChangesAsync();

        _logger.LogInformation("ID card template {TemplateId} deleted by user {UserId}", id, CurrentUserId());
        return NoContent();
    }

    // "Kategori-skabeloner" (Infosys IdTemplateModel::fetchCategoryData): every category with its template, in id order
    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<CategoryIdCardTemplateDto>>> GetCategories() =>
        Ok(await _db.ParticipantCategories
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .Select(c => new CategoryIdCardTemplateDto(c.Id, c.Name, c.IdCardTemplateId))
            .ToListAsync());

    // Infosys IdTemplateModel::updateCategoryTemplate: the empty choice (null or 0) removes the category's template
    [HttpPut("categories/{categoryId:int}")]
    public async Task<IActionResult> UpdateCategory(int categoryId, [FromBody] UpdateCategoryIdCardTemplateDto dto)
    {
        var category = await _db.ParticipantCategories.FindAsync(categoryId);
        if (category == null) return NotFound(new { message = "Category not found" });

        var templateId = dto.TemplateId is null or 0 ? null : dto.TemplateId;
        if (templateId != null && !await _db.IdCardTemplates.AnyAsync(t => t.Id == templateId))
        {
            return BadRequest(new { message = "Unknown template" });
        }

        category.IdCardTemplateId = templateId;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Category {CategoryId}'s ID card template set to {TemplateId} by user {UserId}", categoryId, templateId, CurrentUserId());
        return NoContent();
    }

    // The templates that match, in id order, with their items and without the background's bytes. The filter comes
    // before the projection: a filter on the projected record can't be translated to SQL.
    private IQueryable<IdCardTemplateDto> Templates(Expression<Func<IdCardTemplate, bool>> filter) =>
        _db.IdCardTemplates
            .AsNoTracking()
            .Where(filter)
            .OrderBy(t => t.Id)
            .Select(t => new IdCardTemplateDto(
                t.Id,
                t.Name,
                t.Width,
                t.Height,
                t.Background != null,
                t.Items
                    .OrderBy(i => i.Id)
                    .Select(i => new IdCardTemplateItemDto(i.Type, i.X, i.Y, i.Width, i.Height, i.Rotation, i.DataSource))
                    .ToList()));

    private static IdCardTemplateDto ToDto(IdCardTemplate t) =>
        new(
            t.Id,
            t.Name,
            t.Width,
            t.Height,
            t.Background != null,
            t.Items.Select(i => new IdCardTemplateItemDto(i.Type, i.X, i.Y, i.Width, i.Height, i.Rotation, i.DataSource)).ToList());

    // Infosys stores positions and sizes as unsigned numbers; the editor keeps the rotation within 0-359 and gives a
    // text item a data source
    private static (bool IsValid, string Error) ValidateUpdate(UpdateIdCardTemplateDto dto)
    {
        if (dto.Name == null) return (false, "The template needs a name");
        if (dto.Name.Length > MaxNameLength) return (false, "The name is too long");
        if (dto.Items == null) return (false, "The template's items are missing");

        foreach (var item in dto.Items)
        {
            if (!Enum.IsDefined(item.Type)) return (false, "Unknown item type");
            if (item.X < 0 || item.Y < 0 || item.Width < 0 || item.Height < 0) return (false, "Positions and sizes can't be negative");
            if (item.Rotation is < 0 or > 359) return (false, "The rotation must be 0-359 degrees");
            if (item.Type == IdCardItemType.Text && (item.DataSource == null || !Enum.IsDefined(item.DataSource.Value)))
            {
                return (false, "A text item needs a data source");
            }
        }

        return (true, string.Empty);
    }

    private string? CurrentUserId() => User?.FindFirstValue(ClaimTypes.NameIdentifier);
}
