using System.Linq.Expressions;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fastasys.ApiService.Controllers;

// One of the participant's spots (Infosys Pladser or SpecialSpot): a seat on a run, or a designer row without a run
internal sealed record ParticipantSpot(
    int ActivityId,
    string Title,
    string TitleEnglish,
    bool IsTimeExclusive,
    DateTime Start,
    DateTime End,
    ParticipantActivityRole Role,
    int? RunId,
    int? RunNumber,
    string? RoomName,
    string? RoomLocation);

// Infosys Deltagere::getPladser, used by the Aktiviteter box, the seat editor and isBusyBetween
internal static class ParticipantSpots
{
    // The participant's seats on runs, by start time. For a Brætspilsdesigner (work_area 2) Infosys adds a "designer"
    // row for every schedule of their own game (game_id; getDesignerDuties) and a "spiller" row for every schedule of
    // the activities with automatic signup for designers (getAutoSpots). It does that only for work_area 2:
    // getAutoSpots also knows authors and other designer areas, but getPladser never asks for them.
    // TODO: the user decided (2026-11-13) to keep this legacy behaviour for now and add the automatic signups for
    // authors (Infosys work_area 1, 54-56, auto_signup_category 'author') and the other designer areas (17-19) later.
    // Infosys never uses the 'all' and 'organizer' categories either. The organizer area ids were renumbered in
    // Fastasys, so map 54-56 and 17-19 to the new ids first.
    // The rows are then sorted by start time. PHP's uasort is stable, so at equal times the seats come first, then
    // the designer rows, then the automatic ones.
    public static async Task<List<ParticipantSpot>> LoadAsync(InfosysDbContext db, Participant p)
    {
        var seats = await db.RunSeats
            .AsNoTracking()
            .Where(s => s.ParticipantId == p.Id)
            .OrderBy(s => s.Run.Schedule.StartTime)
            .ThenBy(s => s.RunId)
            .Select(s => new ParticipantSpot(
                s.Run.Schedule.ActivityId,
                s.Run.Schedule.Activity.Title,
                s.Run.Schedule.Activity.TitleEnglish,
                s.Run.Schedule.Activity.IsTimeExclusive,
                s.Run.Schedule.StartTime,
                s.Run.Schedule.EndTime,
                s.Role == RunSeatRole.Player ? ParticipantActivityRole.Player : ParticipantActivityRole.GameMaster,
                s.RunId,
                s.Run.Number,
                s.Run.Room.Name,
                s.Run.Room.Location))
            .ToListAsync();

        if (p.OrganizerAreaId != OrganizerArea.BoardGameDesignerId)
        {
            return seats;
        }

        var ownGame = p.GameActivityId is int gameId
            ? await SchedulesAsSpotsAsync(db, s => s.ActivityId == gameId, ParticipantActivityRole.Designer)
            : [];
        var automatic = await SchedulesAsSpotsAsync(
            db, s => s.Activity.AutoSignupCategory == AutoSignupCategory.Designer, ParticipantActivityRole.Player);

        // OrderBy is stable, like uasort
        return seats.Concat(ownGame).Concat(automatic).OrderBy(a => a.Start).ToList();
    }

    // Infosys Deltagere::isBusyBetween's busy periods: the spots on time-exclusive activities
    public static IEnumerable<(DateTime Start, DateTime End)> BusyPeriods(IEnumerable<ParticipantSpot> spots) =>
        spots.Where(s => s.IsTimeExclusive).Select(s => (s.Start, s.End));

    // Spots for schedules the participant has no seat on (Infosys SpecialSpot): no run, and the schedule's meeting room
    private static async Task<List<ParticipantSpot>> SchedulesAsSpotsAsync(
        InfosysDbContext db, Expression<Func<Schedule, bool>> filter, ParticipantActivityRole role) =>
        await db.Schedules
            .AsNoTracking()
            .Where(filter)
            .OrderBy(s => s.Id)
            .Select(s => new ParticipantSpot(
                s.ActivityId,
                s.Activity.Title,
                s.Activity.TitleEnglish,
                s.Activity.IsTimeExclusive,
                s.StartTime,
                s.EndTime,
                role,
                null,
                null,
                s.Room != null ? s.Room.Name : null,
                s.Room != null ? s.Room.Location : null))
            .ToListAsync();
}
