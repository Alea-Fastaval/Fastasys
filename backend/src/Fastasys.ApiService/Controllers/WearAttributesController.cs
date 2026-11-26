using System.Security.Claims;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fastasys.ApiService.Controllers;

// A row of Infosys's "Rediger wear egenskaber" (wear/attributes): Dansk and Engelsk
public abstract record WearAttributeFieldsDto
{
    public string? NameDanish { get; init; }
    public string? NameEnglish { get; init; }
}

// "Tilføj egenskab" under one of the kinds
public record CreateWearAttributeDto : WearAttributeFieldsDto
{
    public WearAttributeKind? Kind { get; init; }
}

// "Gem" on a changed row
public record UpdateWearAttributeDto : WearAttributeFieldsDto;

// Infosys WearController::attributes and WearModel::setAttribute. Infosys has no way to delete an attribute or change
// its kind or place, and its "Tilføj Kategori" (a new kind) isn't ported: the kinds are fixed (agreed 2026-11-26).
// The page took WearController/* in Infosys; here wear_edit changes and wear_view may look.
[ApiController]
[Route("api/wear-attributes")]
[Authorize]
public class WearAttributesController(InfosysDbContext db, ILogger<WearAttributesController> logger) : ControllerBase
{
    // WearModel::getAttributes: the attributes by kind (model, design, color, size) and then position
    [HttpGet]
    [Authorize(Policy = "WearView")]
    public async Task<ActionResult<IEnumerable<WearAttributeDto>>> GetAll()
    {
        var attributes = await db.WearAttributes.AsNoTracking().ToListAsync();
        return Ok(
            attributes
                .OrderBy(a => a.Kind)
                .ThenBy(a => a.Position)
                .ThenBy(a => a.Id)
                .Select(WearTypesController.ToDto)
                .ToList()
        );
    }

    // A new attribute goes last in its kind (MAX(position) + 1)
    [HttpPost]
    [Authorize(Policy = "WearManagement")]
    public async Task<ActionResult<WearAttributeDto>> Create([FromBody] CreateWearAttributeDto dto)
    {
        if (dto.Kind is not { } kind || !Enum.IsDefined(kind))
            return BadRequest(new { message = "Unknown attribute kind" });

        var (isValid, error) = Validate(dto);
        if (!isValid)
            return BadRequest(new { message = error });

        var attribute = new WearAttribute
        {
            Kind = kind,
            NameDanish = dto.NameDanish ?? string.Empty,
            NameEnglish = dto.NameEnglish ?? string.Empty,
            Position = (await db.WearAttributes.Where(a => a.Kind == kind).MaxAsync(a => (int?)a.Position) ?? 0) + 1,
        };
        db.WearAttributes.Add(attribute);
        await db.SaveChangesAsync();

        logger.LogInformation("Wear attribute {AttributeId} created by user {UserId}", attribute.Id, CurrentUserId());

        return Ok(WearTypesController.ToDto(attribute));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "WearManagement")]
    public async Task<ActionResult<WearAttributeDto>> Update(int id, [FromBody] UpdateWearAttributeDto dto)
    {
        var attribute = await db.WearAttributes.FindAsync(id);
        if (attribute == null)
            return NotFound(new { message = "Wear attribute not found" });

        var (isValid, error) = Validate(dto);
        if (!isValid)
            return BadRequest(new { message = error });

        attribute.NameDanish = dto.NameDanish ?? string.Empty;
        attribute.NameEnglish = dto.NameEnglish ?? string.Empty;
        await db.SaveChangesAsync();

        logger.LogInformation("Wear attribute {AttributeId} updated by user {UserId}", id, CurrentUserId());

        return Ok(WearTypesController.ToDto(attribute));
    }

    // Infosys checks nothing; the names are as long as the schema allows
    public static (bool IsValid, string Error) Validate(WearAttributeFieldsDto dto)
    {
        if (
            (dto.NameDanish?.Length ?? 0) > WearAttribute.MaxNameLength
            || (dto.NameEnglish?.Length ?? 0) > WearAttribute.MaxNameLength
        )
            return (false, $"A name can be at most {WearAttribute.MaxNameLength} characters");

        return (true, string.Empty);
    }

    private string? CurrentUserId() => User?.FindFirstValue(ClaimTypes.NameIdentifier);
}
