using System.Security.Claims;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fastasys.ApiService.Controllers;

// Infosys wear/breakdown ("Bestillings-oversigt"): the size columns (every size attribute by position) and the rows
// (see WearRules.Breakdown). A row reads "({WearTypeId}) {WearTypeName} - {attribute} - ...", then I alt (Total),
// Ingen størrelse (NoSize) and the amount per size (SizeCounts, sizes with none left out).
public record WearBreakdownDto(IReadOnlyList<WearAttributeDto> Sizes, IReadOnlyList<WearBreakdownRowDto> Rows);

public record WearBreakdownRowDto(
    int WearTypeId,
    string WearTypeName,
    IReadOnlyList<WearAttributeDto> Attributes,
    bool StartsVariant,
    int Total,
    int NoSize,
    IReadOnlyList<WearSizeCountDto> SizeCounts
);

public record WearSizeCountDto(int SizeId, int Count);

// An order on the order lists: "Kategori: {Name}", the price's category, "Antal: {Amount}", "Str.: {Size}" and
// "Status: Udleveret / Ikke udleveret"
public record WearOrderLineDto(
    int Id,
    string Name,
    string PriceCategoryName,
    int Amount,
    string? Size,
    bool IsReceived
);

// Infosys wear/detailed ("Alle bestillinger") and wear/unfilled ("Alle ikke-udleverede"): a participant with their
// orders. "ID: {ParticipantId} — Navn: {Name} — {CategoryName}".
public record WearOrderListParticipantDto(
    int ParticipantId,
    string Name,
    string CategoryName,
    IReadOnlyList<WearOrderLineDto> Orders
);

// Infosys wear/detailed/:id: ("Liste over {WearTypeName} - {attribute} - ... bestillinger"), the list behind a number
// of Bestillings-oversigt
public record WearMiniListDto(
    int WearTypeId,
    string WearTypeName,
    IReadOnlyList<WearAttributeDto> Attributes,
    IReadOnlyList<WearMiniListOrderDto> Orders
);

public record WearMiniListOrderDto(int ParticipantId, string ParticipantName, WearOrderLineDto Order);

// "Marker udleveret" / "Fortryd udlevering"
public record UpdateWearOrderReceivedDto(bool? IsReceived);

// Infosys WearController's order pages. Annulled participants are left out of the counts and lists (agreed
// 2026-11-26; Infosys counted them). Bestillings-oversigt takes wear_view (Infosys's Read-only role saw it); the lists
// show participants and take wear_list_view or wear_distribute_edit; marking an order takes wear_distribute_edit
// (WearController/* in Infosys).
[ApiController]
[Route("api/wear-orders")]
[Authorize]
public class WearOrdersController(InfosysDbContext db, ILogger<WearOrdersController> logger) : ControllerBase
{
    // WearModel::getWearBreakdown: the wear types by position
    [HttpGet("breakdown")]
    [Authorize(Policy = "WearView")]
    public async Task<ActionResult<WearBreakdownDto>> GetBreakdown()
    {
        var wearTypes = await db
            .WearTypes.AsNoTracking()
            .OrderBy(w => w.Position)
            .ThenBy(w => w.Id)
            .Select(w => new
            {
                w.Id,
                w.NameDanish,
                Variants = w
                    .VariantAttributes.Select(v => new
                    {
                        v.Variant,
                        v.Attribute.Id,
                        v.Attribute.Kind,
                        v.Attribute.NameDanish,
                        v.Attribute.Position,
                    })
                    .ToList(),
            })
            .ToListAsync();
        var orders = await WearOrderLoader.LoadAsync(db.ParticipantWearOrders.Where(o => !o.Participant.IsAnnulled));
        var attributes = await db.WearAttributes.AsNoTracking().ToDictionaryAsync(a => a.Id);

        var rows = WearRules.Breakdown(
            wearTypes
                .Select(w => new WearRules.WearTypeVariants(
                    w.Id,
                    w.NameDanish,
                    w.Variants.Select(v => new WearRules.VariantAttribute(
                            v.Variant,
                            new WearRules.Attribute(v.Id, v.Kind, v.NameDanish, v.Position)
                        ))
                        .ToList()
                ))
                .ToList(),
            orders.Select(o => new WearRules.Order(o.Id, o.WearTypeId, o.Amount, o.Attributes)).ToList()
        );

        var sizes = attributes
            .Values.Where(a => a.Kind == WearAttributeKind.Size)
            .OrderBy(a => a.Position)
            .ThenBy(a => a.Id)
            .Select(WearTypesController.ToDto)
            .ToList();

        return Ok(
            new WearBreakdownDto(
                sizes,
                rows.Select(r => new WearBreakdownRowDto(
                        r.WearTypeId,
                        r.WearTypeName,
                        r.Attributes.Select(a => WearTypesController.ToDto(attributes[a.Id])).ToList(),
                        r.StartsVariant,
                        r.Total,
                        r.NoSize,
                        sizes
                            .Where(s => r.BySize.ContainsKey(s.Id))
                            .Select(s => new WearSizeCountDto(s.Id, r.BySize[s.Id]))
                            .ToList()
                    ))
                    .ToList()
            )
        );
    }

    // Infosys wear/detailed (unfilled false) and wear/unfilled (unfilled true): the participants with orders, by id.
    // "Alle ikke-udleverede" lists only the orders that aren't handed out; Infosys listed every order of a participant
    // with one of them (changed 2026-11-26).
    [HttpGet]
    [Authorize(Policy = "WearListView")]
    public async Task<ActionResult<IEnumerable<WearOrderListParticipantDto>>> GetAll([FromQuery] bool unfilled = false)
    {
        var participantOrders = db.ParticipantWearOrders.Where(o => !o.Participant.IsAnnulled);
        if (unfilled)
            participantOrders = participantOrders.Where(o => !o.IsReceived);

        var orders = await WearOrderLoader.LoadAsync(participantOrders);
        var ids = orders.Select(o => o.ParticipantId).Distinct().ToList();
        var participants = await db
            .Participants.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                p.FirstName,
                p.LastName,
                p.Nickname,
                CategoryName = p.Category.Name,
            })
            .ToDictionaryAsync(p => p.Id);

        return Ok(
            orders
                .GroupBy(o => o.ParticipantId)
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    var p = participants[g.Key];
                    return new WearOrderListParticipantDto(
                        p.Id,
                        Participant.DisplayName(p.FirstName, p.LastName, p.Nickname),
                        p.CategoryName,
                        g.Select(ToLine).ToList()
                    );
                })
                .ToList()
        );
    }

    // Infosys wear/detailed/:id:?model=..&size=..: the type's orders that have every attribute given. noSize is the
    // "Ingen størrelse" column: orders without a size. Infosys skipped an attribute no order had, which is how its
    // size=none link showed every order of the type (fixed 2026-11-26).
    [HttpGet("by-type/{wearTypeId:int}")]
    [Authorize(Policy = "WearListView")]
    public async Task<ActionResult<WearMiniListDto>> GetByType(
        int wearTypeId,
        [FromQuery] int[]? attributeIds,
        [FromQuery] bool noSize = false
    )
    {
        var wear = await db.WearTypes.AsNoTracking().FirstOrDefaultAsync(w => w.Id == wearTypeId);
        if (wear == null)
            return NotFound(new { message = "Wear type not found" });

        var wanted = (attributeIds ?? []).Distinct().ToList();
        var attributes = await db
            .WearAttributes.AsNoTracking()
            .Where(a => wanted.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id);
        if (attributes.Count != wanted.Count)
            return BadRequest(new { message = "Unknown wear attribute" });

        var query = db.ParticipantWearOrders.Where(o =>
            !o.Participant.IsAnnulled && o.WearPrice.WearTypeId == wearTypeId
        );
        foreach (var attributeId in wanted)
        {
            query = query.Where(o => o.Attributes.Any(a => a.AttributeId == attributeId));
        }

        if (noSize)
            query = query.Where(o => !o.Attributes.Any(a => a.Attribute.Kind == WearAttributeKind.Size));

        var orders = await WearOrderLoader.LoadAsync(query);
        var ids = orders.Select(o => o.ParticipantId).Distinct().ToList();
        var names = await db
            .Participants.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                p.FirstName,
                p.LastName,
                p.Nickname,
            })
            .ToDictionaryAsync(p => p.Id, p => Participant.DisplayName(p.FirstName, p.LastName, p.Nickname));

        return Ok(
            new WearMiniListDto(
                wear.Id,
                wear.NameDanish,
                wanted.Select(id => WearTypesController.ToDto(attributes[id])).ToList(),
                orders
                    .Select(o => new WearMiniListOrderDto(o.ParticipantId, names[o.ParticipantId], ToLine(o)))
                    .ToList()
            )
        );
    }

    // Infosys wear/detailed/ajax/ flipped the first order with the participant and price, so with two orders of the
    // same wear and price (e.g. two sizes) it could change the wrong one; here the order itself is marked (fixed
    // 2026-11-26)
    [HttpPut("{id:int}/received")]
    [Authorize(Policy = "WearDistribution")]
    public async Task<IActionResult> UpdateReceived(int id, [FromBody] UpdateWearOrderReceivedDto dto)
    {
        if (dto.IsReceived is not { } isReceived)
            return BadRequest(new { message = "IsReceived is required" });

        var order = await db.ParticipantWearOrders.FindAsync(id);
        if (order == null)
            return NotFound(new { message = "Wear order not found" });

        order.IsReceived = isReceived;
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Wear order {WearOrderId} of participant {ParticipantId} marked {State} by user {UserId}",
            id,
            order.ParticipantId,
            isReceived ? "received" : "not received",
            CurrentUserId()
        );

        return NoContent();
    }

    private static WearOrderLineDto ToLine(WearOrderLoader.LoadedOrder o) =>
        new(o.Id, o.Name, o.PriceCategoryName, o.Amount, o.Size, o.IsReceived);

    private string? CurrentUserId() => User?.FindFirstValue(ClaimTypes.NameIdentifier);
}
