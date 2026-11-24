using System.Security.Claims;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fastasys.ApiService.Controllers;

// A ticket type in the edit page's select and list, and in the participant search's Indgang section. The pages show
// it as "{Name} ({short day of Day})" and the search as "{Name} {short day of Day}", without the day when
// IsAllConvention.
public record TicketTypeOptionDto(int Id, string Name, DateOnly Day, bool IsAllConvention);

// The Indgang part of Infosys's "Rediger indgang, mad og wear" page (deltager/updatemadwear/:id:): the participant's
// tickets, in ticket type id order, and every ticket type the select offers, in id order
public record ParticipantTicketsDto(
    int ParticipantId,
    IReadOnlyList<TicketTypeOptionDto> Tickets,
    IReadOnlyList<TicketTypeOptionDto> TicketTypes
);

// The page's "Opdater" (Infosys indgang[]): the ticket types the participant should have
public record UpdateParticipantTicketsDto(IReadOnlyList<int>? TicketTypeIds);

// Infosys ParticipantController::updateMadWear and ParticipantModel::handleEntranceUpdate, for the tickets. Food and
// wear are on the same Infosys page; they come with their own ports. Infosys lets every role with
// ParticipantController/* change the tickets; here it takes entrance_participant_edit.
[ApiController]
[Route("api/participants/{participantId:int}/tickets")]
[Authorize(Policy = "ParticipantTicketManagement")]
public class ParticipantTicketsController(InfosysDbContext db, ILogger<ParticipantTicketsController> logger)
    : ControllerBase
{
    private const string NotFoundMessage = "Participant not found";

    [HttpGet]
    public async Task<ActionResult<ParticipantTicketsDto>> Get(int participantId)
    {
        if (!await db.Participants.AnyAsync(p => p.Id == participantId))
            return NotFound(new { message = NotFoundMessage });

        var tickets = await db
            .ParticipantTickets.AsNoTracking()
            .Where(t => t.ParticipantId == participantId)
            .OrderBy(t => t.TicketTypeId)
            .Select(t => new TicketTypeOptionDto(
                t.TicketType.Id,
                t.TicketType.Name,
                t.TicketType.Day,
                t.TicketType.IsAllConvention
            ))
            .ToListAsync();

        var types = await db
            .TicketTypes.AsNoTracking()
            .OrderBy(t => t.Id)
            .Select(t => new TicketTypeOptionDto(t.Id, t.Name, t.Day, t.IsAllConvention))
            .ToListAsync();

        return Ok(new ParticipantTicketsDto(participantId, tickets, types));
    }

    // "Opdater": the list replaces the participant's tickets. Like Infosys, an id that isn't a ticket type (deleted
    // since the page was opened) is skipped. The same type listed twice is kept once; in Infosys that was an error
    // page (fixed 2026-11-24).
    [HttpPut]
    public async Task<IActionResult> Update(int participantId, [FromBody] UpdateParticipantTicketsDto dto)
    {
        if (!await db.Participants.AnyAsync(p => p.Id == participantId))
            return NotFound(new { message = NotFoundMessage });

        var requested = (dto.TicketTypeIds ?? []).Distinct().ToList();
        var wanted = await db.TicketTypes.Where(t => requested.Contains(t.Id)).Select(t => t.Id).ToListAsync();
        var current = await db.ParticipantTickets.Where(t => t.ParticipantId == participantId).ToListAsync();

        db.ParticipantTickets.RemoveRange(current.Where(t => !wanted.Contains(t.TicketTypeId)));
        db.ParticipantTickets.AddRange(
            wanted
                .Where(id => !current.Any(t => t.TicketTypeId == id))
                .Select(id => new ParticipantTicket { ParticipantId = participantId, TicketTypeId = id })
        );

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // The tickets were changed at the same time, or a ticket type was deleted meanwhile
            return Conflict(new { message = "The tickets were changed at the same time; try again" });
        }

        logger.LogInformation(
            "Tickets of participant {ParticipantId} updated by user {UserId}",
            participantId,
            CurrentUserId()
        );

        return NoContent();
    }

    private string? CurrentUserId() => User?.FindFirstValue(ClaimTypes.NameIdentifier);
}
