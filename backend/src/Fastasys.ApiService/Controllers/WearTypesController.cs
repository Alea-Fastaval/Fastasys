using System.Security.Claims;
using System.Text.Json.Serialization;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fastasys.ApiService.Controllers;

// A row of Infosys's "Wear-typer" (wear/showwear): Id, Navn, Beskrivelse and the order (Rækkefølge)
public record WearTypeSummaryDto(int Id, string NameDanish, string DescriptionDanish, int Position);

// A wear attribute (Infosys wear_attributes): its kind, Danish and English name, and its place within the kind
public record WearAttributeDto(int Id, WearAttributeKind Kind, string NameDanish, string NameEnglish, int Position);

// A price of a wear type: the participant category ("Brugerkategori") and the price ("Pris"). OrderCount is how many
// orders were made at the price (annulled participants' too); an ordered price can't be removed.
public record WearPriceDto(int Id, int ParticipantCategoryId, string CategoryName, int Price, int OrderCount);

// An image shown for the wear type, for one of its attributes or for all of them ("Alle", AttributeId null)
public record WearImageLinkDto(int WearImageId, int? AttributeId);

// Infosys wear/viswear/:id: and the edit form: the fields, the variants (each one's attributes in the order model,
// design, color, size, each kind by position), the prices in the order they were added, the image links, and how many
// orders the type has (annulled participants' too), which stop it from being deleted.
public record WearTypeDto(
    int Id,
    string NameDanish,
    string NameEnglish,
    string DescriptionDanish,
    string DescriptionEnglish,
    int MaxOrder,
    bool IsRequired,
    IReadOnlyList<IReadOnlyList<WearAttributeDto>> Variants,
    IReadOnlyList<WearPriceDto> Prices,
    IReadOnlyList<WearImageLinkDto> ImageLinks,
    int OrderCount
);

// The fields of the Infosys create/edit form (wear/createwear, wear/editwear/:id:): Navn, Beskrivelse, Engelsk titel,
// Engelsk beskrivelse, Maks bestilling, Påkrævet, Varianter (each variant a list of attribute ids), the image links and
// the prices ("Wear priser"), one per participant category.
public abstract record WearTypeFieldsDto
{
    public string? NameDanish { get; init; }
    public string? NameEnglish { get; init; }
    public string? DescriptionDanish { get; init; }
    public string? DescriptionEnglish { get; init; }
    public int? MaxOrder { get; init; }
    public bool IsRequired { get; init; }
    public IReadOnlyList<IReadOnlyList<int>>? Variants { get; init; }
    public IReadOnlyList<WearPriceFieldsDto>? Prices { get; init; }
    public IReadOnlyList<WearImageLinkDto>? ImageLinks { get; init; }
}

public record CreateWearTypeDto : WearTypeFieldsDto;

public record UpdateWearTypeDto : WearTypeFieldsDto;

public record WearPriceFieldsDto(int? ParticipantCategoryId, int? Price);

[JsonConverter(typeof(JsonStringEnumConverter<MoveDirection>))]
public enum MoveDirection
{
    Up,
    Down,
}

// The up and down arrows of "Wear-typer"
public record MoveWearTypeDto(MoveDirection? Direction);

// Infosys WearController: Wear-typer, one type, create, edit, delete and the order of the types. Infosys lets the
// Read-only role look and only admin create, change and delete; here wear_view may look and wear_edit may change.
// Moving a type up or down took WearController/* (not admin) in Infosys; here it takes wear_edit.
[ApiController]
[Route("api/wear-types")]
[Authorize]
public class WearTypesController(InfosysDbContext db, ILogger<WearTypesController> logger) : ControllerBase
{
    private const string NotFoundMessage = "Wear type not found";

    // Infosys wear/showwear ("Wear-typer"): the wear types by position (Wear's default order)
    [HttpGet]
    [Authorize(Policy = "WearView")]
    public async Task<ActionResult<IEnumerable<WearTypeSummaryDto>>> GetAll() =>
        Ok(
            await db
                .WearTypes.AsNoTracking()
                .OrderBy(w => w.Position)
                .ThenBy(w => w.Id)
                .Select(w => new WearTypeSummaryDto(w.Id, w.NameDanish, w.DescriptionDanish, w.Position))
                .ToListAsync()
        );

    // The participant categories the form's price select offers (WearModel::getAllParticipantCategories; the page
    // sorts them by name)
    [HttpGet("categories")]
    [Authorize(Policy = "WearManagement")]
    public async Task<ActionResult<IEnumerable<ParticipantCategoryDto>>> GetCategories() =>
        Ok(
            await db
                .ParticipantCategories.AsNoTracking()
                .OrderBy(c => c.Id)
                .Select(c => new ParticipantCategoryDto(c.Id, c.Name))
                .ToListAsync()
        );

    // Infosys wear/viswear/:id:
    [HttpGet("{id:int}")]
    [Authorize(Policy = "WearView")]
    public async Task<ActionResult<WearTypeDto>> Get(int id)
    {
        var dto = await LoadDtoAsync(db, id);
        return dto == null ? NotFound(new { message = NotFoundMessage }) : Ok(dto);
    }

    // Infosys wear/createwear ("Opret wear-type"). A new type goes last (MAX(position) + 1).
    [HttpPost]
    [Authorize(Policy = "WearManagement")]
    public async Task<ActionResult<WearTypeDto>> Create([FromBody] CreateWearTypeDto dto)
    {
        var (isValid, error) = await ValidateAsync(dto);
        if (!isValid)
            return BadRequest(new { message = error });

        var wear = new WearType { Position = (await db.WearTypes.MaxAsync(w => (int?)w.Position) ?? 0) + 1 };
        Apply(wear, dto);
        foreach (var price in dto.Prices ?? [])
        {
            wear.Prices.Add(
                new WearPrice { ParticipantCategoryId = price.ParticipantCategoryId!.Value, Price = price.Price!.Value }
            );
        }

        ApplyVariants(wear, dto);
        ApplyImageLinks(wear, dto);
        db.WearTypes.Add(wear);
        await db.SaveChangesAsync();

        logger.LogInformation("Wear type {WearTypeId} created by user {UserId}", wear.Id, CurrentUserId());

        return CreatedAtAction(nameof(Get), new { id = wear.Id }, await LoadDtoAsync(db, wear.Id));
    }

    // Infosys wear/editwear/:id: ("Opdater"). The prices are matched by category: a listed category's price is changed
    // or added, and an unlisted one is removed. Removing a price that participants have ordered is refused; in Infosys
    // the foreign key gave an error page (fixed 2026-11-26). The variants and image links are replaced.
    [HttpPut("{id:int}")]
    [Authorize(Policy = "WearManagement")]
    public async Task<ActionResult<WearTypeDto>> Update(int id, [FromBody] UpdateWearTypeDto dto)
    {
        var wear = await db
            .WearTypes.Include(w => w.Prices)
                .ThenInclude(p => p.ParticipantCategory)
            .Include(w => w.VariantAttributes)
            .Include(w => w.ImageLinks)
            .FirstOrDefaultAsync(w => w.Id == id);
        if (wear == null)
            return NotFound(new { message = NotFoundMessage });

        var (isValid, error) = await ValidateAsync(dto);
        if (!isValid)
            return BadRequest(new { message = error });

        var posted = (dto.Prices ?? []).ToDictionary(p => p.ParticipantCategoryId!.Value, p => p.Price!.Value);
        var removed = wear.Prices.Where(p => !posted.ContainsKey(p.ParticipantCategoryId)).ToList();
        var removedIds = removed.Select(p => p.Id).ToList();
        var ordered = await db
            .ParticipantWearOrders.Where(o => removedIds.Contains(o.WearPriceId))
            .Select(o => o.WearPriceId)
            .FirstOrDefaultAsync();
        if (ordered != 0)
            return Conflict(PriceOrdered(removed.First(p => p.Id == ordered).ParticipantCategory.Name));

        Apply(wear, dto);
        foreach (var (categoryId, price) in posted)
        {
            var existing = wear.Prices.FirstOrDefault(p => p.ParticipantCategoryId == categoryId);
            if (existing != null)
                existing.Price = price;
            else
                wear.Prices.Add(new WearPrice { ParticipantCategoryId = categoryId, Price = price });
        }

        db.WearPrices.RemoveRange(removed);
        ApplyVariants(wear, dto);
        ApplyImageLinks(wear, dto);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // A participant ordered at a removed price at the same time (the foreign key is RESTRICT), or an attribute or
            // image went away
            return Conflict(new { message = "The wear type was changed at the same time; try again" });
        }

        logger.LogInformation("Wear type {WearTypeId} updated by user {UserId}", id, CurrentUserId());

        return Ok(await LoadDtoAsync(db, id));
    }

    // Infosys wear/deletewear/:id:: refused while the type has orders ("Denne wear type har allerede N bestillinger");
    // its prices, variants and image links go with it
    [HttpDelete("{id:int}")]
    [Authorize(Policy = "WearManagement")]
    public async Task<IActionResult> Delete(int id)
    {
        var wear = await db
            .WearTypes.Include(w => w.Prices)
            .Include(w => w.VariantAttributes)
            .Include(w => w.ImageLinks)
            .FirstOrDefaultAsync(w => w.Id == id);
        if (wear == null)
            return NotFound(new { message = NotFoundMessage });

        var orderCount = await db.ParticipantWearOrders.CountAsync(o => o.WearPrice.WearTypeId == id);
        if (orderCount > 0)
            return Conflict(InUse(orderCount));

        db.WearTypes.Remove(wear);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // A participant ordered it at the same time
            return Conflict(InUse(await db.ParticipantWearOrders.CountAsync(o => o.WearPrice.WearTypeId == id)));
        }

        logger.LogInformation("Wear type {WearTypeId} deleted by user {UserId}", id, CurrentUserId());

        return Ok(new { message = "Wear type deleted successfully" });
    }

    // Infosys wear/showwear/ajax (switch_row): swaps the type's position with the type above or below it. At the top
    // or bottom nothing happens, like Infosys.
    [HttpPut("{id:int}/position")]
    [Authorize(Policy = "WearManagement")]
    public async Task<IActionResult> Move(int id, [FromBody] MoveWearTypeDto dto)
    {
        if (dto.Direction is not { } direction || !Enum.IsDefined(direction))
            return BadRequest(new { message = "Direction must be Up or Down" });

        var types = await db.WearTypes.OrderBy(w => w.Position).ThenBy(w => w.Id).ToListAsync();
        var index = types.FindIndex(w => w.Id == id);
        if (index < 0)
            return NotFound(new { message = NotFoundMessage });

        var otherIndex = direction == MoveDirection.Up ? index - 1 : index + 1;
        if (otherIndex < 0 || otherIndex >= types.Count)
            return NoContent();

        // Infosys swapped the two positions. The types are numbered 1, 2, 3, ... first, so two types with the same
        // position still change places.
        (types[index], types[otherIndex]) = (types[otherIndex], types[index]);
        for (var i = 0; i < types.Count; i++)
        {
            types[i].Position = i + 1;
        }

        await db.SaveChangesAsync();

        logger.LogInformation(
            "Wear type {WearTypeId} moved {Direction} by user {UserId}",
            id,
            direction,
            CurrentUserId()
        );

        return NoContent();
    }

    // Returns whether the form fields are valid and, if not, why. Invalid input is expected, so it never throws.
    public static (bool IsValid, string Error) Validate(
        WearTypeFieldsDto dto,
        IReadOnlySet<int> categoryIds,
        IReadOnlySet<int> attributeIds,
        IReadOnlySet<int> imageIds
    )
    {
        // Infosys refuses a navn that PHP's empty() calls empty, which includes "0"
        if (string.IsNullOrEmpty(dto.NameDanish) || dto.NameDanish == "0")
            return (false, "The name is required");
        if (dto.NameDanish.Length > WearType.MaxNameLength)
            return (false, $"The name can be at most {WearType.MaxNameLength} characters");
        if ((dto.NameEnglish?.Length ?? 0) > WearType.MaxNameLength)
            return (false, $"The English title can be at most {WearType.MaxNameLength} characters");

        // Infosys saved an empty Maks bestilling as 0 and didn't check the rest (fixed 2026-11-26)
        if (dto.MaxOrder is < 0)
            return (false, "Maks bestilling must be a whole number of 0 or more");

        var prices = dto.Prices ?? [];
        foreach (var price in prices)
        {
            if (price.ParticipantCategoryId is not { } categoryId || !categoryIds.Contains(categoryId))
                return (false, "Unknown participant category");
            // Infosys took any text as a price (fixed 2026-11-26)
            if (price.Price is null or < 0)
                return (false, "Price must be a whole number of 0 or more");
        }

        // Infosys wearpriser: unique (wear_id, brugerkategori_id)
        if (prices.Select(p => p.ParticipantCategoryId).Distinct().Count() != prices.Count)
            return (false, "A participant category can have only one price");

        if ((dto.Variants ?? []).Any(v => v == null || v.Any(a => !attributeIds.Contains(a))))
            return (false, "Unknown wear attribute");

        foreach (var link in dto.ImageLinks ?? [])
        {
            if (!imageIds.Contains(link.WearImageId))
                return (false, "Unknown wear image");
            if (link.AttributeId is { } attributeId && !attributeIds.Contains(attributeId))
                return (false, "Unknown wear attribute");
        }

        return (true, string.Empty);
    }

    // The type as the show page and the form need it
    public static async Task<WearTypeDto?> LoadDtoAsync(InfosysDbContext db, int id)
    {
        var wear = await db
            .WearTypes.AsNoTracking()
            .Include(w => w.Prices)
                .ThenInclude(p => p.ParticipantCategory)
            .Include(w => w.VariantAttributes)
                .ThenInclude(v => v.Attribute)
            .Include(w => w.ImageLinks)
            .FirstOrDefaultAsync(w => w.Id == id);
        if (wear == null)
            return null;

        var priceIds = wear.Prices.Select(p => p.Id).ToList();
        var orderCounts = await db
            .ParticipantWearOrders.Where(o => priceIds.Contains(o.WearPriceId))
            .GroupBy(o => o.WearPriceId)
            .Select(g => new { WearPriceId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(c => c.WearPriceId, c => c.Count);

        return new WearTypeDto(
            wear.Id,
            wear.NameDanish,
            wear.NameEnglish,
            wear.DescriptionDanish,
            wear.DescriptionEnglish,
            wear.MaxOrder,
            wear.IsRequired,
            Variants(wear.VariantAttributes),
            wear.Prices.OrderBy(p => p.Id)
                .Select(p => new WearPriceDto(
                    p.Id,
                    p.ParticipantCategoryId,
                    p.ParticipantCategory.Name,
                    p.Price,
                    orderCounts.GetValueOrDefault(p.Id)
                ))
                .ToList(),
            wear.ImageLinks.OrderBy(l => l.Id).Select(l => new WearImageLinkDto(l.WearImageId, l.AttributeId)).ToList(),
            orderCounts.Values.Sum()
        );
    }

    // Wear::getVariants: the variants in number order, each one's attributes by kind (model, design, color, size) and
    // then position
    public static IReadOnlyList<IReadOnlyList<WearAttributeDto>> Variants(
        IEnumerable<WearVariantAttribute> attributes
    ) =>
        attributes
            .GroupBy(v => v.Variant)
            .OrderBy(g => g.Key)
            .Select(g =>
                (IReadOnlyList<WearAttributeDto>)
                    g.Select(v => v.Attribute)
                        .OrderBy(a => a.Kind)
                        .ThenBy(a => a.Position)
                        .ThenBy(a => a.Id)
                        .Select(ToDto)
                        .ToList()
            )
            .ToList();

    public static WearAttributeDto ToDto(WearAttribute a) => new(a.Id, a.Kind, a.NameDanish, a.NameEnglish, a.Position);

    // The reasons tell the page which message to show
    private static object InUse(int orderCount) =>
        new
        {
            message = "Participants have ordered the wear type, so it can't be deleted",
            reason = "inUse",
            orderCount,
        };

    private static object PriceOrdered(string categoryName) =>
        new
        {
            message = "Participants have ordered at the price, so it can't be removed",
            reason = "priceOrdered",
            categoryName,
        };

    private async Task<(bool IsValid, string Error)> ValidateAsync(WearTypeFieldsDto dto)
    {
        var categoryIds = (await db.ParticipantCategories.Select(c => c.Id).ToListAsync()).ToHashSet();
        var attributeIds = (await db.WearAttributes.Select(a => a.Id).ToListAsync()).ToHashSet();
        var imageIds = (await db.WearImages.Select(i => i.Id).ToListAsync()).ToHashSet();
        return Validate(dto, categoryIds, attributeIds, imageIds);
    }

    private static void Apply(WearType wear, WearTypeFieldsDto dto)
    {
        wear.NameDanish = dto.NameDanish!;
        wear.NameEnglish = dto.NameEnglish ?? string.Empty;
        wear.DescriptionDanish = dto.DescriptionDanish ?? string.Empty;
        wear.DescriptionEnglish = dto.DescriptionEnglish ?? string.Empty;
        wear.MaxOrder = dto.MaxOrder ?? 0;
        wear.IsRequired = dto.IsRequired;
    }

    // Infosys numbers the variants by their place on the form; a variant with nothing ticked is dropped. Rows that stay
    // are kept, so the same key is never deleted and added again.
    private void ApplyVariants(WearType wear, WearTypeFieldsDto dto)
    {
        var wanted = new HashSet<(int Variant, int AttributeId)>();
        var number = 0;
        foreach (var variant in dto.Variants ?? [])
        {
            var attributeIds = variant.Distinct().ToList();
            if (attributeIds.Count == 0)
                continue;

            foreach (var attributeId in attributeIds)
            {
                wanted.Add((number, attributeId));
            }

            number++;
        }

        foreach (var old in wear.VariantAttributes.Where(v => !wanted.Contains((v.Variant, v.AttributeId))).ToList())
        {
            wear.VariantAttributes.Remove(old);
            db.WearVariantAttributes.Remove(old);
        }

        foreach (var (variant, attributeId) in wanted)
        {
            if (!wear.VariantAttributes.Any(v => v.Variant == variant && v.AttributeId == attributeId))
                wear.VariantAttributes.Add(new WearVariantAttribute { Variant = variant, AttributeId = attributeId });
        }
    }

    private void ApplyImageLinks(WearType wear, WearTypeFieldsDto dto)
    {
        var wanted = (dto.ImageLinks ?? []).ToHashSet();
        foreach (var old in wear.ImageLinks.Where(l => !wanted.Contains(new(l.WearImageId, l.AttributeId))).ToList())
        {
            wear.ImageLinks.Remove(old);
            db.WearImageLinks.Remove(old);
        }

        foreach (var link in wanted)
        {
            if (!wear.ImageLinks.Any(l => l.WearImageId == link.WearImageId && l.AttributeId == link.AttributeId))
                wear.ImageLinks.Add(
                    new WearImageLink { WearImageId = link.WearImageId, AttributeId = link.AttributeId }
                );
        }
    }

    private string? CurrentUserId() => User?.FindFirstValue(ClaimTypes.NameIdentifier);
}
