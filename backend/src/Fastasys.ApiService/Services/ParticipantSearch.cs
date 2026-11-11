using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Fastasys.ApiService.Data.Entities;

namespace Fastasys.ApiService.Services;

/// <summary>Infosys search box "Søgelogik": And / Or.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SearchLogic>))]
public enum SearchLogic
{
    And,
    Or
}

/// <summary>
/// Infosys search box: "Ny søgning", "Begræns søgning til tidligere resultat", "Tilføj søgning til tidligere
/// resultat", "Træk tidligere resultat fra denne søgning".
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<SearchCombination>))]
public enum SearchCombination
{
    New,
    Intersection,
    Union,
    Difference
}

/// <summary>Infosys search box "Fødselsdato": Før / Efter / Samme.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<BirthDateRange>))]
public enum BirthDateRange
{
    Before,
    After,
    Same
}

/// <summary>
/// The fields of the Infosys participant search box (ParticipantModel::searchParticipants). null or an empty text
/// means the field is not used. The fields for removed columns and for features not ported yet (entrance, food)
/// are left out.
/// </summary>
public sealed record ParticipantCriteria
{
    public string? Id { get; init; }
    public string? BirthDate { get; init; }
    public BirthDateRange BirthDateRange { get; init; }
    public int? CategoryId { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Nickname { get; init; }
    public string? Email { get; init; }
    public string? AmountPaid { get; init; }
    public string? Address { get; init; }
    public string? ZipCode { get; init; }
    public string? City { get; init; }
    public string? Country { get; init; }
    public string? PhoneNumber { get; init; }
    public bool? SmsSignup { get; init; }
    public IReadOnlyList<ParticipantLanguage>? ActivityLanguages { get; init; }
    public bool? SuperHero { get; init; }
    public int? DesiredHeroTasks { get; init; }
    public bool? SetupHelpMonday { get; init; }
    public bool? SetupHelpTuesday { get; init; }
    public bool? IsAuthorOrDesigner { get; init; }
    public int? DesiredActivities { get; init; }
    public bool? SuperGameMaster { get; init; }
    public bool? FinancialSupport { get; init; }
    public bool? IsNoShow { get; init; }
    public bool? IsAnnulled { get; init; }
    public string? YouthClub { get; init; }
    public int? OrganizerAreaId { get; init; }
    public string? Game { get; init; }
    public string? CheckedInAt { get; init; }
    public string? Notes { get; init; }
}

/// <summary>
/// The participant searches of Infosys: the quick search in the page header (IndexController::main and
/// ParticipantModel::miniWildCardSearch) and the search box (ParticipantModel::searchParticipants), and how a search
/// is combined with the previous result. The participants are searched in memory; the participants passed in must
/// have their category, country and game loaded.
/// </summary>
public static partial class ParticipantSearch
{
    // Infosys IndexController::main: a quick search whose PHP intval() isn't 0 opens the participant with that id
    // (or the id in that badge) when there is one
    public static int? DirectId(string query, IReadOnlySet<int> existingIds, int currentYear)
    {
        var number = PhpIntval(query);
        if (number == 0) return null;
        var id = ParticipantRules.IdFromBarcode(query, currentYear) ?? number;
        return id is > 0 and <= int.MaxValue && existingIds.Contains((int)id) ? (int)id : null;
    }

    // Infosys miniWildCardSearch. The query is split on spaces. A term whose intval() isn't 0 matches the mobile
    // number or the id (or the id in a badge); any other term matches first name, e-mail, last name, youth club,
    // the signup notes or nickname. If some participants' "first last" name contains every term, only they are
    // returned. Infosys searched for an empty term too when the query had two spaces in a row or a space at either
    // end, which matched everyone; empty terms are ignored (fixed on 2026-11-11).
    public static IReadOnlyList<int> Quick(IEnumerable<Participant> participants, string query, int currentYear)
    {
        var terms = query.Split(' ').Where(term => term.Length > 0).ToList();
        var matchers = terms.Select(term => QuickTermMatcher(term, currentYear)).ToList();

        var found = participants.Where(p => matchers.Count == 0 || matchers.Any(match => match(p))).OrderBy(p => p.Id).ToList();
        var fullMatches = found
            .Where(p => terms.All(term => (p.FirstName + " " + p.LastName).Contains(term, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        return (fullMatches.Count > 0 ? fullMatches : found).Select(p => p.Id).ToList();
    }

    private static Func<Participant, bool> QuickTermMatcher(string term, int currentYear)
    {
        var like = new Like("%" + term + "%");
        var number = PhpIntval(term);
        if (number != 0)
        {
            var id = ParticipantRules.IdFromBarcode(term, currentYear) ?? number;
            return p => like.Matches(p.PhoneNumber) || p.Id == id;
        }

        return p => like.Matches(p.FirstName) || like.Matches(p.Email) || like.Matches(p.LastName)
            || like.Matches(p.YouthClub) || SignupNotes(p).Any(like.Matches) || like.Matches(p.Nickname);
    }

    // Infosys searchParticipants. With "And" every used field must match, with "Or" one of them. Infosys mixed the
    // two up: Fødselsdato and Kategori were always joined with "and", which with "Or" made them an alternative of
    // their own (fixed on 2026-11-11 with the other "Or" bug). No used fields finds everyone, as in Infosys.
    public static IReadOnlyList<int> Advanced(IEnumerable<Participant> participants, ParticipantCriteria criteria, SearchLogic logic, int currentYear)
    {
        var predicates = Predicates(criteria, currentYear).ToList();
        return participants
            .Where(p => predicates.Count == 0 || (logic == SearchLogic.And ? predicates.All(match => match(p)) : predicates.Any(match => match(p))))
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToList();
    }

    // How a search is combined with the previous result. Infosys added the previous result to the query in a way
    // that only worked with "And"; with "Or", "Begræns" gave the union and "Træk fra" almost everyone. The result
    // sets are now combined directly (fixed on 2026-11-11). Like Infosys, a previous search that found nobody is
    // treated as no previous search.
    public static IReadOnlyList<int> Combine(IReadOnlyCollection<int>? previous, IReadOnlyList<int> current, SearchCombination combination)
    {
        if (previous == null || previous.Count == 0 || combination == SearchCombination.New) return current;

        var before = previous.ToHashSet();
        var result = combination switch
        {
            SearchCombination.Intersection => current.Where(before.Contains),
            SearchCombination.Union => current.Union(previous),
            SearchCombination.Difference => current.Where(id => !before.Contains(id)),
            _ => current,
        };
        return result.Distinct().Order().ToList();
    }

    // Infosys deltager_note holds the signup notes as JSON; the notes are separate columns here
    public static IEnumerable<string> SignupNotes(Participant p) =>
        new[] { p.CommentNote, p.HeroForceNote, p.JuniorContact, p.JuniorComment, p.SleepingNote }.OfType<string>();

    // Infosys shows a check-in time as stored, in Danish time; "not checked in" is 0000-00-00 00:00:00
    public static string CheckInText(DateTime? checkedInAt) =>
        checkedInAt == null
            ? "0000-00-00 00:00:00"
            : TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.SpecifyKind(checkedInAt.Value, DateTimeKind.Utc), "Europe/Copenhagen")
                .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    private static IEnumerable<Func<Participant, bool>> Predicates(ParticipantCriteria c, int currentYear)
    {
        // ID: the id in a badge, or intval() of the text
        if (IsUsed(c.Id))
        {
            var id = ParticipantRules.IdFromBarcode(c.Id, currentYear) ?? PhpIntval(c.Id!);
            yield return p => p.Id == id;
        }

        // DATE(birthdate) compared with DATE(value). A value MySQL can't read as a date matches nobody. An unknown
        // birth date is 0000-00-00 in Infosys, so it comes before every date.
        if (IsUsed(c.BirthDate))
        {
            var date = ParseMySqlDate(c.BirthDate!.Trim());
            yield return p =>
            {
                if (date == null) return false;
                var birthDate = p.BirthDate ?? DateOnly.MinValue;
                return c.BirthDateRange switch
                {
                    BirthDateRange.Before => birthDate <= date,
                    BirthDateRange.After => birthDate >= date,
                    _ => birthDate == date,
                };
            };
        }

        if (c.CategoryId != null) yield return p => p.CategoryId == c.CategoryId;

        foreach (var predicate in TextFields(c)) yield return predicate;

        if (IsUsed(c.AmountPaid))
        {
            var value = c.AmountPaid!.Trim();
            if (IsPhpNumeric(value))
            {
                var number = double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
                yield return p => p.AmountPaid == number;
            }
            else
            {
                var like = new Like("%" + value + "%");
                yield return p => like.Matches(p.AmountPaid.ToString(CultureInfo.InvariantCulture));
            }
        }

        // Adresse: either address line, always as a part of the text
        if (IsUsed(c.Address))
        {
            var like = new Like("%" + c.Address!.Trim() + "%");
            yield return p => like.Matches(p.AddressLine1) || like.Matches(p.AddressLine2);
        }

        // Postnr and Mobil: always as a part of the text
        if (IsUsed(c.ZipCode))
        {
            var like = new Like("%" + c.ZipCode!.Trim() + "%");
            yield return p => like.Matches(p.ZipCode);
        }

        if (IsUsed(c.PhoneNumber))
        {
            var like = new Like("%" + c.PhoneNumber!.Trim() + "%");
            yield return p => like.Matches(p.PhoneNumber);
        }

        // Sprog: one condition per ticked language
        foreach (var language in c.ActivityLanguages ?? [])
        {
            yield return p => p.ActivityLanguages.Contains(language);
        }

        if (c.SmsSignup != null) yield return p => p.SmsSignup == c.SmsSignup;
        if (c.SuperHero != null) yield return p => p.SuperHero == c.SuperHero;
        if (c.SetupHelpMonday != null) yield return p => p.SetupHelpMonday == c.SetupHelpMonday;
        if (c.SetupHelpTuesday != null) yield return p => p.SetupHelpTuesday == c.SetupHelpTuesday;
        if (c.IsAuthorOrDesigner != null) yield return p => p.IsAuthorOrDesigner == c.IsAuthorOrDesigner;
        if (c.SuperGameMaster != null) yield return p => p.SuperGameMaster == c.SuperGameMaster;
        if (c.FinancialSupport != null) yield return p => p.FinancialSupport == c.FinancialSupport;
        if (c.IsNoShow != null) yield return p => p.IsNoShow == c.IsNoShow;
        if (c.IsAnnulled != null) yield return p => p.IsAnnulled == c.IsAnnulled;
        if (c.DesiredHeroTasks != null) yield return p => p.DesiredHeroTasks == c.DesiredHeroTasks;
        if (c.DesiredActivities != null) yield return p => p.DesiredActivities == c.DesiredActivities;
        if (c.OrganizerAreaId != null) yield return p => p.OrganizerAreaId == c.OrganizerAreaId;
    }

    // The text fields Infosys compares with "=" when the value is a number and with LIKE '%value%' otherwise
    private static IEnumerable<Func<Participant, bool>> TextFields(ParticipantCriteria c)
    {
        var fields = new (string? Value, Func<Participant, IEnumerable<string>> Texts)[]
        {
            (c.FirstName, p => [p.FirstName]),
            (c.LastName, p => [p.LastName]),
            (c.Nickname, p => [p.Nickname]),
            (c.Email, p => [p.Email]),
            (c.City, p => [p.City]),
            (c.YouthClub, p => [p.YouthClub]),
            // Infosys searched the old land and scenarie columns, which no longer exist, so these two fields broke
            // the search; they match the country's and the game's name (fixed on 2026-11-11)
            (c.Country, p => [p.Country?.NameDanish ?? string.Empty]),
            (c.Game, p => [p.GameActivity?.Title ?? string.Empty]),
            (c.CheckedInAt, p => [CheckInText(p.CheckedInAt)]),
            (c.Notes, SignupNotes),
        };

        foreach (var (value, texts) in fields)
        {
            if (!IsUsed(value)) continue;
            var trimmed = value!.Trim();
            if (IsPhpNumeric(trimmed))
            {
                // MySQL compares the strings without case and without trailing spaces
                yield return p => texts(p).Any(text => string.Equals(text.TrimEnd(), trimmed.TrimEnd(), StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                var like = new Like("%" + trimmed + "%");
                yield return p => texts(p).Any(like.Matches);
            }
        }
    }

    // Infosys skips a field whose value trim()s to ''
    private static bool IsUsed(string? value) => value != null && value.Trim().Length > 0;

    // PHP intval(): optional whitespace and sign, then the leading digits; 0 when there are none
    public static long PhpIntval(string text)
    {
        var match = LeadingInteger().Match(text);
        if (!match.Success) return 0;
        return long.TryParse(match.Value.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)
            ? number
            : (match.Value.Contains('-') ? long.MinValue : long.MaxValue);
    }

    // PHP 8 is_numeric(): an integer or decimal number, optionally with an exponent, surrounded by whitespace
    public static bool IsPhpNumeric(string text) => PhpNumber().IsMatch(text);

    // MySQL DATE(): yyyy-mm-dd (also with / or . and without leading zeros) or yyyymmdd; null when it isn't a date
    public static DateOnly? ParseMySqlDate(string text)
    {
        var match = MySqlDate().Match(text);
        if (!match.Success) return null;
        int year = int.Parse(match.Groups["y"].Value), month = int.Parse(match.Groups["m"].Value), day = int.Parse(match.Groups["d"].Value);
        return month is >= 1 and <= 12 && year >= 1 && day >= 1 && day <= DateTime.DaysInMonth(year, month)
            ? new DateOnly(year, month, day)
            : null;
    }

    [GeneratedRegex(@"^\s*[+-]?\d+")]
    private static partial Regex LeadingInteger();

    [GeneratedRegex(@"^\s*[+-]?(\d+(\.\d*)?|\.\d+)([eE][+-]?\d+)?\s*$")]
    private static partial Regex PhpNumber();

    [GeneratedRegex(@"^(?<y>\d{4})[-/.](?<m>\d{1,2})[-/.](?<d>\d{1,2})$|^(?<y>\d{4})(?<m>\d{2})(?<d>\d{2})$")]
    private static partial Regex MySqlDate();

    // MySQL LIKE with the case-insensitive collation Infosys uses: % is any text, _ is one character and a
    // backslash makes the next character literal
    public sealed class Like
    {
        private readonly Regex _regex;

        public Like(string pattern)
        {
            var regex = new StringBuilder("^");
            for (var i = 0; i < pattern.Length; i++)
            {
                var ch = pattern[i];
                if (ch == '\\' && i + 1 < pattern.Length) regex.Append(Regex.Escape(pattern[++i].ToString()));
                else if (ch == '%') regex.Append(".*");
                else if (ch == '_') regex.Append('.');
                else regex.Append(Regex.Escape(ch.ToString()));
            }

            _regex = new Regex(regex.Append('$').ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);
        }

        public bool Matches(string? text) => text != null && _regex.IsMatch(text);
    }
}
