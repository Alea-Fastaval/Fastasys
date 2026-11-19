using Fastasys.ApiService.Data.Entities;

namespace Fastasys.ApiService.Services;

/// <summary>A ranked preference, independent of whether the participant has an assigned seat.</summary>
public readonly record struct KarmaSignup(int? Priority, RunSeatRole Role, bool IsTimeExclusive);

/// <summary>One assigned seat. Multiple runs of the same activity count separately.</summary>
public readonly record struct KarmaAssignment(KarmaType KarmaType, RunSeatRole Role, bool IsTimeExclusive);

/// <summary>
/// A grouped signup contribution. Priority 0 includes game-master signups and legacy priority-0 player rows.
/// Points apply to the whole group, including any cap, rather than to each signup independently.
/// </summary>
public readonly record struct KarmaSignupContribution(int Priority, int Count, int Points);

/// <summary>A grouped player-seat contribution, including any cap.</summary>
public readonly record struct KarmaAssignmentContribution(KarmaType KarmaType, int Count, int Points);

/// <summary>
/// Counts of excluded inputs. Each input is counted in at most one category; time exclusivity is checked first.
/// A game-master signup does not need a player priority and is not treated as unranked.
/// </summary>
public sealed record KarmaExclusions(
    int NonTimeExclusiveSignups,
    int UnrankedPlayerSignups,
    int NonTimeExclusiveAssignments,
    int GameMasterAssignments,
    int UnknownSignupRoles = 0,
    int CancelledSignups = 0,
    int CancelledAssignments = 0
);

public sealed record KarmaResult(
    IReadOnlyList<KarmaSignupContribution> Signups,
    IReadOnlyList<KarmaAssignmentContribution> Assignments,
    KarmaExclusions Exclusions
)
{
    public int Total => Signups.Sum(s => s.Points) + Assignments.Sum(a => a.Points);
}

/// <summary>
/// Infosys Model::buildKarma rules at b6225c471951bddaf592a8c61fb1bbaea5a446d6,
/// with non-time-exclusive activities excluded from both inputs (Fastasys #26).
/// Input validation and database access belong to callers.
/// </summary>
public static class KarmaRules
{
    private static readonly int[] FirstPriorityPoints = [-12, -20, -26, -29, -32, -34, -35, -36];

    public static KarmaResult Calculate(IEnumerable<KarmaSignup> signups, IEnumerable<KarmaAssignment> assignments)
    {
        var priorityCounts = new Dictionary<int, int>();
        var assignmentCounts = new Dictionary<KarmaType, int>();
        var nonTimeExclusiveSignups = 0;
        var unrankedPlayerSignups = 0;
        var nonTimeExclusiveAssignments = 0;
        var gameMasterAssignments = 0;

        foreach (var signup in signups)
        {
            if (!signup.IsTimeExclusive)
            {
                nonTimeExclusiveSignups++;
                continue;
            }

            // Infosys maps all game-master preferences to potential bucket 0, regardless of priority.
            var priority = signup.Role == RunSeatRole.GameMaster ? 0 : signup.Priority;
            if (priority is null)
            {
                unrankedPlayerSignups++;
                continue;
            }

            priorityCounts[priority.Value] = priorityCounts.GetValueOrDefault(priority.Value) + 1;
        }

        foreach (var assignment in assignments)
        {
            if (!assignment.IsTimeExclusive)
            {
                nonTimeExclusiveAssignments++;
                continue;
            }

            if (assignment.Role == RunSeatRole.GameMaster)
            {
                gameMasterAssignments++;
                continue;
            }

            assignmentCounts[assignment.KarmaType] = assignmentCounts.GetValueOrDefault(assignment.KarmaType) + 1;
        }

        var signupContributions = priorityCounts.OrderBy(p => p.Key)
            .Select(p => new KarmaSignupContribution(p.Key, p.Value, SignupPoints(p.Key, p.Value)))
            .ToArray();
        var assignmentContributions = assignmentCounts.OrderBy(a => a.Key)
            .Select(a => new KarmaAssignmentContribution(a.Key, a.Value, AssignmentPoints(a.Key, a.Value)))
            .ToArray();

        return new KarmaResult(
            Array.AsReadOnly(signupContributions),
            Array.AsReadOnly(assignmentContributions),
            new KarmaExclusions(nonTimeExclusiveSignups, unrankedPlayerSignups, nonTimeExclusiveAssignments, gameMasterAssignments)
        );
    }

    private static int SignupPoints(int priority, int count) => priority switch
    {
        0 => -7,
        1 => FirstPriorityPoints[Math.Min(count, FirstPriorityPoints.Length) - 1],
        2 => -Math.Min(count, 3),
        3 => -1,
        _ => 0
    };

    // The runtime rules use Small (3) for +7; the old Infosys tests incorrectly use Medium (2).
    private static int AssignmentPoints(KarmaType type, int count) => type switch
    {
        KarmaType.Big => 10 * Math.Min(count, 10),
        KarmaType.Small => 7 * Math.Min(count, 10),
        _ => 0
    };
}
