using System.Security.Claims;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fastasys.ApiService.Controllers;

// Infosys deltager/updatetilmelding/:id: ("Opdater Aktivitets-tilmeldinger", the "Ret/slet" link of the
// Aktivitetstilmeldinger box) and the dialog that opens when a row of that box is clicked
public record ParticipantSignupsDto(
    int ParticipantId,
    // The participant's wishes, by the schedule's start
    IReadOnlyList<ParticipantActivitySignupDto> Signups,
    IReadOnlyList<SignupActivityOptionDto> Activities
);

// An activity the pages can offer, in id order (Infosys getAllAktiviteter has no order). The edit page's "Aktivitet"
// select offers those with CanSignUp (Infosys kan_tilmeldes). The row dialog offers the schedules of the wish's own
// activity, so the activities of the participant's wishes are included even when they can't be signed up for.
// HasGameMasters is false for an activity without game masters, which can't get a game master wish.
public record SignupActivityOptionDto(
    int Id,
    string Title,
    string TitleEnglish,
    bool CanSignUp,
    bool HasGameMasters,
    IReadOnlyList<SignupScheduleOptionDto> Schedules);

// A schedule in the "Tid" selects, in id order. Infosys doesn't grey out busy schedules for wishes.
public record SignupScheduleOptionDto(int Id, DateTime Start, DateTime End);

// The edit page's "Opdater" (Infosys afvikling_id[], type[] and prioritet[])
public record UpdateParticipantSignupsDto(IReadOnlyList<ParticipantSignupChoiceDto> Signups);

public record ParticipantSignupChoiceDto(int ScheduleId, RunSeatRole Role, int Priority);

// The row dialog's "Opdater" (Infosys deltager/ajax/updateschedule): the wish's new schedule, role and priority
public record UpdateSignupDto(int ScheduleId, RunSeatRole Role, int Priority);

// "fine", or "conflict" with the player wishes at the same time that the user must sort ("Sorter efter prioritet")
public record SignupUpdateResultDto(string Status, IReadOnlyList<ParticipantActivitySignupDto> Conflicts);

// Infosys deltager/ajax/updateschedulepriorities: the schedules of the player wishes in the order the user sorted them
public record SignupPrioritiesDto(IReadOnlyList<int> ScheduleIds);

// Infosys ParticipantController::updateTilmeldinger, ajaxUpdateSchedule, ajaxUpdateSchedulePriorities and
// ajaxRemoveSchedule. Infosys lets every role with ParticipantController/* use them, so here the right to edit
// participants is enough (agreed 2026-11-13). Fixed on 2026-11-13, as agreed: the server refuses an activity-locked
// participant (Infosys only hid the link), a wish is changed or removed by schedule and role (Infosys hit both wishes
// on a schedule with a game master and a player wish), and errors are reported instead of dropped.
[ApiController]
[Route("api/participants/{participantId:int}/signups")]
[Authorize(Policy = "ParticipantManagement")]
public class ParticipantSignupsController : ControllerBase
{
    private const string ConflictStatus = "conflict";
    private const string FineStatus = "fine";

    // The reasons tell the pages which message to show
    private static readonly object Locked = new { message = "The participant's activities are locked", reason = "locked" };
    private static readonly object NotSignedUp = new { message = "Participant is not signed up for the schedule", reason = "notSignedUp" };
    private static readonly object AlreadySignedUp = new { message = "Participant is already signed up for the schedule", reason = "alreadySignedUp" };
    private static readonly object NoGameMasters = new { message = "The activity has no game masters", reason = "noGameMasters" };

    private readonly InfosysDbContext _db;
    private readonly ILogger<ParticipantSignupsController> _logger;

    public ParticipantSignupsController(InfosysDbContext db, ILogger<ParticipantSignupsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<ParticipantSignupsDto>> Get(int participantId)
    {
        var (participant, refusal) = await LoadParticipantAsync(participantId);
        if (participant == null) return refusal!;

        var signups = await ParticipantSignupRows.LoadAsync(_db, participantId);
        var wishedActivityIds = signups.Select(s => s.ActivityId).Distinct().ToList();

        var activities = await _db.Activities
            .AsNoTracking()
            .Where(a => a.CanSignUp || wishedActivityIds.Contains(a.Id))
            .OrderBy(a => a.Id)
            .Select(a => new SignupActivityOptionDto(
                a.Id,
                a.Title,
                a.TitleEnglish,
                a.CanSignUp,
                a.GameMastersPerTeam > 0,
                a.Schedules
                    .OrderBy(s => s.Id)
                    .Select(s => new SignupScheduleOptionDto(s.Id, s.StartTime, s.EndTime))
                    .ToList()))
            .ToListAsync();

        return Ok(new ParticipantSignupsDto(participantId, signups.Select(s => s.ToDto()).ToList(), activities));
    }

    // The edit page's "Opdater": Infosys ParticipantModel::updateTilmeldinger. The list replaces the participant's
    // wishes: a wish on a schedule and role in the list is kept with the list's priority (Infosys kept the old priority),
    // the others are removed, and the new ones are added. The same schedule and role can't be in the list twice
    // (Infosys's page could add it twice), and a game master wish for an activity without game masters is refused
    // (Infosys dropped it silently).
    [HttpPut]
    public async Task<IActionResult> Update(int participantId, [FromBody] UpdateParticipantSignupsDto dto)
    {
        var choices = dto.Signups ?? [];
        if (choices.Any(c => !Enum.IsDefined(c.Role))) return BadRequest(new { message = "Unknown role" });
        if (choices.Any(c => !SignupRules.IsValidPriority(c.Role, c.Priority))) return BadRequest(new { message = "Invalid priority" });
        if (choices.DistinctBy(c => (c.ScheduleId, c.Role)).Count() != choices.Count)
        {
            return BadRequest(new { message = "A schedule and role is listed more than once", reason = "duplicate" });
        }

        var (participant, refusal) = await LoadParticipantAsync(participantId);
        if (participant == null) return refusal!;

        var scheduleIds = choices.Select(c => c.ScheduleId).Distinct().ToList();
        var gameMastersBySchedule = await _db.Schedules
            .Where(s => scheduleIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Activity.GameMastersPerTeam })
            .ToDictionaryAsync(s => s.Id, s => s.GameMastersPerTeam);
        if (gameMastersBySchedule.Count != scheduleIds.Count) return BadRequest(new { message = "Unknown schedule" });

        var current = await _db.ScheduleSignups.Where(s => s.ParticipantId == participantId).ToListAsync();
        var isNew = (ParticipantSignupChoiceDto c) => !current.Any(s => s.ScheduleId == c.ScheduleId && s.Role == c.Role);
        if (choices.Any(c => isNew(c) && !SignupRules.CanWish(c.Role, gameMastersBySchedule[c.ScheduleId])))
        {
            return BadRequest(NoGameMasters);
        }

        foreach (var signup in current)
        {
            var choice = choices.FirstOrDefault(c => c.ScheduleId == signup.ScheduleId && c.Role == signup.Role);
            if (choice == null)
            {
                _db.ScheduleSignups.Remove(signup);
            }
            else
            {
                signup.Priority = choice.Priority;
            }
        }

        foreach (var choice in choices.Where(isNew))
        {
            _db.ScheduleSignups.Add(new ScheduleSignup
            {
                ParticipantId = participantId,
                ScheduleId = choice.ScheduleId,
                Role = choice.Role,
                Priority = choice.Priority
            });
        }

        if (!await TrySaveAsync()) return Conflict(new { message = "Could not update the participant's signups" });

        _logger.LogInformation("Participant {ParticipantId}'s signups updated by user {UserId}", participantId, CurrentUserId());
        return NoContent();
    }

    // The row dialog's "Opdater": Infosys ParticipantModel::updateSchedule. A game master wish gets priority 1. Moving
    // the wish to a schedule and role the participant already has is refused ("Kan ikke flytte afvikling - deltageren
    // er allerede tilmeldt afviklingen der flyttes til"). Afterwards a player wish is checked for priority conflicts
    // with the participant's other player wishes at the same time (see SignupRules.HasPriorityConflict).
    [HttpPut("{scheduleId:int}/{role}")]
    public async Task<ActionResult<SignupUpdateResultDto>> UpdateOne(int participantId, int scheduleId, RunSeatRole role, [FromBody] UpdateSignupDto dto)
    {
        if (!Enum.IsDefined(role) || !Enum.IsDefined(dto.Role)) return BadRequest(new { message = "Unknown role" });

        var priority = dto.Role == RunSeatRole.GameMaster ? SignupRules.GameMasterPriority : dto.Priority;
        if (!SignupRules.IsValidPriority(dto.Role, priority)) return BadRequest(new { message = "Invalid priority" });

        var (participant, refusal) = await LoadParticipantAsync(participantId);
        if (participant == null) return refusal!;

        var signup = await _db.ScheduleSignups.FirstOrDefaultAsync(s => s.ParticipantId == participantId && s.ScheduleId == scheduleId && s.Role == role);
        if (signup == null) return NotFound(NotSignedUp);

        var target = await _db.Schedules.Include(s => s.Activity).FirstOrDefaultAsync(s => s.Id == dto.ScheduleId);
        if (target == null) return BadRequest(new { message = "Unknown schedule" });

        var moves = dto.ScheduleId != scheduleId || dto.Role != role;
        if (moves)
        {
            if (!SignupRules.CanWish(dto.Role, target.Activity.GameMastersPerTeam)) return BadRequest(NoGameMasters);
            if (await _db.ScheduleSignups.AnyAsync(s => s.ParticipantId == participantId && s.ScheduleId == dto.ScheduleId && s.Role == dto.Role))
            {
                return Conflict(AlreadySignedUp);
            }

            // The schedule and role are the key, so the wish is replaced
            _db.ScheduleSignups.Remove(signup);
            _db.ScheduleSignups.Add(new ScheduleSignup { ParticipantId = participantId, ScheduleId = dto.ScheduleId, Role = dto.Role, Priority = priority });
        }
        else
        {
            signup.Priority = priority;
        }

        if (!await TrySaveAsync()) return Conflict(AlreadySignedUp);

        _logger.LogInformation("Participant {ParticipantId}'s signup for schedule {ScheduleId} updated by user {UserId}", participantId, dto.ScheduleId, CurrentUserId());

        if (dto.Role != RunSeatRole.Player) return Ok(new SignupUpdateResultDto(FineStatus, []));

        var concurrent = (await ParticipantSignupRows.LoadAsync(_db, participantId))
            .Where(s => s.Role == RunSeatRole.Player && RunRules.Overlaps(s.Start, s.End, target.StartTime, target.EndTime))
            .ToList();

        return SignupRules.HasPriorityConflict(concurrent.Select(s => s.Priority))
            ? Ok(new SignupUpdateResultDto(ConflictStatus, concurrent.OrderBy(s => s.Priority).ThenBy(s => s.Start).Select(s => s.ToDto()).ToList()))
            : Ok(new SignupUpdateResultDto(FineStatus, []));
    }

    // "Sorter efter prioritet": Infosys ParticipantModel::updateSignupPriorities. The player wishes on the listed
    // schedules get 1, 2, 3 and then 4 for the rest, in the listed order.
    [HttpPut("priorities")]
    public async Task<IActionResult> UpdatePriorities(int participantId, [FromBody] SignupPrioritiesDto dto)
    {
        var scheduleIds = dto.ScheduleIds ?? [];
        if (scheduleIds.Count == 0) return BadRequest(new { message = "No schedules to update" });
        if (scheduleIds.Distinct().Count() != scheduleIds.Count) return BadRequest(new { message = "A schedule is listed more than once" });

        var (participant, refusal) = await LoadParticipantAsync(participantId);
        if (participant == null) return refusal!;

        var wishes = await _db.ScheduleSignups
            .Where(s => s.ParticipantId == participantId && s.Role == RunSeatRole.Player && scheduleIds.Contains(s.ScheduleId))
            .ToListAsync();
        if (wishes.Count != scheduleIds.Count) return NotFound(NotSignedUp);

        var priorities = SignupRules.PrioritiesInOrder(scheduleIds.Count);
        for (var i = 0; i < scheduleIds.Count; i++)
        {
            wishes.Single(s => s.ScheduleId == scheduleIds[i]).Priority = priorities[i];
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("Participant {ParticipantId}'s signup priorities updated by user {UserId}", participantId, CurrentUserId());
        return NoContent();
    }

    // The row dialog's "Slet": Infosys ParticipantModel::removeParticipantSchedule
    [HttpDelete("{scheduleId:int}/{role}")]
    public async Task<IActionResult> Delete(int participantId, int scheduleId, RunSeatRole role)
    {
        if (!Enum.IsDefined(role)) return BadRequest(new { message = "Unknown role" });

        var (participant, refusal) = await LoadParticipantAsync(participantId);
        if (participant == null) return refusal!;

        var signup = await _db.ScheduleSignups.FirstOrDefaultAsync(s => s.ParticipantId == participantId && s.ScheduleId == scheduleId && s.Role == role);
        if (signup == null) return NotFound(NotSignedUp);

        _db.ScheduleSignups.Remove(signup);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Participant {ParticipantId}'s signup for schedule {ScheduleId} removed by user {UserId}", participantId, scheduleId, CurrentUserId());
        return NoContent();
    }

    // The participant, or why the request is refused: not found, or activity-locked
    private async Task<(Participant? Participant, ObjectResult? Refusal)> LoadParticipantAsync(int participantId)
    {
        var participant = await _db.Participants.AsNoTracking().FirstOrDefaultAsync(p => p.Id == participantId);
        if (participant == null) return (null, NotFound(new { message = "Participant not found" }));
        if (participant.IsActivityLocked) return (null, Conflict(Locked));
        return (participant, null);
    }

    private async Task<bool> TrySaveAsync()
    {
        try
        {
            await _db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException)
        {
            // The same wish was added at the same time (primary key participant + schedule + role)
            return false;
        }
    }

    private string? CurrentUserId() => User?.FindFirstValue(ClaimTypes.NameIdentifier);
}
