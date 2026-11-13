using Fastasys.ApiService.Services;

namespace Fastasys.Tests;

// Pins the Infosys participant rules from entities/deltagere.php and framework/functions.php, with the changes
// agreed on 2026-11-11.
public class ParticipantRulesTests
{
    [Theory]
    [InlineData(new int[0], 1)]
    [InlineData(new[] { 1, 2, 3 }, 4)]
    [InlineData(new[] { 1, 3, 4 }, 2)]
    [InlineData(new[] { 2, 3 }, 1)]
    [InlineData(new[] { 5, 1, 3, 2 }, 4)]
    [InlineData(new[] { 1, 1, 2 }, 3)]
    public void LowestFreeId_ReusesTheLowestGap(int[] used, int expected)
    {
        Assert.Equal(expected, ParticipantRules.LowestFreeId(used));
    }

    [Fact]
    public void LowestFreeId_HasNoInfonautRangeAndNoUpperLimit()
    {
        // Infosys gave ids 900-929 only to Infonauts and searched 1-5000; both were dropped on 2026-11-11
        var upTo899 = Enumerable.Range(1, 899);
        Assert.Equal(900, ParticipantRules.LowestFreeId(upTo899));
        Assert.Equal(5001, ParticipantRules.LowestFreeId(Enumerable.Range(1, 5000)));
    }

    [Theory]
    [InlineData("004512345678", "12345678")]
    [InlineData("+dk12345678", "12345678")]
    [InlineData("  12345678  ", "12345678")]
    [InlineData("tlf 004512345678", "tlf 12345678")]
    [InlineData("+46701234567", "+46701234567")]
    [InlineData("0045 1234 5678", "0045 1234 5678")]
    [InlineData("1234", "1234")]
    [InlineData(null, "")]
    public void NormalizePhone_RemovesADanishPrefixInFrontOfEightDigits(string? input, string expected)
    {
        Assert.Equal(expected, ParticipantRules.NormalizePhone(input));
    }

    [Fact]
    public void NormalizePhone_AlsoRemovesPlus45()
    {
        // Infosys left "+45" in place (its pattern looks for "+dk"); fixed on 2026-11-11
        Assert.Equal("12345678", ParticipantRules.NormalizePhone("+4512345678"));
    }

    [Fact]
    public void NormalizeEmail_Trims()
    {
        Assert.Equal("someone@example.invalid", ParticipantRules.NormalizeEmail("  someone@example.invalid "));
    }

    [Theory]
    [InlineData("none", true)]
    [InlineData("ask", true)]
    [InlineData("ns", true)]
    [InlineData("nsno", true)]
    [InlineData("fsmo", true)]
    [InlineData("nsns", false)]
    [InlineData("xx", false)]
    [InlineData("nsxx", false)]
    [InlineData("n", false)]
    [InlineData("nsnofs", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidPronouns_AcceptsWhatThePronounWidgetCanSave(string? pronouns, bool expected)
    {
        Assert.Equal(expected, ParticipantRules.IsValidPronouns(pronouns));
    }

    [Fact]
    public void IdFromBarcode_ReadsTheIdFromAnEan8WithTheCurrentYear()
    {
        var code = ParticipantRules.Ean8ForParticipant(1234, 2027);

        // Weights 3,1,3,1,3,1,3 on 2701234 give 35, so the check digit is 5
        Assert.Equal("27012345", code);
        Assert.Equal(1234, ParticipantRules.IdFromBarcode(code, 27));
    }

    [Fact]
    public void IdFromBarcode_GivesSevenDigitsForAnEan8FromAnotherYear()
    {
        // Infosys EAN8ToNumber: when the year doesn't match, the first seven digits are the "id"
        Assert.Equal(2701234, ParticipantRules.IdFromBarcode("27012345", 26));
    }

    [Theory]
    [InlineData("27012344")]
    [InlineData("2701234")]
    [InlineData("270123450")]
    [InlineData("2701234x")]
    [InlineData("")]
    [InlineData(null)]
    public void IdFromBarcode_IgnoresWhatIsNotABarcode(string? input)
    {
        Assert.Null(ParticipantRules.IdFromBarcode(input, 27));
    }

    [Fact]
    public void IdFromBarcode_ReadsAnEan13()
    {
        // Infosys numberToEAN13 doesn't count the first digit: odd positions 1-11 weigh 3 (2 and 4 give 18),
        // even positions 2-10 weigh 1 (1 and 3 give 4), so 000000001234 gets check digit 8
        Assert.Equal(1234, ParticipantRules.IdFromBarcode("0000000012348", 27));
        Assert.Null(ParticipantRules.IdFromBarcode("0000000012345", 27));
    }

    // Infosys Deltagere::isBusyBetween: a period is busy when it starts inside a busy period, ends inside one, or
    // covers one. Busy 10:00-14:00.
    [Theory]
    [InlineData(10, 14, true)]
    [InlineData(11, 12, true)]
    [InlineData(8, 11, true)]
    [InlineData(13, 16, true)]
    [InlineData(9, 15, true)]
    [InlineData(8, 10, false)]
    [InlineData(14, 16, false)]
    [InlineData(15, 18, false)]
    public void IsBusyBetween_WhenThePeriodOverlapsABusyOne(int startHour, int endHour, bool expected)
    {
        var day = new DateTime(2027, 3, 24, 0, 0, 0, DateTimeKind.Utc);
        var busy = new[] { (day.AddHours(10), day.AddHours(14)) };

        Assert.Equal(expected, ParticipantRules.IsBusyBetween(busy, day.AddHours(startHour), day.AddHours(endHour)));
    }

    [Fact]
    public void IsBusyBetween_IsNeverTrueWithoutBusyPeriods()
    {
        var day = new DateTime(2027, 3, 24, 0, 0, 0, DateTimeKind.Utc);

        Assert.False(ParticipantRules.IsBusyBetween([], day, day.AddHours(4)));
    }
}
