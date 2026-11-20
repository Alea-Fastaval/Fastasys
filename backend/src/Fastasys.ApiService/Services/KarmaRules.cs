using Fastasys.ApiService.Data.Entities;

namespace Fastasys.ApiService.Services;

public readonly record struct KarmaSignup(int? Priority, RunSeatRole Role, bool IsTimeExclusive);

public readonly record struct KarmaAssignment(KarmaType KarmaType, RunSeatRole Role, bool IsTimeExclusive);

public static class KarmaRules
{
    private static readonly int[] FirstPriorityPoints = [-12, -20, -26, -29, -32, -34, -35, -36];

    public static int Calculate(
        IEnumerable<KarmaSignup> signups,
        IEnumerable<KarmaAssignment> assignments,
        ActivitySettings? settings = null
    )
    {
        settings ??= new ActivitySettings();
        var signupPoints = signups
            .Where(signup =>
                signup.IsTimeExclusive && (signup.Role == RunSeatRole.GameMaster || signup.Priority.HasValue)
            )
            .GroupBy(signup => signup.Role == RunSeatRole.GameMaster ? 0 : signup.Priority!.Value)
            .Sum(group => CalculateSignupPoints(group.Key, group.Count()));
        var assignmentPoints = assignments
            .Where(assignment => assignment.IsTimeExclusive && assignment.Role != RunSeatRole.GameMaster)
            .GroupBy(assignment => assignment.KarmaType)
            .Sum(group => GetAssignmentPoints(group.Key, settings) * Math.Min(group.Count(), 10));
        return signupPoints + assignmentPoints;
    }

    private static int CalculateSignupPoints(int priority, int count) =>
        priority switch
        {
            0 => -7,
            1 => FirstPriorityPoints[Math.Min(count, FirstPriorityPoints.Length) - 1],
            2 => -Math.Min(count, 3),
            3 => -1,
            _ => 0,
        };

    private static int GetAssignmentPoints(KarmaType type, ActivitySettings settings) =>
        type switch
        {
            KarmaType.Small => settings.SmallKarma,
            KarmaType.Medium => settings.MediumKarma,
            KarmaType.Big => settings.BigKarma,
            _ => 0,
        };
}
