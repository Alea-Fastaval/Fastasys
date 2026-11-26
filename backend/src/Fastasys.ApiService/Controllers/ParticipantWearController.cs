using System.Security.Claims;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fastasys.ApiService.Controllers;

// An order on the edit page: "{Amount} stk. {Name} str. {Size} — ( {CategoryName} - {UnitPrice} )"
public record ParticipantWearOrderRowDto(
    int Id,
    int WearTypeId,
    int WearPriceId,
    IReadOnlyList<int> AttributeIds,
    string Name,
    string? Size,
    string CategoryName,
    int UnitPrice,
    int Amount,
    bool IsReceived
);

// A wear type in the page's selects (Infosys wear/ajaxgetwear/:id:): its prices, "{CategoryName} - {Price}", in the
// order they were added, and its variants for the attribute selects
public record WearTypeOptionDto(
    int Id,
    string NameDanish,
    IReadOnlyList<WearPriceOptionDto> Prices,
    IReadOnlyList<IReadOnlyList<WearAttributeDto>> Variants
);

public record WearPriceOptionDto(int Id, string CategoryName, int Price);

// The Wear part of Infosys's "Rediger indgang, mad og wear" page (deltager/updatemadwear/:id:): the participant's
// orders and the wear types by position
public record ParticipantWearDto(
    int ParticipantId,
    IReadOnlyList<ParticipantWearOrderRowDto> Orders,
    IReadOnlyList<WearTypeOptionDto> WearTypes
);

// An order the participant should have. With an Id it is one of their orders, kept with its "Udleveret" and given the
// amount; without one it is new: a price of a wear type, an attribute of each kind of one of its variants, and an
// amount.
public record ParticipantWearOrderFieldsDto(int? Id, int? WearPriceId, IReadOnlyList<int>? AttributeIds, int? Amount);

// The page's "Opdater": the orders the participant should have
public record UpdateParticipantWearDto(IReadOnlyList<ParticipantWearOrderFieldsDto>? Orders);

// Infosys ParticipantController::updateMadWear and ParticipantModel::handleWearUpdate, for the wear. Infosys lets only
// Infonaut and admin change a participant's wear ("Kun infonauter kan opdatere wear"); here it takes
// wear_participant_edit.
[ApiController]
[Route("api/participants/{participantId:int}/wear")]
[Authorize(Policy = "ParticipantWearManagement")]
public class ParticipantWearController(InfosysDbContext db, ILogger<ParticipantWearController> logger) : ControllerBase
{
    private const string NotFoundMessage = "Participant not found";

    [HttpGet]
    public async Task<ActionResult<ParticipantWearDto>> Get(int participantId)
    {
        if (!await db.Participants.AnyAsync(p => p.Id == participantId))
            return NotFound(new { message = NotFoundMessage });

        var orders = await WearOrderLoader.LoadAsync(
            db.ParticipantWearOrders.Where(o => o.ParticipantId == participantId)
        );
        var wearTypes = await db
            .WearTypes.AsNoTracking()
            .Include(w => w.Prices)
                .ThenInclude(p => p.ParticipantCategory)
            .Include(w => w.VariantAttributes)
                .ThenInclude(v => v.Attribute)
            .OrderBy(w => w.Position)
            .ThenBy(w => w.Id)
            .ToListAsync();

        return Ok(
            new ParticipantWearDto(
                participantId,
                orders
                    .Select(o => new ParticipantWearOrderRowDto(
                        o.Id,
                        o.WearTypeId,
                        o.WearPriceId,
                        o.Attributes.Select(a => a.Id).ToList(),
                        o.Name,
                        o.Size,
                        o.PriceCategoryName,
                        o.UnitPrice,
                        o.Amount,
                        o.IsReceived
                    ))
                    .ToList(),
                wearTypes
                    .Select(w => new WearTypeOptionDto(
                        w.Id,
                        w.NameDanish,
                        w.Prices.OrderBy(p => p.Id)
                            .Select(p => new WearPriceOptionDto(p.Id, p.ParticipantCategory.Name, p.Price))
                            .ToList(),
                        WearTypesController.Variants(w.VariantAttributes)
                    ))
                    .ToList()
            )
        );
    }

    // "Opdater": the list replaces the participant's orders. Infosys deleted all of them and added the posted ones
    // back, so "Udleveret" was reset on every order kept; a kept order now keeps it (fixed 2026-11-26). Infosys saved
    // any amount and any attributes it was sent; an amount must now be 1 or more and the attributes must be a choice in
    // one of the wear type's variants (fixed 2026-11-26).
    [HttpPut]
    public async Task<IActionResult> Update(int participantId, [FromBody] UpdateParticipantWearDto dto)
    {
        if (!await db.Participants.AnyAsync(p => p.Id == participantId))
            return NotFound(new { message = NotFoundMessage });

        var posted = dto.Orders ?? [];
        var current = await db.ParticipantWearOrders.Where(o => o.ParticipantId == participantId).ToListAsync();
        var priceIds = posted.Where(o => o.Id == null).Select(o => o.WearPriceId ?? 0).Distinct().ToList();
        var prices = await db
            .WearPrices.AsNoTracking()
            .Where(p => priceIds.Contains(p.Id))
            .Select(p => new { p.Id, p.WearTypeId })
            .ToDictionaryAsync(p => p.Id, p => p.WearTypeId);
        var typeIds = prices.Values.Distinct().ToList();
        var variants = await db
            .WearVariantAttributes.AsNoTracking()
            .Where(v => typeIds.Contains(v.WearTypeId))
            .Select(v => new
            {
                v.WearTypeId,
                v.Variant,
                v.Attribute.Id,
                v.Attribute.Kind,
                v.Attribute.NameDanish,
                v.Attribute.Position,
            })
            .ToListAsync();
        var attributeIds = posted.SelectMany(o => o.AttributeIds ?? []).Distinct().ToList();
        var attributes = await db
            .WearAttributes.AsNoTracking()
            .Where(a => attributeIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => new WearRules.Attribute(a.Id, a.Kind, a.NameDanish, a.Position));

        var (isValid, error) = Validate(
            posted,
            current.Select(o => o.Id).ToHashSet(),
            prices,
            attributes,
            variants
                .GroupBy(v => v.WearTypeId)
                .ToDictionary(
                    g => g.Key,
                    g =>
                        (IReadOnlyCollection<WearRules.VariantAttribute>)
                            g.Select(v => new WearRules.VariantAttribute(
                                    v.Variant,
                                    new WearRules.Attribute(v.Id, v.Kind, v.NameDanish, v.Position)
                                ))
                                .ToList()
                )
        );
        if (!isValid)
            return BadRequest(new { message = error });

        var kept = posted.Where(o => o.Id != null).ToDictionary(o => o.Id!.Value, o => o.Amount!.Value);
        foreach (var order in current)
        {
            if (kept.TryGetValue(order.Id, out var amount))
                order.Amount = amount;
            else
                db.ParticipantWearOrders.Remove(order);
        }

        foreach (var order in posted.Where(o => o.Id == null))
        {
            var newOrder = new ParticipantWearOrder
            {
                ParticipantId = participantId,
                WearPriceId = order.WearPriceId!.Value,
                Amount = order.Amount!.Value,
            };
            foreach (var attributeId in (order.AttributeIds ?? []).Distinct())
            {
                newOrder.Attributes.Add(new ParticipantWearOrderAttribute { AttributeId = attributeId });
            }

            db.ParticipantWearOrders.Add(newOrder);
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // The wear was changed at the same time, or a price was removed meanwhile
            return Conflict(new { message = "The wear orders were changed at the same time; try again" });
        }

        logger.LogInformation(
            "Wear orders of participant {ParticipantId} updated by user {UserId}",
            participantId,
            CurrentUserId()
        );

        return NoContent();
    }

    // Returns whether the posted orders are valid and, if not, why. Invalid input is expected, so it never throws.
    // currentIds are the participant's order ids; prices maps the known price ids to their wear type; variants holds
    // each wear type's variant attributes (missing for a type without variants).
    public static (bool IsValid, string Error) Validate(
        IReadOnlyList<ParticipantWearOrderFieldsDto> orders,
        IReadOnlySet<int> currentIds,
        IReadOnlyDictionary<int, int> prices,
        IReadOnlyDictionary<int, WearRules.Attribute> attributes,
        IReadOnlyDictionary<int, IReadOnlyCollection<WearRules.VariantAttribute>> variants
    )
    {
        var keptIds = new HashSet<int>();
        foreach (var order in orders)
        {
            // Infosys's page added a new order only with an amount above 0, but saved whatever it was sent
            if (order.Amount is null or < 1)
                return (false, "The amount must be a whole number of 1 or more");

            if (order.Id is { } id)
            {
                if (!currentIds.Contains(id))
                    return (false, "Unknown wear order");
                if (!keptIds.Add(id))
                    return (false, "A wear order is listed twice");
                continue;
            }

            if (order.WearPriceId is not { } priceId || !prices.TryGetValue(priceId, out var wearTypeId))
                return (false, "Unknown wear price");

            var chosen = new List<WearRules.Attribute>();
            foreach (var attributeId in order.AttributeIds ?? [])
            {
                if (!attributes.TryGetValue(attributeId, out var attribute))
                    return (false, "Unknown wear attribute");
                chosen.Add(attribute);
            }

            if (!WearRules.MatchesVariant(chosen, variants.GetValueOrDefault(wearTypeId) ?? []))
                return (false, "The attributes aren't a choice in one of the wear type's variants");
        }

        return (true, string.Empty);
    }

    private string? CurrentUserId() => User?.FindFirstValue(ClaimTypes.NameIdentifier);
}
