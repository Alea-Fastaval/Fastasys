using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Services;

namespace Fastasys.Tests;

// Compatibility target: Infosys Model::buildKarma, rather than its older Medium/Small test rules.
public class KarmaRulesTests
{
    [Fact]
    public void EmptyInputs_HaveAnExplicitZeroTotal()
    {
        var result = KarmaRules.Calculate([], []);

        Assert.Equal(0, result.Total);
        Assert.Empty(result.Signups);
        Assert.Empty(result.Assignments);
        Assert.Equal(new KarmaExclusions(0, 0, 0, 0), result.Exclusions);
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(1, 1, -12)]
    [InlineData(1, 2, -20)]
    [InlineData(1, 3, -26)]
    [InlineData(1, 4, -29)]
    [InlineData(1, 5, -32)]
    [InlineData(1, 6, -34)]
    [InlineData(1, 7, -35)]
    [InlineData(1, 8, -36)]
    [InlineData(1, 9, -36)]
    [InlineData(1, 20, -36)]
    [InlineData(2, 0, 0)]
    [InlineData(2, 1, -1)]
    [InlineData(2, 2, -2)]
    [InlineData(2, 3, -3)]
    [InlineData(2, 4, -3)]
    [InlineData(3, 0, 0)]
    [InlineData(3, 1, -1)]
    [InlineData(3, 2, -1)]
    [InlineData(4, 1, 0)]
    [InlineData(4, 5, 0)]
    // Other legacy priority buckets have no matching rule.
    [InlineData(10, 1, 0)]
    public void PlayerPreferences_UseTheLegacyCountThresholds(int priority, int count, int expected)
    {
        var result = KarmaRules.Calculate(PlayerSignups(priority, count), []);

        Assert.Equal(expected, result.Total);
        if (count > 0)
        {
            Assert.Equal(new KarmaSignupContribution(priority, count, expected), Assert.Single(result.Signups));
        }
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, -7)]
    [InlineData(2, -7)]
    [InlineData(10, -7)]
    public void GameMasterSignups_ContributeMinusSevenOnce(int count, int expected)
    {
        var signups = Enumerable.Repeat(new KarmaSignup(null, RunSeatRole.GameMaster, true), count);

        var result = KarmaRules.Calculate(signups, []);

        Assert.Equal(expected, result.Total);
        Assert.Equal(0, result.Exclusions.UnrankedPlayerSignups);
        if (count > 0)
        {
            Assert.Equal(new KarmaSignupContribution(0, count, -7), Assert.Single(result.Signups));
        }
    }

    [Fact]
    public void GameMasterPriority_IsIgnored_AndLegacyPlayerPriorityZeroSharesTheSameRule()
    {
        var result = KarmaRules.Calculate(
            [new(1, RunSeatRole.GameMaster, true), new(4, RunSeatRole.GameMaster, true), new(0, RunSeatRole.Player, true)],
            []);

        Assert.Equal(-7, result.Total);
        Assert.Equal(new KarmaSignupContribution(0, 3, -7), Assert.Single(result.Signups));
    }

    [Fact]
    public void LegacyPlayerPriorityZero_ContributesWithoutAGameMasterSignup()
    {
        Assert.Equal(-7, KarmaRules.Calculate(PlayerSignups(0, 1), []).Total);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 10, 7)]
    [InlineData(2, 20, 14)]
    [InlineData(3, 30, 21)]
    [InlineData(4, 40, 28)]
    [InlineData(5, 50, 35)]
    [InlineData(6, 60, 42)]
    [InlineData(7, 70, 49)]
    [InlineData(8, 80, 56)]
    [InlineData(9, 90, 63)]
    [InlineData(10, 100, 70)]
    [InlineData(11, 100, 70)]
    [InlineData(20, 100, 70)]
    public void PlayerAssignments_UseBigAndSmallRulesWithTenSeatCaps(int count, int bigPoints, int smallPoints)
    {
        var big = KarmaRules.Calculate([], PlayerAssignments(KarmaType.Big, count));
        var small = KarmaRules.Calculate([], PlayerAssignments(KarmaType.Small, count));

        Assert.Equal(bigPoints, big.Total);
        Assert.Equal(smallPoints, small.Total);
        if (count > 0)
        {
            Assert.Equal(new KarmaAssignmentContribution(KarmaType.Big, count, bigPoints), Assert.Single(big.Assignments));
            Assert.Equal(new KarmaAssignmentContribution(KarmaType.Small, count, smallPoints), Assert.Single(small.Assignments));
        }
    }

    [Theory]
    [InlineData(KarmaType.None)]
    [InlineData(KarmaType.Medium)]
    public void NoneAndMediumAssignments_HaveZeroPointsButRemainVisibleInTheBreakdown(KarmaType type)
    {
        var result = KarmaRules.Calculate([], PlayerAssignments(type, 2));

        Assert.Equal(0, result.Total);
        Assert.Equal(new KarmaAssignmentContribution(type, 2, 0), Assert.Single(result.Assignments));
    }

    [Fact]
    public void AssignedGameMasters_DoNotContribute_OrGrantTheSignupBonus()
    {
        var result = KarmaRules.Calculate([], [
            new(KarmaType.Big, RunSeatRole.GameMaster, true),
            new(KarmaType.Small, RunSeatRole.GameMaster, true)
        ]);

        Assert.Equal(0, result.Total);
        Assert.Empty(result.Signups);
        Assert.Empty(result.Assignments);
        Assert.Equal(new KarmaExclusions(0, 0, 0, 2), result.Exclusions);
    }

    [Fact]
    public void MixedInputs_AddIndependentRuleContributions()
    {
        var signups = PlayerSignups(1, 2)
            .Concat(PlayerSignups(2, 4))
            .Concat(PlayerSignups(3, 2))
            .Concat(PlayerSignups(4, 3))
            .Append(new KarmaSignup(1, RunSeatRole.GameMaster, true));
        var assignments = PlayerAssignments(KarmaType.Big, 3)
            .Concat(PlayerAssignments(KarmaType.Small, 2))
            .Concat(PlayerAssignments(KarmaType.Medium, 1))
            .Concat(PlayerAssignments(KarmaType.None, 1))
            .Append(new KarmaAssignment(KarmaType.Big, RunSeatRole.GameMaster, true));

        var result = KarmaRules.Calculate(signups, assignments);

        // Preferences: -7 -20 -3 -1. Assigned players: +30 +14. Total: 13.
        Assert.Equal(13, result.Total);
        Assert.Equal(new KarmaSignupContribution[] {
            new(0, 1, -7), new(1, 2, -20), new(2, 4, -3), new(3, 2, -1), new(4, 3, 0)
        }, result.Signups);
        Assert.Equal(new KarmaAssignmentContribution[] {
            new(KarmaType.None, 1, 0), new(KarmaType.Big, 3, 30),
            new(KarmaType.Medium, 1, 0), new(KarmaType.Small, 2, 14)
        }, result.Assignments);
        Assert.Equal(new KarmaExclusions(0, 0, 0, 1), result.Exclusions);
    }

    [Fact]
    public void Assignment_DoesNotReplaceThePreferenceContribution()
    {
        var signups = PlayerSignups(1, 1);

        Assert.Equal(-12, KarmaRules.Calculate(signups, []).Total);
        Assert.Equal(-2, KarmaRules.Calculate(signups, PlayerAssignments(KarmaType.Big, 1)).Total);
    }

    [Fact]
    public void SignupContribution_DoesNotDependOnAssignedActivityKarmaType()
    {
        // A signup has no karma-type input: even an activity with type None contributes by priority.
        var result = KarmaRules.Calculate(PlayerSignups(1, 1), PlayerAssignments(KarmaType.None, 1));

        Assert.Equal(-12, result.Total);
        Assert.Equal(new KarmaSignupContribution(1, 1, -12), Assert.Single(result.Signups));
    }

    [Fact]
    public void UnrankedPlayerSignups_AreExcludedWithoutInventingAPriority()
    {
        var result = KarmaRules.Calculate([new(null, RunSeatRole.Player, true)], []);

        Assert.Equal(0, result.Total);
        Assert.Empty(result.Signups);
        Assert.Equal(new KarmaExclusions(0, 1, 0, 0), result.Exclusions);
    }

    [Theory]
    [InlineData(1, RunSeatRole.Player)]
    [InlineData(2, RunSeatRole.Player)]
    [InlineData(3, RunSeatRole.Player)]
    [InlineData(4, RunSeatRole.Player)]
    [InlineData(null, RunSeatRole.Player)]
    [InlineData(1, RunSeatRole.GameMaster)]
    [InlineData(null, RunSeatRole.GameMaster)]
    public void NonTimeExclusiveSignups_DoNotChangeExistingKarma(int? priority, RunSeatRole role)
    {
        var signups = PlayerSignups(1, 1);
        var assignments = PlayerAssignments(KarmaType.Big, 1);
        var baseline = KarmaRules.Calculate(signups, assignments);

        var result = KarmaRules.Calculate(signups.Append(new KarmaSignup(priority, role, false)), assignments);

        Assert.Equal(-2, result.Total);
        Assert.Equal(baseline.Signups, result.Signups);
        Assert.Equal(baseline.Assignments, result.Assignments);
        Assert.Equal(new KarmaExclusions(1, 0, 0, 0), result.Exclusions);
    }

    [Theory]
    [InlineData(KarmaType.Big, RunSeatRole.Player)]
    [InlineData(KarmaType.Small, RunSeatRole.Player)]
    [InlineData(KarmaType.Medium, RunSeatRole.Player)]
    [InlineData(KarmaType.None, RunSeatRole.Player)]
    [InlineData(KarmaType.Big, RunSeatRole.GameMaster)]
    public void NonTimeExclusiveAssignments_DoNotChangeExistingKarma(KarmaType type, RunSeatRole role)
    {
        var signups = PlayerSignups(1, 1);
        var assignments = PlayerAssignments(KarmaType.Big, 1);
        var baseline = KarmaRules.Calculate(signups, assignments);

        var result = KarmaRules.Calculate(signups, assignments.Append(new KarmaAssignment(type, role, false)));

        Assert.Equal(-2, result.Total);
        Assert.Equal(baseline.Signups, result.Signups);
        Assert.Equal(baseline.Assignments, result.Assignments);
        Assert.Equal(new KarmaExclusions(0, 0, 1, 0), result.Exclusions);
    }

    [Fact]
    public void DropInFirstPriorityAndSeat_AreExcludedTogether_WhileTimeExclusiveInputsStillCount()
    {
        var signups = PlayerSignups(1, 1);
        var assignments = PlayerAssignments(KarmaType.Big, 1);
        var dropInSignup = new KarmaSignup(1, RunSeatRole.Player, false);
        var dropInSeat = new KarmaAssignment(KarmaType.Small, RunSeatRole.Player, false);

        var baseline = KarmaRules.Calculate(signups, assignments);
        var withDropIn = KarmaRules.Calculate(signups.Append(dropInSignup), assignments.Append(dropInSeat));
        var withExclusive = KarmaRules.Calculate(
            signups.Append(dropInSignup with { IsTimeExclusive = true }),
            assignments.Append(dropInSeat with { IsTimeExclusive = true }));
        var afterRemoval = KarmaRules.Calculate(signups, assignments);

        Assert.Equal(-2, baseline.Total);
        Assert.Equal(-2, withDropIn.Total);
        Assert.Equal(new KarmaExclusions(1, 0, 1, 0), withDropIn.Exclusions);
        // Two first priorities: -20; one Big and one Small seat: +10 +7.
        Assert.Equal(-3, withExclusive.Total);
        Assert.Equal(-2, afterRemoval.Total);
    }

    [Fact]
    public void OnlyExcludedInputs_HaveZeroKarma()
    {
        var result = KarmaRules.Calculate([
            new(1, RunSeatRole.Player, false),
            new(null, RunSeatRole.GameMaster, false),
            new(null, RunSeatRole.Player, true)
        ], [
            new(KarmaType.Big, RunSeatRole.Player, false),
            new(KarmaType.Small, RunSeatRole.GameMaster, true)
        ]);

        Assert.Equal(0, result.Total);
        Assert.Empty(result.Signups);
        Assert.Empty(result.Assignments);
        Assert.Equal(new KarmaExclusions(2, 1, 1, 1), result.Exclusions);
    }

    private static IEnumerable<KarmaSignup> PlayerSignups(int priority, int count) =>
        Enumerable.Repeat(new KarmaSignup(priority, RunSeatRole.Player, true), count);

    private static IEnumerable<KarmaAssignment> PlayerAssignments(KarmaType type, int count) =>
        Enumerable.Repeat(new KarmaAssignment(type, RunSeatRole.Player, true), count);
}
