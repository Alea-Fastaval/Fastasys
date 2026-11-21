using System.Text.RegularExpressions;
using Fastasys.ApiService.Data.Entities;

namespace Fastasys.ApiService.Services;

/// <summary>
/// Business rules for participants (Infosys: entities/deltagere.php and framework/functions.php).
/// </summary>
public static partial class ParticipantRules
{
    // Infosys deltagere.pronouns: "none", "ask", or one or two pronoun codes.
    // "none" is the column's default: the participant hasn't given their pronouns yet. Infosys showed it as "Ingen" and
    // let staff choose it. That was dropped on 2026-11-21 after a PR review, because nobody has no pronouns: it is
    // shown blank and can't be chosen. Participants created by staff still start with it. When the signup is ported,
    // pronouns are obligatory there, so everyone who signs up has picked some (the user, 2026-11-21).
    public const string PronounsNotGiven = "none";
    public const string AskForPronouns = "ask";
    public static readonly IReadOnlyList<string> PronounCodes = ["ns", "no", "fs", "fo", "ms", "mo"];

    // Infosys Deltagere's "alder": the full years from the birth date to today. Infosys's empty birth date
    // (0000-00-00) gave the years since 1970; here an unknown birth date has no age.
    public static int? AgeOn(DateOnly? birthDate, DateOnly today)
    {
        if (birthDate is not DateOnly born) return null;
        var age = today.Year - born.Year;
        return (today.Month, today.Day).CompareTo((born.Month, born.Day)) < 0 ? age - 1 : age;
    }

    // Infosys visdeltager (youngster.css) and the check-in message "Deltageren er under 18 år.": the participant is
    // under 18 on the convention's first day (Deltagere::getAge(con.start) < 18). The marking is new in Fastasys:
    // Infosys's youngster.css doesn't exist, so its participant page showed nothing (agreed 2026-11-18).
    // Not marked when the convention start isn't set. Not marked either when the birth date is unknown: here an
    // unknown birth date has no age; in Infosys an empty one gave -1 (under 18) and 0000-00-00 a very high age.
    public static bool IsUnder18(DateOnly? birthDate, DateOnly? conventionStart) =>
        conventionStart is DateOnly start && AgeOn(birthDate, start) is int age && age < 18;

    // Infosys visdeltager marks "Tilmeldt" in red for a late signup. Agreed on 2026-11-18, two fixes:
    // - Infosys compared the participant's creation time but showed the latest signup (signed_up, which the signup sets
    //   on every resubmit). Now the date shown decides: the latest signup.
    // - Infosys read con.signupend as 00:00 at the start of that day, so a signup on the end date was already late. Now
    //   the end date itself is on time, as the signup e-mail tells participants: late is from 00:00 the day after,
    //   Danish time.
    // Nobody is late while the signup end isn't set.
    public static bool IsLateSignup(DateTime signedUpAt, DateOnly? signupEnd) =>
        signupEnd is DateOnly end && DanishDate(signedUpAt) > end;

    // The date in Denmark at a UTC instant
    private static DateOnly DanishDate(DateTime utc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.SpecifyKind(utc, DateTimeKind.Utc), "Europe/Copenhagen"));

    // Infosys Deltagere::isBusyBetween: whether the period overlaps one of the participant's busy periods. Infosys's
    // busy periods are the schedules of their seats (designer rows included) on time-exclusive activities, those
    // schedules' multiblocks, and their hero force shifts; multiblocks and hero force are not ported yet.
    public static bool IsBusyBetween(IEnumerable<(DateTime Start, DateTime End)> busyPeriods, DateTime start, DateTime end) =>
        busyPeriods.Any(busy => RunRules.Overlaps(start, end, busy.Start, busy.End));

    // Infosys Deltagere::setInsertId gives a new participant the lowest id that isn't used. The Infonaut range
    // (900-929), the id reserved for one e-mail address and the 1-5000 search window were dropped on 2026-11-11.
    // The search is bounded: it checks 1 up to the highest used id, and if all of those are taken, the next one is free.
    public static int LowestFreeId(IEnumerable<int> usedIds)
    {
        var used = usedIds.Where(id => id > 0).ToHashSet();
        var highest = used.Count == 0 ? 0 : used.Max();
        for (var candidate = 1; candidate <= highest; candidate++)
        {
            if (!used.Contains(candidate)) return candidate;
        }

        return checked(highest + 1);
    }

    // Infosys Deltagere::normalizePhone, run on every save: trims the number and removes a Danish prefix in front
    // of 8 digits. Infosys looks for "+dk" or "0045"; "+45" was added on 2026-11-11 (it was a bug that a number
    // like +4512345678 kept its prefix). The pattern isn't anchored, as in Infosys.
    public static string NormalizePhone(string? phone) =>
        DanishPrefix().Replace((phone ?? string.Empty).Trim(), "$2");

    [GeneratedRegex(@"(\+45|\+dk|0045)(\d{8})")]
    private static partial Regex DanishPrefix();

    // Infosys Deltagere::normalizeEmail
    public static string NormalizeEmail(string? email) => (email ?? string.Empty).Trim();

    // The values the pronoun picker can save: "ask", one code, or two different codes. Infosys's widget could also save
    // "none" ("Ingen"); not any more (2026-11-21, see PronounsNotGiven).
    public static bool IsValidPronouns(string? pronouns)
    {
        if (pronouns is AskForPronouns) return true;
        if (pronouns is null || (pronouns.Length != 2 && pronouns.Length != 4)) return false;

        var first = pronouns[..2];
        if (!PronounCodes.Contains(first)) return false;
        if (pronouns.Length == 2) return true;

        var second = pronouns[2..];
        return PronounCodes.Contains(second) && second != first;
    }

    // Infosys Deltagere::getPronoun: "Spørg mig", "Hun" or "Hun/Hende", in the given language. Blank when the pronouns
    // aren't given yet (Infosys: "Ingen"; changed 2026-11-21). The frontend's pronounText (participant-format.ts) does
    // the same for the participant page. An unknown code is shown as it is.
    public static string PronounText(string pronouns, PreferredLanguage language)
    {
        var words = language == PreferredLanguage.English ? EnglishPronouns : DanishPronouns;
        string Word(string code) => words.GetValueOrDefault(code, code);

        // "none" is four letters too, so it is checked first, as in Infosys
        if (pronouns == PronounsNotGiven) return string.Empty;
        if (pronouns == AskForPronouns || pronouns.Length != 4) return Word(pronouns);
        return Word(pronouns[..2]) + "/" + Word(pronouns[2..]);
    }

    private static readonly Dictionary<string, string> DanishPronouns = new()
    {
        [AskForPronouns] = "Spørg mig",
        ["ns"] = "De",
        ["no"] = "Dem",
        ["fs"] = "Hun",
        ["fo"] = "Hende",
        ["ms"] = "Han",
        ["mo"] = "Ham"
    };

    private static readonly Dictionary<string, string> EnglishPronouns = new()
    {
        [AskForPronouns] = "Ask me",
        ["ns"] = "They",
        ["no"] = "Them",
        ["fs"] = "She",
        ["fo"] = "Her",
        ["ms"] = "He",
        ["mo"] = "Him"
    };

    // Infosys EANToNumber: a scanned badge is turned back into a participant id. A valid EAN-8 whose first two
    // digits are the current year (two digits) holds the id in its last five digits; a valid EAN-8 from another
    // year gives its first seven digits as a number. A valid EAN-13 gives its first twelve digits as a number.
    // Anything else (wrong length, not digits, wrong check digit) is not a barcode.
    public static long? IdFromBarcode(string? input, int currentTwoDigitYear)
    {
        if (input is null || !input.All(char.IsAsciiDigit)) return null;

        if (input.Length == 13)
        {
            var digits = input[..12];
            return NumberToEan13(digits) == input ? long.Parse(digits) : null;
        }

        if (input.Length == 8)
        {
            var digits = input[..7];
            if (NumberToEan8(digits) != input) return null;
            return int.Parse(digits[..2]) == currentTwoDigitYear % 100
                ? int.Parse(digits[2..])
                : int.Parse(digits);
        }

        return null;
    }

    // Infosys Deltagere::getEan8Number: two-digit year, five-digit id and a check digit
    public static string Ean8ForParticipant(int participantId, int year) =>
        NumberToEan8((year % 100).ToString("00") + participantId.ToString("00000"));

    // Infosys numberToEAN8 for a 7-digit string: weights 3,1,3,1,3,1,3
    private static string NumberToEan8(string sevenDigits)
    {
        var sum = 0;
        for (var i = 0; i < 7; i++)
        {
            var digit = sevenDigits[i] - '0';
            sum += i % 2 == 0 ? 3 * digit : digit;
        }

        return sevenDigits + CheckDigit(sum);
    }

    // Infosys numberToEAN13 for a 12-digit string. Its loop starts at index 1, so the first digit is never counted
    // (index 1, 3, ..., 11 weigh 3, index 2, 4, ..., 10 weigh 1). That is kept so the same codes are accepted.
    private static string NumberToEan13(string twelveDigits)
    {
        var sum = 0;
        for (var i = 1; i < 12; i++)
        {
            var digit = twelveDigits[i] - '0';
            sum += i % 2 == 1 ? 3 * digit : digit;
        }

        return twelveDigits + CheckDigit(sum);
    }

    private static int CheckDigit(int sum)
    {
        var control = 10 - sum % 10;
        return control < 10 ? control : 0;
    }
}
