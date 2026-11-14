using Fastasys.ApiService.Data.Entities;

namespace Fastasys.ApiService.Services;

/// <summary>
/// Business rules for signup wishes (Infosys: deltagere_tilmeldinger, entities/deltagere_tilmeldinger.php and the
/// participant model), with the fixes agreed on 2026-11-13.
/// </summary>
public static class SignupRules
{
    // The participant page offers priorities 1-4 for player wishes
    public const int LowestPriority = 4;

    // The participant page gives a game master wish priority 1 (the Infosys signup gives it 0)
    public const int GameMasterPriority = 1;

    // Infosys DeltagereTilmeldinger::setTilmelding refuses a game master wish for an activity without game masters
    // (Aktiviteter::needsSpilleder: spilledere_per_hold > 0). Infosys dropped it silently; now it is refused with an
    // error.
    public static bool CanWish(RunSeatRole role, int gameMastersPerTeam) =>
        role == RunSeatRole.Player || gameMastersPerTeam > 0;

    // A priority the participant page may save: 1-4 for a player. A game master wish keeps the priority it has (0 from
    // the signup, 1 from the participant page).
    public static bool IsValidPriority(RunSeatRole role, int priority) =>
        role == RunSeatRole.Player ? priority is >= 1 and <= LowestPriority : priority is >= 0 and <= LowestPriority;

    // Infosys DeltagereTilmeldinger::findConflictingSchedules: the wishes at the same time conflict when two of them
    // share a priority below 4 (4 may be used any number of times). Infosys counted schedules that only touch and
    // game master wishes too; agreed on 2026-11-13 to leave both out, so callers pass the player wishes on schedules
    // that overlap.
    public static bool HasPriorityConflict(IEnumerable<int> priorities) =>
        priorities.Where(p => p < LowestPriority).GroupBy(p => p).Any(g => g.Count() > 1);

    // Infosys ParticipantModel::updateSignupPriorities: the wishes in the order the user sorted them get 1, 2, 3 and
    // then 4 for the rest. The user decided on 2026-11-13 to keep that.
    public static IReadOnlyList<int> PrioritiesInOrder(int count) =>
        Enumerable.Range(1, count).Select(i => Math.Min(i, LowestPriority)).ToList();

    // --- The "Tilmeldinger" table on the activity page (Infosys visaktivitet.phtml, .afviklingsbox-signups) ---

    // A row is "on-team", and hidden, when the participant has a seat on a run of the schedule (any role), or when it
    // is a player wish, the participant plays on a run of the activity and the activity can't be replayed
    public static bool IsOnTeam(bool hasSeatOnSchedule, RunSeatRole wishRole, bool playsActivity, bool isReplayable) =>
        hasSeatOnSchedule || (wishRole == RunSeatRole.Player && playsActivity && !isReplayable);

    // The heart (Infosys ActivityModel::getDesiredActivityStats): null for a participant who didn't say how many
    // activities they want; otherwise whether their player seats reach the number exactly (a filled heart) or not (an
    // empty one). Fixed on 2026-11-14, as agreed: Infosys's query joined the participant's wishes on the activity with
    // their seats, so it counted each seat once per wish.
    public static bool? DesiredActivitiesReached(int desiredActivities, int playerSeats) =>
        desiredActivities > 0 ? desiredActivities == playerSeats : null;

    // The flag: Infosys shows dk or uk only when the participant's activity languages are exactly Danish or exactly
    // English (deltagere.sprog 'dansk' or 'engelsk'); any other set gets none
    public static string? LanguageFlag(IReadOnlyCollection<ParticipantLanguage> languages) =>
        languages.Count != 1 ? null : languages.Single() switch
        {
            ParticipantLanguage.Danish => "dk",
            ParticipantLanguage.English => "uk",
            _ => null
        };

    // Infosys ActivityModel::getDoubleBookings: a game master wish is double-booked when the participant has another
    // wish whose schedule overlaps it. This is Infosys's own test (the game master's schedule starts inside the other
    // one, covers it, or ends inside it); schedules that only touch don't overlap.
    public static bool OverlapsForDoubleBooking(DateTime start, DateTime end, DateTime otherStart, DateTime otherEnd) =>
        (start > otherStart && start < otherEnd)
        || (start <= otherStart && end >= otherEnd)
        || (end > otherStart && end < otherEnd);
}
