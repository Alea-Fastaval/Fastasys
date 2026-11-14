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

    // --- The activity page's Tilmeldinger tables ---

    [Theory]
    [InlineData(true, RunSeatRole.GameMaster, false, false, true)]
    [InlineData(false, RunSeatRole.Player, true, false, true)]
    [InlineData(false, RunSeatRole.GameMaster, true, false, false)]
    [InlineData(false, RunSeatRole.Player, true, true, false)]
    [InlineData(false, RunSeatRole.Player, false, false, false)]
    public void IsOnTeam_SeatedOnTheScheduleOrPlayingANonReplayableActivity(bool seatedHere, RunSeatRole role, bool playsActivity, bool replayable, bool expected)
    {
        Assert.Equal(expected, SignupRules.IsOnTeam(seatedHere, role, playsActivity, replayable));
    }

    [Theory]
    [InlineData(0, 0, null)]
    [InlineData(2, 2, true)]
    [InlineData(3, 1, false)]
    [InlineData(1, 2, false)]
    public void DesiredActivitiesReached_ComparesTheWantedNumberWithTheSeats(int desired, int seats, bool? expected)
    {
        Assert.Equal(expected, SignupRules.DesiredActivitiesReached(desired, seats));
    }

    [Fact]
    public void LanguageFlag_OnlyForExactlyDanishOrExactlyEnglish()
    {
        Assert.Equal("dk", SignupRules.LanguageFlag([ParticipantLanguage.Danish]));
        Assert.Equal("uk", SignupRules.LanguageFlag([ParticipantLanguage.English]));
        Assert.Null(SignupRules.LanguageFlag([ParticipantLanguage.Scandinavian]));
        Assert.Null(SignupRules.LanguageFlag([ParticipantLanguage.Danish, ParticipantLanguage.English]));
        Assert.Null(SignupRules.LanguageFlag([]));
    }

    // The game master's schedule is 10-14
    [Theory]
    [InlineData(12, 16, true)]
    [InlineData(8, 12, true)]
    [InlineData(11, 13, true)]
    [InlineData(8, 16, true)]
    [InlineData(10, 14, true)]
    [InlineData(14, 18, false)]
    [InlineData(6, 10, false)]
    public void OverlapsForDoubleBooking_InfosysTest(int otherStart, int otherEnd, bool expected)
    {
        var day = new DateTime(2027, 3, 24, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(expected, SignupRules.OverlapsForDoubleBooking(day.AddHours(10), day.AddHours(14), day.AddHours(otherStart), day.AddHours(otherEnd)));
    }
}
