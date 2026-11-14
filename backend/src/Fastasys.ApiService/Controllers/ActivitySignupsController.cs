using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fastasys.ApiService.Controllers;

// The "Tilmeldinger" table under one schedule of the Infosys activity page (visaktivitet.phtml, .afviklingsbox-signups)
public record ScheduleSignupsDto(int ScheduleId, IReadOnlyList<SignupPoolRowDto> Signups);

// A row of the table. Infosys shows: an SL icon for a game master wish, the name (linked to the participant), Pri,
// karma, age, a heart, a language flag and a note icon; hovering the row shows the language, age, wanted activities
// and the note.
// - Karma is not ported yet, so it is always null (like the runs' karma).
// - Age is null when the birth date is unknown.
// - Language is "dk", "uk" or null (see SignupRules.LanguageFlag); ActivityLanguages is what the hover shows.
// - DesiredActivitiesReached is the heart: null for none, true for a filled one, false for an empty one.
// - Note is the participant's "Andre kommentarer"; only users who may edit participants get it (null otherwise).
// - IsOnTeam rows are hidden. IsBusy and IsLocked rows can't be dragged; IsDoubleBooked rows get a dashed border.
public record SignupPoolRowDto(
    int ParticipantId,
    string Name,
    RunSeatRole Role,
    int Priority,
    int? Karma,
    int? Age,
    string? Language,
    IReadOnlyList<ParticipantLanguage> ActivityLanguages,
    int DesiredActivities,
    bool? DesiredActivitiesReached,
    string? Note,
    bool IsOnTeam,
    bool IsBusy,
    bool IsLocked,
    bool IsDoubleBooked);

// Infosys ActivityController::visAktivitet's signup tables (with ActivityModel::getDoubleBookings and
// getDesiredActivityStats). Everyone who can see the activity page sees them, as in Infosys. Placing a participant on a
// run by dragging uses the runs' seat endpoints (Infosys groups/scheduleparticipant).
[ApiController]
[Route("api/activities/{activityId:int}/signups")]
[Authorize(Policy = "ActivityView")]
public class ActivitySignupsController : ControllerBase
{
    private readonly InfosysDbContext _db;
    private readonly IAuthorizationService _authorization;

    public ActivitySignupsController(InfosysDbContext db, IAuthorizationService authorization)
    {
        _db = db;
        _authorization = authorization;
    }

    // One table per schedule, in the activity page's order (start, then id)
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ScheduleSignupsDto>>> Get(int activityId)
    {
        var activity = await _db.Activities.AsNoTracking().FirstOrDefaultAsync(a => a.Id == activityId);
        if (activity == null) return NotFound(new { message = "Activity not found" });

        // The notes are for participant editors, as in the participant list
        var mayReadNotes = (await _authorization.AuthorizeAsync(User, "ParticipantManagement")).Succeeded;

        var schedules = await _db.Schedules
            .AsNoTracking()
            .Where(s => s.ActivityId == activityId)
            .OrderBy(s => s.StartTime)
            .ThenBy(s => s.Id)
            .Select(s => new { s.Id, s.StartTime, s.EndTime })
            .ToListAsync();
        var scheduleIds = schedules.Select(s => s.Id).ToList();

        var wishes = await _db.ScheduleSignups
            .AsNoTracking()
            .Where(w => scheduleIds.Contains(w.ScheduleId))
            .Select(w => new { w.ScheduleId, w.ParticipantId, w.Role, w.Priority })
            .ToListAsync();
        var participantIds = wishes.Select(w => w.ParticipantId).Distinct().ToList();
        var participants = await _db.Participants
            .AsNoTracking()
            .Where(p => participantIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        // Seats on the activity's runs (on-team), all player seats (the heart) and all wishes with their times
        // (double bookings)
        var activitySeats = await _db.RunSeats
            .AsNoTracking()
            .Where(s => s.Run.Schedule.ActivityId == activityId)
            .Select(s => new { s.ParticipantId, s.Run.ScheduleId, s.Role })
            .ToListAsync();
        var playerSeats = (await _db.RunSeats
                .AsNoTracking()
                .Where(s => participantIds.Contains(s.ParticipantId) && s.Role == RunSeatRole.Player)
                .Select(s => s.ParticipantId)
                .ToListAsync())
            .CountBy(id => id)
            .ToDictionary(c => c.Key, c => c.Value);
        var allWishes = await _db.ScheduleSignups
            .AsNoTracking()
            .Where(w => participantIds.Contains(w.ParticipantId))
            .Select(w => new { w.ParticipantId, w.ScheduleId, w.Role, w.Schedule.StartTime, w.Schedule.EndTime })
            .ToListAsync();
        var spots = await ParticipantSpots.LoadManyAsync(_db, participants.Values.ToList());
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Europe/Copenhagen"));

        bool IsDoubleBooked(int participantId, int scheduleId, DateTime start, DateTime end) =>
            allWishes.Any(w => w.ParticipantId == participantId && w.ScheduleId == scheduleId && w.Role == RunSeatRole.GameMaster)
            && allWishes.Any(w => w.ParticipantId == participantId && w.ScheduleId != scheduleId
                && SignupRules.OverlapsForDoubleBooking(start, end, w.StartTime, w.EndTime));

        return Ok(schedules.Select(schedule => new ScheduleSignupsDto(
            schedule.Id,
            wishes
                .Where(w => w.ScheduleId == schedule.Id)
                // Infosys Afviklinger::getSignupByType: game masters first, then by priority
                .OrderBy(w => w.Role == RunSeatRole.GameMaster ? 0 : 1)
                .ThenBy(w => w.Priority)
                .ThenBy(w => w.ParticipantId)
                .Select(w =>
                {
                    var p = participants[w.ParticipantId];
                    var isOnTeam = SignupRules.IsOnTeam(
                        activitySeats.Any(s => s.ParticipantId == p.Id && s.ScheduleId == schedule.Id),
                        w.Role,
                        activitySeats.Any(s => s.ParticipantId == p.Id && s.Role == RunSeatRole.Player),
                        activity.IsReplayable);
                    var isBusy = activity.IsTimeExclusive && !isOnTeam
                        && ParticipantRules.IsBusyBetween(ParticipantSpots.BusyPeriods(spots[p.Id]), schedule.StartTime, schedule.EndTime);
                    var note = mayReadNotes && !string.IsNullOrWhiteSpace(p.CommentNote) ? p.CommentNote.Trim() : null;

                    return new SignupPoolRowDto(
                        p.Id,
                        p.LegalName(),
                        w.Role,
                        w.Priority,
                        Karma: null,
                        ParticipantRules.AgeOn(p.BirthDate, today),
                        SignupRules.LanguageFlag(p.ActivityLanguages),
                        p.ActivityLanguages,
                        p.DesiredActivities,
                        SignupRules.DesiredActivitiesReached(
                            p.DesiredActivities,
                            wishes.Count(x => x.ParticipantId == p.Id),
                            playerSeats.GetValueOrDefault(p.Id)),
                        note,
                        isOnTeam,
                        isBusy,
                        p.IsActivityLocked,
                        IsDoubleBooked(p.Id, schedule.Id, schedule.StartTime, schedule.EndTime));
                })
                .ToList()))
            .ToList());
    }
}
