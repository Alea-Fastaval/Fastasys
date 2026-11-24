using System.Globalization;
using Fastasys.ApiService.Services;
using Microsoft.Extensions.Configuration;

namespace Fastasys.Tests;

// Pins when the signup is open, as agreed on 2026-11-24: a person must switch it on AND the opening day must have begun
// (Danish time). Infosys had no such check; the WordPress plugin had an "åben" setting.
public class SignupOpeningRulesTests
{
    private static DateTime Utc(string instant) =>
        DateTime.Parse(instant, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);

    private static DateOnly? Date(string? date) =>
        date == null ? null : DateOnly.Parse(date, CultureInfo.InvariantCulture);

    [Theory]
    // Opening day 1 February 2027; Denmark is UTC+1 then
    [InlineData(true, "2027-02-01", "2027-01-31T22:59:59Z", false)] // 23:59:59 the day before
    [InlineData(true, "2027-02-01", "2027-01-31T23:00:00Z", true)] // 00:00 on the opening day
    [InlineData(true, "2027-02-01", "2027-03-25T12:00:00Z", true)] // late signup stays open until switched off
    [InlineData(false, "2027-02-01", "2027-02-10T12:00:00Z", false)] // the day has come, but nobody switched it on
    [InlineData(true, null, "2027-02-10T12:00:00Z", false)] // switched on, but the opening day isn't set
    public void IsOpen_NeedsBothTheSwitchAndTheOpeningDay(bool switchedOn, string? start, string now, bool expected)
    {
        Assert.Equal(expected, SignupOpeningRules.IsOpen(switchedOn, Date(start), Utc(now)));
    }

    [Theory]
    // Junior opening 15 January 2027 at 10:00 Danish time, which is 09:00 UTC
    [InlineData(true, "2027-01-15T10:00", "2027-01-15T08:59:59Z", false)]
    [InlineData(true, "2027-01-15T10:00", "2027-01-15T09:00:00Z", true)]
    [InlineData(false, "2027-01-15T10:00", "2027-02-15T09:00:00Z", false)] // the signup itself is closed
    [InlineData(true, null, "2027-02-15T09:00:00Z", false)] // the 2027 date isn't known yet
    public void IsJuniorOpen_NeedsTheSignupOpenAndTheJuniorStartPassed(
        bool signupOpen,
        string? juniorStart,
        string now,
        bool expected
    )
    {
        var start = juniorStart == null ? (DateTime?)null : DateTime.Parse(juniorStart, CultureInfo.InvariantCulture);
        Assert.Equal(expected, SignupOpeningRules.IsJuniorOpen(signupOpen, start, Utc(now)));
    }

    [Theory]
    // Signup end 28 February 2027: that day is still on time (agreed 2026-11-18), late from 00:00 on 1 March
    [InlineData("2027-02-28T22:59:59Z", false)]
    [InlineData("2027-02-28T23:00:00Z", true)]
    public void IsLate_FromTheDayAfterTheSignupEnd(string now, bool expected)
    {
        Assert.Equal(expected, SignupOpeningRules.IsLate(new DateOnly(2027, 2, 28), Utc(now)));
    }

    [Fact]
    public void IsLate_NeverWhileTheSignupEndIsNotSet()
    {
        Assert.False(SignupOpeningRules.IsLate(null, Utc("2027-12-01T12:00:00Z")));
    }

    [Theory]
    [InlineData("2027-02-01T00:00", "2027-01-31T23:00:00Z")] // winter time, UTC+1
    [InlineData("2027-03-29T10:00", "2027-03-29T08:00:00Z")] // summer time, UTC+2
    [InlineData("2027-03-28T01:59", "2027-03-28T00:59:00Z")] // just before the clocks go forward
    [InlineData("2027-03-28T02:30", "2027-03-28T01:30:00Z")] // the skipped hour is read an hour later: 03:30
    [InlineData("2027-10-31T02:30", "2027-10-31T01:30:00Z")] // a time that exists twice is read as winter time
    public void CopenhagenToUtc_ReadsDanishWallClockTime(string wallClock, string expected)
    {
        var utc = SignupOpeningRules.CopenhagenToUtc(DateTime.Parse(wallClock, CultureInfo.InvariantCulture));

        Assert.Equal(Utc(expected), utc);
        Assert.Equal(DateTimeKind.Utc, utc.Kind);
    }

    [Fact]
    public void ConventionOptions_ReadTheSignupDatesFromTheConfiguration()
    {
        var options = Bind(
            new()
            {
                ["Convention:End"] = "2027-03-29",
                ["Convention:SignupStart"] = "2027-02-01",
                ["Convention:JuniorSignupStart"] = "2027-01-15T10:00",
                ["Convention:PayByDay"] = "2027-03-30",
            }
        );

        Assert.Equal(new DateOnly(2027, 3, 29), options.End);
        Assert.Equal(new DateOnly(2027, 2, 1), options.SignupStart);
        Assert.Equal(new DateTime(2027, 1, 15, 10, 0, 0), options.JuniorSignupStart);
        Assert.Equal(DateTimeKind.Unspecified, options.JuniorSignupStart?.Kind);
        Assert.Equal(new DateOnly(2027, 3, 30), options.PayByDay);
    }

    [Fact]
    public void ConventionOptions_AnEmptyValueIsNotSet()
    {
        var options = Bind(
            new()
            {
                ["Convention:End"] = "",
                ["Convention:SignupStart"] = "",
                ["Convention:JuniorSignupStart"] = "",
                ["Convention:PayByDay"] = "",
            }
        );

        Assert.Null(options.End);
        Assert.Null(options.SignupStart);
        Assert.Null(options.JuniorSignupStart);
        Assert.Null(options.PayByDay);
    }

    private static ConventionOptions Bind(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build()
            .GetSection(ConventionOptions.Section)
            .Get<ConventionOptions>()!;
}
