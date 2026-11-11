using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Services;

namespace Fastasys.Tests;

// Pins the Infosys rules in Hold (needsGMs, needsGamers, canUseGamers, getNextHoldnummer),
// Pladser (setDeltagerPlads, getNextPladsnummer) and Lokaler (isOccupiedBetween).
public class RunRulesTests
{
    private static readonly DateTime Start = new(2027, 3, 24, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = Start.AddHours(4);

    [Theory]
    [InlineData(0, 1, true)]
    [InlineData(1, 1, false)]
    [InlineData(2, 1, false)]
    [InlineData(1, 2, true)]
    [InlineData(0, 0, false)]
    public void NeedsGameMasters_WhenFewerThanSpillederePerHold(int gameMasters, int perTeam, bool expected)
    {
        Assert.Equal(expected, RunRules.NeedsGameMasters(gameMasters, perTeam));
    }

    [Theory]
    [InlineData(0, 3, true)]
    [InlineData(2, 3, true)]
    [InlineData(3, 3, false)]
    [InlineData(4, 3, false)]
    [InlineData(0, 0, false)]
    public void NeedsPlayers_WhenFewerThanMinPlayers(int players, int minPlayers, bool expected)
    {
        Assert.Equal(expected, RunRules.NeedsPlayers(players, minPlayers));
    }

    [Theory]
    [InlineData(4, 5, true)]
    [InlineData(5, 5, false)]
    [InlineData(6, 5, false)]
    [InlineData(0, 0, false)]
    public void CanUsePlayers_WhenFewerThanMaxPlayers(int players, int maxPlayers, bool expected)
    {
        Assert.Equal(expected, RunRules.CanUsePlayers(players, maxPlayers));
    }

    [Fact]
    public void HasRoomFor_PlayerUsesMaxPlayers_GameMasterUsesGameMastersPerTeam()
    {
        var activity = new Activity { MinPlayersPerTeam = 3, MaxPlayersPerTeam = 5, GameMastersPerTeam = 1 };

        // A player can still be added above the minimum, up to the maximum
        Assert.True(RunRules.HasRoomFor(RunSeatRole.Player, 4, 1, activity));
        Assert.False(RunRules.HasRoomFor(RunSeatRole.Player, 5, 0, activity));

        // Game masters are capped by spilledere_per_hold, independent of the players
        Assert.True(RunRules.HasRoomFor(RunSeatRole.GameMaster, 5, 0, activity));
        Assert.False(RunRules.HasRoomFor(RunSeatRole.GameMaster, 0, 1, activity));
    }

    [Theory]
    [InlineData(new int[0], 1)]
    [InlineData(new[] { 1 }, 2)]
    [InlineData(new[] { 1, 2, 3 }, 4)]
    [InlineData(new[] { 1, 3 }, 4)]
    [InlineData(new[] { 3, 1 }, 4)]
    public void NextRunNumber_IsHighestPlusOne_GapsAreNotReused(int[] existing, int expected)
    {
        Assert.Equal(expected, RunRules.NextRunNumber(existing));
    }

    [Theory]
    [InlineData(new int[0], 1)]
    [InlineData(new[] { 1, 2, 3 }, 4)]
    [InlineData(new[] { 1, 3 }, 2)]
    [InlineData(new[] { 2, 3 }, 1)]
    [InlineData(new[] { 3, 1 }, 2)]
    public void NextSeatNumber_IsFirstFreeNumberFromOne(int[] existing, int expected)
    {
        Assert.Equal(expected, RunRules.NextSeatNumber(existing));
    }

    [Theory]
    // Same period
    [InlineData(0, 4, true)]
    // Starts inside, ends inside, contains, is contained
    [InlineData(1, 5, true)]
    [InlineData(-1, 1, true)]
    [InlineData(-1, 5, true)]
    [InlineData(1, 2, true)]
    // Back to back is not an overlap
    [InlineData(-2, 0, false)]
    [InlineData(4, 6, false)]
    // Far away
    [InlineData(10, 12, false)]
    public void IsRoomOccupied_ByOverlappingBooking(int bookingStartHours, int bookingEndHours, bool expected)
    {
        var bookings = new[] { new RoomBooking(1, Start.AddHours(bookingStartHours), Start.AddHours(bookingEndHours), true) };

        Assert.Equal(expected, RunRules.IsRoomOccupied(1, Start, End, true, bookings));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void IsRoomOccupied_RoomIsSharedOnlyWhenNeitherActivityFillsIt(bool newFillsRoom, bool bookedFillsRoom, bool expected)
    {
        var bookings = new[] { new RoomBooking(1, Start, End, bookedFillsRoom) };

        Assert.Equal(expected, RunRules.IsRoomOccupied(1, Start, End, newFillsRoom, bookings));
    }

    [Fact]
    public void IsRoomOccupied_IgnoresOtherRooms()
    {
        var bookings = new[] { new RoomBooking(2, Start, End, true) };

        Assert.False(RunRules.IsRoomOccupied(1, Start, End, true, bookings));
    }

    // Fastaval 2027 runs over the switch to summer time: on Sunday 28 March the clocks go from 02:00 to 03:00.
    // A run from Saturday 23:00 (UTC+1) to Sunday 04:00 (UTC+2) is four hours long, and the times are compared
    // as instants.
    private static readonly DateTime NightStart = new(2027, 3, 27, 22, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime NightEnd = new(2027, 3, 28, 2, 0, 0, DateTimeKind.Utc);

    [Theory]
    // Starts at 03:30 summer time, half an hour before the night run ends
    [InlineData("2027-03-28T03:30:00+02:00", "2027-03-28T06:00:00+02:00", true)]
    // Starts at 04:00 summer time, when the night run ends: back to back
    [InlineData("2027-03-28T04:00:00+02:00", "2027-03-28T06:00:00+02:00", false)]
    // Ends at 01:30 winter time, inside the night run
    [InlineData("2027-03-27T20:00:00+01:00", "2027-03-28T01:30:00+01:00", true)]
    // Ends at 23:00 winter time, when the night run starts: back to back
    [InlineData("2027-03-27T20:00:00+01:00", "2027-03-27T23:00:00+01:00", false)]
    // 01:59 winter time to 03:01 summer time is only two minutes, inside the night run
    [InlineData("2027-03-28T01:59:00+01:00", "2027-03-28T03:01:00+02:00", true)]
    public void IsRoomOccupied_AcrossTheSwitchToSummerTime(string start, string end, bool expected)
    {
        var bookings = new[] { new RoomBooking(1, NightStart, NightEnd, true) };

        Assert.Equal(expected, RunRules.IsRoomOccupied(1, Utc(start), Utc(end), true, bookings));
    }

    private static DateTime Utc(string value) =>
        DateTimeOffset.Parse(value, System.Globalization.CultureInfo.InvariantCulture).UtcDateTime;
}
