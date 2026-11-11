using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Services;

namespace Fastasys.Tests;

// Pins the Infosys participant searches (IndexController::main, ParticipantModel::miniWildCardSearch and
// ParticipantModel::searchParticipants) with the fixes agreed on 2026-11-11.
public class ParticipantSearchTests
{
    private const int Year = 2027;

    private static readonly Country Danmark = new() { Code = "DK", NameDanish = "Danmark", NameEnglish = "Denmark" };
    private static readonly Country Sverige = new() { Code = "SE", NameDanish = "Sverige", NameEnglish = "Sweden" };

    private static Participant P(int id, string first, string last, Action<Participant>? setup = null)
    {
        var participant = new Participant { Id = id, FirstName = first, LastName = last, Country = Danmark };
        setup?.Invoke(participant);
        return participant;
    }

    private static readonly Participant[] People =
    [
        P(1, "Anna", "Hansen", p => { p.Email = "anna@example.invalid"; p.PhoneNumber = "11223344"; }),
        P(2, "Anna", "Jensen", p => { p.Nickname = "Annie"; p.AdminNote = "hemmelig"; }),
        P(3, "Bo", "Hansen", p => { p.YouthClub = "Testklubben"; p.CommentNote = "kommer med toget"; }),
        P(12, "Carl", "Dahl", p => { p.PhoneNumber = "99887766"; p.Country = Sverige; }),
    ];

    private static IReadOnlyList<int> Quick(string query) => ParticipantSearch.Quick(People, query, Year);

    [Fact]
    public void Quick_MatchesNamesEmailNicknameYouthClubAndSignupNotes_NotAdminNotes()
    {
        Assert.Equal([1], Quick("anna@"));
        Assert.Equal([2], Quick("annie"));
        Assert.Equal([3], Quick("testklub"));
        Assert.Equal([3], Quick("toget"));
        Assert.Empty(Quick("hemmelig"));
    }

    [Fact]
    public void Quick_PrefersParticipantsWhoseFullNameHasEveryTerm()
    {
        // Every word matches someone; only Anna Hansen's "first last" has both
        Assert.Equal([1], Quick("Anna Hansen"));
        // Nobody is called both, so everyone found by either word is shown
        Assert.Equal([1, 2, 3], Quick("Anna Bo"));
    }

    [Fact]
    public void Quick_ANumberMatchesAPhoneNumberOrAnId()
    {
        Assert.Equal([1, 12], Quick("12"));
        Assert.Equal([12], Quick("9988"));
    }

    [Fact]
    public void Quick_ABadgeMatchesItsParticipant()
    {
        Assert.Equal([12], Quick(ParticipantRules.Ean8ForParticipant(12, Year)));
    }

    [Fact]
    public void Quick_IgnoresEmptyTerms()
    {
        // Infosys searched for an empty term too, which matched everyone (fixed on 2026-11-11)
        Assert.Equal([3], Quick("toget  "));
        Assert.Equal([3], Quick(" toget"));
        Assert.Equal([3], Quick("Bo  Hansen"));
    }

    [Fact]
    public void Quick_IsLikeMySqlLike_CaseInsensitiveWithWildcards()
    {
        Assert.Equal([1, 2], Quick("ANNA"));
        Assert.Equal([1, 3], Quick("H_nsen"));
        Assert.Equal([1, 2], Quick("an%a"));
        // A backslash makes the wildcard literal
        Assert.Empty(Quick(@"an\%a"));
    }

    [Theory]
    [InlineData("12", 12)]
    [InlineData("12abc", 12)]
    [InlineData("12 Anna", 12)]
    [InlineData(" 3", 3)]
    [InlineData("0", null)]
    [InlineData("Anna", null)]
    [InlineData("77", null)]
    public void DirectId_IsIntvalOfTheWholeQueryWhenThatParticipantExists(string query, int? expected)
    {
        Assert.Equal(expected, ParticipantSearch.DirectId(query, new HashSet<int> { 1, 2, 3, 12 }, Year));
    }

    [Fact]
    public void DirectId_ReadsABadge()
    {
        var badge = ParticipantRules.Ean8ForParticipant(3, Year);
        Assert.Equal(3, ParticipantSearch.DirectId(badge, new HashSet<int> { 3 }, Year));
    }

    private static IReadOnlyList<int> Advanced(ParticipantCriteria criteria, SearchLogic logic = SearchLogic.And) =>
        ParticipantSearch.Advanced(People, criteria, logic, Year);

    [Fact]
    public void Advanced_NoFieldsFindsEveryone()
    {
        Assert.Equal([1, 2, 3, 12], Advanced(new ParticipantCriteria { FirstName = "  " }));
        Assert.Equal([1, 2, 3, 12], Advanced(new ParticipantCriteria(), SearchLogic.Or));
    }

    [Fact]
    public void Advanced_AndNeedsEveryField_OrNeedsOne()
    {
        var criteria = new ParticipantCriteria { FirstName = "Anna", LastName = "Hansen" };

        Assert.Equal([1], Advanced(criteria));
        Assert.Equal([1, 2, 3], Advanced(criteria, SearchLogic.Or));
    }

    [Fact]
    public void Advanced_OrTreatsCategoryAndBirthDateLikeTheOtherFields()
    {
        // Infosys always joined Fødselsdato and Kategori with "and", also with "Or" (fixed on 2026-11-11)
        var people = new[]
        {
            P(1, "A", "A", p => { p.CategoryId = 2; p.BirthDate = new DateOnly(2010, 1, 1); }),
            P(2, "B", "B", p => { p.CategoryId = 1; p.BirthDate = new DateOnly(1980, 1, 1); }),
            P(3, "C", "C", p => { p.CategoryId = 1; p.BirthDate = new DateOnly(2010, 1, 1); }),
        };
        var criteria = new ParticipantCriteria { CategoryId = 2, BirthDate = "1990-01-01", BirthDateRange = BirthDateRange.Before };

        Assert.Equal([1, 2], ParticipantSearch.Advanced(people, criteria, SearchLogic.Or, Year));
    }

    [Fact]
    public void Advanced_TextIsEqualWhenItIsANumberAndPartOfTheTextOtherwise()
    {
        var people = new[] { P(1, "A", "A", p => p.City = "8000"), P(2, "B", "B", p => p.City = "80001"), P(3, "C", "C", p => p.City = "Århus") };

        Assert.Equal([1], ParticipantSearch.Advanced(people, new ParticipantCriteria { City = "8000" }, SearchLogic.And, Year));
        Assert.Equal([3], ParticipantSearch.Advanced(people, new ParticipantCriteria { City = "rhu" }, SearchLogic.And, Year));
    }

    [Fact]
    public void Advanced_ZipCodePhoneAndAddressAreAlwaysPartOfTheText()
    {
        var people = new[] { P(1, "A", "A", p => { p.ZipCode = "8000"; p.AddressLine2 = "2. th"; }), P(2, "B", "B", p => p.ZipCode = "1800") };

        Assert.Equal([1, 2], ParticipantSearch.Advanced(people, new ParticipantCriteria { ZipCode = "800" }, SearchLogic.And, Year));
        Assert.Equal([1], ParticipantSearch.Advanced(people, new ParticipantCriteria { Address = "th" }, SearchLogic.And, Year));
    }

    [Fact]
    public void Advanced_IdReadsBadgesAndIntval()
    {
        Assert.Equal([12], Advanced(new ParticipantCriteria { Id = "12" }));
        Assert.Equal([12], Advanced(new ParticipantCriteria { Id = ParticipantRules.Ean8ForParticipant(12, Year) }));
        Assert.Empty(Advanced(new ParticipantCriteria { Id = "x" }));
    }

    [Fact]
    public void Advanced_BirthDateBeforeAfterAndSame()
    {
        var people = new[]
        {
            P(1, "A", "A", p => p.BirthDate = new DateOnly(1990, 5, 1)),
            P(2, "B", "B", p => p.BirthDate = new DateOnly(2000, 1, 1)),
            P(3, "C", "C", p => p.BirthDate = null),
        };
        IReadOnlyList<int> Search(string date, BirthDateRange range) =>
            ParticipantSearch.Advanced(people, new ParticipantCriteria { BirthDate = date, BirthDateRange = range }, SearchLogic.And, Year);

        // Infosys stores an unknown birth date as 0000-00-00, so it is "before" every date
        Assert.Equal([1, 3], Search("1995-01-01", BirthDateRange.Before));
        Assert.Equal([2], Search("1995-01-01", BirthDateRange.After));
        Assert.Equal([2], Search("2000-1-1", BirthDateRange.Same));
        // MySQL can't read it, so nobody matches
        Assert.Empty(Search("1. januar 2000", BirthDateRange.Same));
    }

    [Fact]
    public void Advanced_LandAndScenarieMatchTheNames()
    {
        // Infosys searched columns that no longer exist (fixed on 2026-11-11)
        var game = new Activity { Id = 5, Title = "Skygger over Testby", Type = ActivityType.Scenario };
        var people = new[] { P(1, "A", "A", p => p.Country = Sverige), P(2, "B", "B", p => { p.GameActivity = game; p.GameActivityId = 5; }), P(3, "C", "C") };

        Assert.Equal([1], ParticipantSearch.Advanced(people, new ParticipantCriteria { Country = "sver" }, SearchLogic.And, Year));
        Assert.Equal([2], ParticipantSearch.Advanced(people, new ParticipantCriteria { Game = "skygger" }, SearchLogic.And, Year));
    }

    [Fact]
    public void Advanced_EachTickedLanguageIsAField()
    {
        var people = new[]
        {
            P(1, "A", "A", p => p.ActivityLanguages = [ParticipantLanguage.Danish, ParticipantLanguage.English]),
            P(2, "B", "B", p => p.ActivityLanguages = [ParticipantLanguage.English]),
            P(3, "C", "C", p => p.ActivityLanguages = [ParticipantLanguage.Danish]),
        };
        var criteria = new ParticipantCriteria { ActivityLanguages = [ParticipantLanguage.Danish, ParticipantLanguage.English] };

        Assert.Equal([1], ParticipantSearch.Advanced(people, criteria, SearchLogic.And, Year));
        Assert.Equal([1, 2, 3], ParticipantSearch.Advanced(people, criteria, SearchLogic.Or, Year));
    }

    [Fact]
    public void Advanced_AmountPaidIsEqualForNumbersAndPartOfTheDigitsOtherwise()
    {
        var people = new[] { P(1, "A", "A", p => p.AmountPaid = 450), P(2, "B", "B", p => p.AmountPaid = 1450) };

        Assert.Equal([1], ParticipantSearch.Advanced(people, new ParticipantCriteria { AmountPaid = "450.0" }, SearchLogic.And, Year));
        Assert.Equal([1, 2], ParticipantSearch.Advanced(people, new ParticipantCriteria { AmountPaid = "45%" }, SearchLogic.And, Year));
    }

    [Fact]
    public void Advanced_CheckInTimeIsTheDanishTimeAsText()
    {
        var people = new[]
        {
            // 08:05 UTC is 10:05 in Copenhagen during Fastaval 2027 (summer time from 28 March)
            P(1, "A", "A", p => p.CheckedInAt = new DateTime(2027, 3, 31, 8, 5, 0, DateTimeKind.Utc)),
            P(2, "B", "B"),
        };

        Assert.Equal([1], ParticipantSearch.Advanced(people, new ParticipantCriteria { CheckedInAt = "2027-03-31 10:05" }, SearchLogic.And, Year));
        // Not checked in is 0000-00-00 00:00:00 in Infosys
        Assert.Equal([2], ParticipantSearch.Advanced(people, new ParticipantCriteria { CheckedInAt = "0000-00" }, SearchLogic.And, Year));
    }

    [Fact]
    public void Advanced_NotesAreTheSignupNotes()
    {
        Assert.Equal([3], Advanced(new ParticipantCriteria { Notes = "toget" }));
        Assert.Empty(Advanced(new ParticipantCriteria { Notes = "hemmelig" }));
    }

    [Fact]
    public void Advanced_YesNoAndNumberFieldsAreEqual()
    {
        var people = new[] { P(1, "A", "A", p => { p.IsNoShow = true; p.DesiredActivities = 3; }), P(2, "B", "B") };

        Assert.Equal([1], ParticipantSearch.Advanced(people, new ParticipantCriteria { IsNoShow = true }, SearchLogic.And, Year));
        Assert.Equal([2], ParticipantSearch.Advanced(people, new ParticipantCriteria { IsNoShow = false }, SearchLogic.And, Year));
        Assert.Equal([1], ParticipantSearch.Advanced(people, new ParticipantCriteria { DesiredActivities = 3 }, SearchLogic.And, Year));
    }

    [Theory]
    [InlineData(SearchCombination.New, new[] { 2, 3 })]
    [InlineData(SearchCombination.Intersection, new[] { 2 })]
    [InlineData(SearchCombination.Union, new[] { 1, 2, 3 })]
    [InlineData(SearchCombination.Difference, new[] { 3 })]
    public void Combine_UsesTheResultsThemselves(SearchCombination combination, int[] expected)
    {
        // Infosys got "Begræns" and "Træk fra" wrong with "Or" (fixed on 2026-11-11); the logic of the search
        // no longer matters for how it is combined
        Assert.Equal(expected, ParticipantSearch.Combine([1, 2], [2, 3], combination));
    }

    [Fact]
    public void Combine_WithoutAPreviousResultIsANewSearch()
    {
        Assert.Equal([2, 3], ParticipantSearch.Combine(null, [2, 3], SearchCombination.Intersection));
        // Like Infosys, a previous search that found nobody counts as no previous search
        Assert.Equal([2, 3], ParticipantSearch.Combine([], [2, 3], SearchCombination.Intersection));
    }

    [Theory]
    [InlineData("12", 12)]
    [InlineData(" +7x", 7)]
    [InlineData("-3", -3)]
    [InlineData("abc", 0)]
    [InlineData("", 0)]
    [InlineData("0045", 45)]
    public void PhpIntval(string text, long expected)
    {
        Assert.Equal(expected, ParticipantSearch.PhpIntval(text));
    }

    [Theory]
    [InlineData("12", true)]
    [InlineData(" 1.5 ", true)]
    [InlineData(".5", true)]
    [InlineData("1e3", true)]
    [InlineData("12a", false)]
    [InlineData("ja", false)]
    [InlineData("", false)]
    public void IsPhpNumeric(string text, bool expected)
    {
        Assert.Equal(expected, ParticipantSearch.IsPhpNumeric(text));
    }
}
