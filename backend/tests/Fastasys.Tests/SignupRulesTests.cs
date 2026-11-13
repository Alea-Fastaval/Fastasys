using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Services;

namespace Fastasys.Tests;

// Pins the signup wish rules from Infosys entities/deltagere_tilmeldinger.php and the participant model, with the
// fixes agreed on 2026-11-13
public class SignupRulesTests
{
    [Theory]
    [InlineData(RunSeatRole.Player, 0, true)]
    [InlineData(RunSeatRole.GameMaster, 1, true)]
    [InlineData(RunSeatRole.GameMaster, 0, false)]
    public void CanWish_OnlyAGameMasterWishNeedsAnActivityWithGameMasters(RunSeatRole role, int gameMastersPerTeam, bool expected)
    {
        Assert.Equal(expected, SignupRules.CanWish(role, gameMastersPerTeam));
    }

    [Theory]
    [InlineData(RunSeatRole.Player, 1, true)]
    [InlineData(RunSeatRole.Player, 4, true)]
    [InlineData(RunSeatRole.Player, 0, false)]
    [InlineData(RunSeatRole.Player, 5, false)]
    // The Infosys signup gives game master wishes priority 0, the participant page 1
    [InlineData(RunSeatRole.GameMaster, 0, true)]
    [InlineData(RunSeatRole.GameMaster, 1, true)]
    [InlineData(RunSeatRole.GameMaster, -1, false)]
    public void IsValidPriority_PlayersOneToFour(RunSeatRole role, int priority, bool expected)
    {
        Assert.Equal(expected, SignupRules.IsValidPriority(role, priority));
    }

    [Theory]
    [InlineData(new[] { 1, 2, 3 }, false)]
    [InlineData(new[] { 1, 1 }, true)]
    [InlineData(new[] { 3, 2, 3 }, true)]
    // Priority 4 may be used any number of times (Infosys: $weight < 4)
    [InlineData(new[] { 4, 4, 4 }, false)]
    [InlineData(new[] { 1 }, false)]
    [InlineData(new int[0], false)]
    public void HasPriorityConflict_WhenTwoWishesShareAPriorityBelowFour(int[] priorities, bool expected)
    {
        Assert.Equal(expected, SignupRules.HasPriorityConflict(priorities));
    }

    [Fact]
    public void PrioritiesInOrder_GivesOneTwoThreeAndThenFour()
    {
        Assert.Equal([1, 2, 3, 4, 4, 4], SignupRules.PrioritiesInOrder(6));
        Assert.Equal([1, 2], SignupRules.PrioritiesInOrder(2));
    }
}
