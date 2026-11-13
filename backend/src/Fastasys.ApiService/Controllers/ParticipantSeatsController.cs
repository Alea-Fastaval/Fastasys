using System.Security.Claims;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fastasys.ApiService.Controllers;

// Infosys deltager/updateaktivitet/:id: ("Opdater Aktiviteter", the "Ret/slet" link of the Aktiviteter box)
public record ParticipantSeatsDto(
    int ParticipantId,
    // The participant's seats on runs, by start time. Infosys also lists a designer's rows without a run here, but
    // they can't be edited, so they are left out.
    IReadOnlyList<ParticipantSeatDto> Seats,
    // The "Aktivitet" select and what Infosys's AJAX endpoints offer below it
    IReadOnlyList<SeatActivityOptionDto> Activities,
    // "Deltagerens tilmeldinger": the participant's signup wishes, by the schedule's start
    IReadOnlyList<SeatWishDto> Wishes
);

// A row of "Deltagerens tilmeldinger": "{Title} / {TitleEnglish} — {Onsdag 10:00-14:00}, {spiller}, {Priority}.
// prioritet". RunId is the run its "Tilføj" button gives the participant a seat on: the first run of the schedule (id
// order) with room for the role (Afviklinger::getGroupNeeding, ActivityController::ajaxGetRandomHold). It is null, and
// the button is not shown, when no run has room or when the activity is time-exclusive and the participant is busy
// then. Infosys's button never worked (it needs Prototype.js, which the page doesn't load); fixed on 2026-11-13.
public record SeatWishDto(
    int ScheduleId,
    RunSeatRole Role,
    int Priority,
    string Title,
    string TitleEnglish,
    DateTime Start,
    DateTime End,
    int? RunId,
    int? RunNumber);

// A row of the seat list: "{Title} / {TitleEnglish} — {spiller|spilleder}, hold {RunNumber}, {Onsdag 10:00-14:00}"
public record ParticipantSeatDto(
    int RunId,
    int RunNumber,
    RunSeatRole Role,
    string Title,
    string TitleEnglish,
    DateTime Start,
    DateTime End);

// An activity in the "Aktivitet" select ("{Title} / {TitleEnglish}"): those that can be signed up for, by title
public record SeatActivityOptionDto(int Id, string Title, string TitleEnglish, IReadOnlyList<SeatScheduleOptionDto> Schedules);

// A schedule in the "Tid" select (Infosys aktivitet/getafviklinger), in id order. IsBusy disables it.
public record SeatScheduleOptionDto(int Id, DateTime Start, DateTime End, bool IsBusy, IReadOnlyList<SeatRunOptionDto> Runs);

// A run in the "Hold" select (Infosys aktivitet/gethold), in id order. The run is disabled when it can use no more
// players and needs no more game masters; the role select (aktivitet/getholdneeds) disables "Spiller" when
// CanUsePlayers is false and "Spilleder" when NeedsGameMasters is false.
public record SeatRunOptionDto(int Id, int Number, bool CanUsePlayers, bool NeedsGameMasters);

// The list the page posts with "Opdater" (Infosys hold_id[] and type[])
public record UpdateParticipantSeatsDto(IReadOnlyList<ParticipantSeatChoiceDto> Seats);

public record ParticipantSeatChoiceDto(int RunId, RunSeatRole Role);

// Infosys ParticipantController::updateAktiviteter and ParticipantModel::updateAktiviteter. Infosys lets the roles
// Activity-admin, Infonaut and admin use it (plus one hard-coded e-mail address, which is not ported). Here it takes
// the right to edit participants and the right to edit activities (whose seats it changes, like the run page).
[ApiController]
[Route("api/participants/{participantId:int}/seats")]
[Authorize(Policy = "ParticipantManagement")]
[Authorize(Policy = "ActivityManagement")]
public class ParticipantSeatsController : ControllerBase
{
    // Infosys: "Deltagerens aktiviteter kan ikke ændres." The reason tells it apart from a seat taken at the same time.
    private static readonly object Locked = new { message = "The participant's activities are locked", reason = "locked" };

    private readonly InfosysDbContext _db;
    private readonly ILogger<ParticipantSeatsController> _logger;

    public ParticipantSeatsController(InfosysDbContext db, ILogger<ParticipantSeatsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // The page. Infosys sends an activity-locked participant back to the participant page with "Deltagerens
    // aktiviteter kan ikke ændres."
    [HttpGet]
    public async Task<ActionResult<ParticipantSeatsDto>> Get(int participantId)
    {
        var participant = await _db.Participants.AsNoTracking().FirstOrDefaultAsync(p => p.Id == participantId);
        if (participant == null) return NotFound(new { message = "Participant not found" });
        if (participant.IsActivityLocked) return Conflict(Locked);

        var spots = await ParticipantSpots.LoadAsync(_db, participant);
        var busy = ParticipantSpots.BusyPeriods(spots).ToList();

        var seats = spots
            .Where(s => s.RunId != null)
            .Select(s => new ParticipantSeatDto(
                s.RunId!.Value,
                s.RunNumber!.Value,
                s.Role == ParticipantActivityRole.GameMaster ? RunSeatRole.GameMaster : RunSeatRole.Player,
                s.Title,
                s.TitleEnglish,
                s.Start,
                s.End))
            .ToList();

        var activities = await _db.Activities
            .AsNoTracking()
            .Where(a => a.CanSignUp)
            .Select(a => new
            {
                a.Id,
                a.Title,
                a.TitleEnglish,
                a.IsTimeExclusive,
                a.MaxPlayersPerTeam,
                a.GameMastersPerTeam,
                Schedules = a.Schedules
                    .OrderBy(s => s.Id)
                    .Select(s => new
                    {
                        s.Id,
                        s.StartTime,
                        s.EndTime,
                        Runs = s.Runs
                            .OrderBy(r => r.Id)
                            .Select(r => new
                            {
                                r.Id,
                                r.Number,
                                Players = r.Seats.Count(x => x.Role == RunSeatRole.Player),
                                GameMasters = r.Seats.Count(x => x.Role == RunSeatRole.GameMaster)
                            })
                            .ToList()
                    })
                    .ToList()
            })
            .ToListAsync();

        // Infosys sorts the activities with strcmp on navn: byte order, so upper case before lower case and æøå last
        var options = activities
            .OrderBy(a => a.Title, StringComparer.Ordinal)
            .Select(a => new SeatActivityOptionDto(
                a.Id,
                a.Title,
                a.TitleEnglish,
                a.Schedules
                    .Select(s => new SeatScheduleOptionDto(
                        s.Id,
                        s.StartTime,
                        s.EndTime,
                        // Infosys ActivityController::ajaxGetAfviklinger
                        a.IsTimeExclusive && ParticipantRules.IsBusyBetween(busy, s.StartTime, s.EndTime),
                        s.Runs
                            .Select(r => new SeatRunOptionDto(
                                r.Id,
                                r.Number,
                                RunRules.CanUsePlayers(r.Players, a.MaxPlayersPerTeam),
                                RunRules.NeedsGameMasters(r.GameMasters, a.GameMastersPerTeam)))
                            .ToList()))
                    .ToList()))
            .ToList();

        return Ok(new ParticipantSeatsDto(participantId, seats, options, await LoadWishesAsync(participantId, busy)));
    }

    private async Task<List<SeatWishDto>> LoadWishesAsync(int participantId, List<(DateTime Start, DateTime End)> busy)
    {
        var wishes = await ParticipantSignupRows.LoadAsync(_db, participantId);
        var scheduleIds = wishes.Select(w => w.ScheduleId).Distinct().ToList();
        var runs = await _db.Runs
            .AsNoTracking()
            .Where(r => scheduleIds.Contains(r.ScheduleId))
            .OrderBy(r => r.Id)
            .Select(r => new
            {
                r.Id,
                r.Number,
                r.ScheduleId,
                r.Schedule.Activity.MaxPlayersPerTeam,
                r.Schedule.Activity.GameMastersPerTeam,
                Players = r.Seats.Count(s => s.Role == RunSeatRole.Player),
                GameMasters = r.Seats.Count(s => s.Role == RunSeatRole.GameMaster)
            })
            .ToListAsync();

        return wishes
            .Select(w =>
            {
                var isBusy = w.IsTimeExclusive && ParticipantRules.IsBusyBetween(busy, w.Start, w.End);
                var run = isBusy
                    ? null
                    : runs.FirstOrDefault(r => r.ScheduleId == w.ScheduleId && (w.Role == RunSeatRole.Player
                        ? RunRules.CanUsePlayers(r.Players, r.MaxPlayersPerTeam)
                        : RunRules.NeedsGameMasters(r.GameMasters, r.GameMastersPerTeam)));
                return new SeatWishDto(w.ScheduleId, w.Role, w.Priority, w.Title, w.TitleEnglish, w.Start, w.End, run?.Id, run?.Number);
            })
            .ToList();
    }

    // "Opdater": Infosys ParticipantModel::updateAktiviteter. The posted list replaces the participant's seats:
    // - A seat on a run that is in the list is kept as it is. Its role is not changed, even if the list has the other
    //   role for that run (legacy behaviour, kept).
    // - A seat on a run that is not in the list is removed. An empty list removes every seat.
    // - A run in the list that the participant has no seat on gets a new seat with the first free seat number, if
    //   the run has room for the role (Pladser::setDeltagerPlads). If it hasn't, Infosys skips it without telling
    //   anyone and still says "Deltageren blev opdateret." (legacy behaviour, kept).
    // Infosys only greys out busy schedules in the select; it doesn't check them when saving, and neither does this.
    [HttpPut]
    public async Task<IActionResult> Update(int participantId, [FromBody] UpdateParticipantSeatsDto dto)
    {
        var choices = dto.Seats ?? [];
        if (choices.Any(c => !Enum.IsDefined(c.Role))) return BadRequest(new { message = "Unknown role" });
        if (choices.Select(c => c.RunId).Distinct().Count() != choices.Count)
        {
            // Infosys's page never posts a run twice
            return BadRequest(new { message = "A run is listed more than once" });
        }

        var participant = await _db.Participants.AsNoTracking().FirstOrDefaultAsync(p => p.Id == participantId);
        if (participant == null) return NotFound(new { message = "Participant not found" });
        if (participant.IsActivityLocked) return Conflict(Locked);

        var runIds = choices.Select(c => c.RunId).ToList();
        var runs = await _db.Runs
            .Include(r => r.Schedule).ThenInclude(s => s.Activity)
            .Include(r => r.Seats)
            .Where(r => runIds.Contains(r.Id))
            .ToListAsync();
        if (runs.Count != runIds.Count) return BadRequest(new { message = "Unknown run" });

        var currentSeats = await _db.RunSeats.Where(s => s.ParticipantId == participantId).ToListAsync();
        _db.RunSeats.RemoveRange(currentSeats.Where(s => !runIds.Contains(s.RunId)));

        foreach (var choice in choices)
        {
            if (currentSeats.Any(s => s.RunId == choice.RunId)) continue;

            var run = runs.Single(r => r.Id == choice.RunId);
            var players = run.Seats.Count(s => s.Role == RunSeatRole.Player);
            var gameMasters = run.Seats.Count(s => s.Role == RunSeatRole.GameMaster);
            if (!RunRules.HasRoomFor(choice.Role, players, gameMasters, run.Schedule.Activity)) continue;

            var seat = new RunSeat
            {
                RunId = run.Id,
                SeatNumber = RunRules.NextSeatNumber(run.Seats.Select(s => s.SeatNumber)),
                Role = choice.Role,
                ParticipantId = participantId
            };
            run.Seats.Add(seat);
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Another seat was taken at the same time (primary key run + seat number, unique run + participant)
            return Conflict(new { message = "Could not update the participant's seats" });
        }

        _logger.LogInformation("Participant {ParticipantId}'s seats updated by user {UserId}", participantId, CurrentUserId());
        return NoContent();
    }

    private string? CurrentUserId() => User?.FindFirstValue(ClaimTypes.NameIdentifier);
}
