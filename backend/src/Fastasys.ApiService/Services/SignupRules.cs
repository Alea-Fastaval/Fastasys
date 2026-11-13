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
    // then 4 for the rest
    public static IReadOnlyList<int> PrioritiesInOrder(int count) =>
        Enumerable.Range(1, count).Select(i => Math.Min(i, LowestPriority)).ToList();
}
