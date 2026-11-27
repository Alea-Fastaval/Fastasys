using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fastasys.ApiService.Controllers;

// One of the participant's signup wishes with what the pages need to show and check it
internal sealed record SignupRow(
    int ActivityId,
    string Title,
    string TitleEnglish,
    bool IsTimeExclusive,
    int GameMastersPerTeam,
    int ScheduleId,
    DateTime Start,
    DateTime End,
    RunSeatRole Role,
    int Priority)
{
    public ParticipantActivitySignupDto ToDto() =>
        new(ActivityId, Title, TitleEnglish, ScheduleId, Start, End, Priority, Role);
}

internal static class ParticipantSignupRows
{
    // Infosys DeltagereTilmeldinger::getDeltagerTilmeldinger: the participant's wishes by the schedule's start. Infosys
    // leaves the order of wishes at the same time open; here it is by schedule, then priority, then role. That is
    // done in memory, since the roles are stored as text and would sort differently in the database.
    public static async Task<List<SignupRow>> LoadAsync(InfosysDbContext db, int participantId) =>
        (await db.ScheduleSignups
            .AsNoTracking()
            .Where(s => s.ParticipantId == participantId)
            .Select(s => new SignupRow(
                s.Schedule.ActivityId,
                s.Schedule.Activity.Title,
                s.Schedule.Activity.TitleEnglish,
                s.Schedule.Activity.IsTimeExclusive,
                s.Schedule.Activity.GameMastersPerTeam,
                s.ScheduleId,
                s.Schedule.StartTime,
                s.Schedule.EndTime,
                s.Role,
                s.Priority))
            .ToListAsync())
        .OrderBy(s => s.Start)
        .ThenBy(s => s.ScheduleId)
        .ThenBy(s => s.Priority)
        .ThenBy(s => s.Role)
        .ToList();
}
