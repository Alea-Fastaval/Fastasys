using System.Globalization;
using System.Security.Claims;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fastasys.ApiService.Controllers;

// "Indtast/scan deltager id": a participant id or a scanned badge
public record WearHandoutInputDto(string? Input);

// A line of the result: "{Name}: {Amount} stk.", then " i str. {Size}" when the order has a size
public record WearHandoutItemDto(string Name, int Amount, string? Size);

// The result of "Marker udleveret" or "Undo": the participant (ID and chosen name), their category, which "Marker
// udleveret" shows after each line, and their orders
public record WearHandoutResultDto(
    int ParticipantId,
    string Name,
    string CategoryName,
    IReadOnlyList<WearHandoutItemDto> Items
);

// Infosys "Wear-udlevering" (wear/handout, WearModel::markWearReceived and undoReceiveWear): scanning a participant
// hands out all their wear at once, and Undo takes it back. Infosys marked the orders one by one and stopped at the
// first that was already handed out, so the ones before it stayed changed; now nothing changes when it is refused
// (agreed 2026-11-26). Infosys meant to refuse a participant who isn't checked in, but its check of
// '0000-00-00 00:00:00' never fired; it is enforced here (agreed 2026-11-26). An annulled participant is refused too,
// which Infosys didn't do (changed 2026-11-26); Undo isn't, so a handout can still be taken back. The page took
// WearController/* in Infosys; here it takes wear_distribute_edit.
[ApiController]
[Route("api/wear-handout")]
[Authorize(Policy = "WearDistribution")]
public class WearHandoutController(InfosysDbContext db, ILogger<WearHandoutController> logger) : ControllerBase
{
    // "Marker udleveret"
    [HttpPost]
    public async Task<ActionResult<WearHandoutResultDto>> HandOut([FromBody] WearHandoutInputDto dto)
    {
        var (participant, refusal) = await FindParticipantAsync(dto.Input);
        if (participant == null)
            return refusal!;

        if (participant.IsAnnulled)
            return Refused("annulled", "The participant is annulled", participant.Id);

        // Infosys: "deltageren (ID: {id}) er ikke tjekket ind."
        if (participant.CheckedInAt == null)
            return Refused("notCheckedIn", "The participant isn't checked in", participant.Id);

        var orders = await db
            .ParticipantWearOrders.Where(o => o.ParticipantId == participant.Id)
            .OrderBy(o => o.Id)
            .ToListAsync();
        if (orders.Count == 0)
            return Refused("noWear", "The participant hasn't ordered wear", participant.Id);
        // Infosys: "deltageren (ID: {id}) har allerede fået udleveret noget af sit wear."
        if (orders.Any(o => o.IsReceived))
            return Refused("alreadyReceived", "Some of the participant's wear is already handed out", participant.Id);

        foreach (var order in orders)
        {
            order.IsReceived = true;
        }

        await db.SaveChangesAsync();

        logger.LogInformation(
            "Wear of participant {ParticipantId} handed out by user {UserId}",
            participant.Id,
            CurrentUserId()
        );

        return Ok(await ResultAsync(participant));
    }

    // "Undo": the last participant's wear isn't handed out after all
    [HttpPost("undo")]
    public async Task<ActionResult<WearHandoutResultDto>> Undo([FromBody] WearHandoutInputDto dto)
    {
        var (participant, refusal) = await FindParticipantAsync(dto.Input);
        if (participant == null)
            return refusal!;

        var orders = await db
            .ParticipantWearOrders.Where(o => o.ParticipantId == participant.Id)
            .OrderBy(o => o.Id)
            .ToListAsync();
        if (orders.Count == 0)
            return Refused("noWear", "The participant hasn't ordered wear", participant.Id);
        // Infosys: "deltageren (ID: {id}) har ikke fået udleveret sit wear."
        if (orders.Any(o => !o.IsReceived))
            return Refused("notReceived", "Not all of the participant's wear is handed out", participant.Id);

        foreach (var order in orders)
        {
            order.IsReceived = false;
        }

        await db.SaveChangesAsync();

        logger.LogInformation(
            "Wear of participant {ParticipantId} marked not handed out by user {UserId}",
            participant.Id,
            CurrentUserId()
        );

        return Ok(await ResultAsync(participant));
    }

    // Infosys WearModel::ajaxWearCrud: the id, or the id in a scanned badge (EANToNumber), must be a participant who
    // isn't marked as a no-show ("Udeblevet")
    private async Task<(Participant? Participant, ActionResult? Refusal)> FindParticipantAsync(string? input)
    {
        // Infosys: "bruger id mangler" (PHP's empty(), so "0" too)
        if (string.IsNullOrEmpty(input) || input == "0")
            return (null, BadRequest(new { message = "The participant id is missing", reason = "noInput" }));

        var id = ParticipantIdOf(input, CopenhagenNow().Year);
        var participant =
            id == null ? null : await db.Participants.Include(p => p.Category).FirstOrDefaultAsync(p => p.Id == id);
        // Infosys: "ingen bruger med det id"
        if (participant == null)
            return (null, NotFound(new { message = "No participant has that id", reason = "notFound" }));

        // Infosys: "deltageren er ikke tjekket ind endnu"
        if (participant.IsNoShow)
            return (null, Refused("noShow", "The participant is marked as a no-show", participant.Id));

        return (participant, null);
    }

    // A scanned badge's id, or else the number typed
    public static int? ParticipantIdOf(string input, int year)
    {
        var fromBarcode = ParticipantRules.IdFromBarcode(input, year);
        if (fromBarcode is { } barcodeId)
            return barcodeId is > 0 and <= int.MaxValue ? (int)barcodeId : null;

        return int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var typed) && typed > 0
            ? typed
            : null;
    }

    private async Task<WearHandoutResultDto> ResultAsync(Participant participant)
    {
        var orders = await WearOrderLoader.LoadAsync(
            db.ParticipantWearOrders.Where(o => o.ParticipantId == participant.Id)
        );
        return new WearHandoutResultDto(
            participant.Id,
            participant.DisplayName(),
            participant.Category.Name,
            orders.Select(o => new WearHandoutItemDto(o.Name, o.Amount, o.Size)).ToList()
        );
    }

    private ConflictObjectResult Refused(string reason, string message, int participantId) =>
        Conflict(
            new
            {
                message,
                reason,
                participantId,
            }
        );

    private static DateTime CopenhagenNow() =>
        TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Europe/Copenhagen");

    private string? CurrentUserId() => User?.FindFirstValue(ClaimTypes.NameIdentifier);
}
