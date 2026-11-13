using System.Text.RegularExpressions;

namespace Fastasys.ApiService.Services;

/// <summary>
/// Business rules for participants (Infosys: entities/deltagere.php and framework/functions.php).
/// </summary>
public static partial class ParticipantRules
{
    // Infosys deltagere.pronouns: "none", "ask", or one or two pronoun codes
    public const string NoPronouns = "none";
    public const string AskForPronouns = "ask";
    public static readonly IReadOnlyList<string> PronounCodes = ["ns", "no", "fs", "fo", "ms", "mo"];

    // Infosys Deltagere::isBusyBetween: whether the period overlaps one of the participant's busy periods. Infosys's
    // busy periods are the schedules of their seats (designer rows included) on time-exclusive activities, those
    // schedules' multiblocks, and their hero force shifts; multiblocks and hero force are not ported yet.
    public static bool IsBusyBetween(IEnumerable<(DateTime Start, DateTime End)> busyPeriods, DateTime start, DateTime end) =>
        busyPeriods.Any(busy => RunRules.Overlaps(start, end, busy.Start, busy.End));

    // Infosys Deltagere::setInsertId gives a new participant the lowest id that isn't used. The Infonaut range
    // (900-929), the id reserved for one e-mail address and the 1-5000 search window were dropped on 2026-11-11.
    public static int LowestFreeId(IEnumerable<int> usedIds)
    {
        var used = usedIds.Where(id => id > 0).ToHashSet();
        var candidate = 1;
        while (used.Contains(candidate)) candidate++;
        return candidate;
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

    // The values the Infosys pronoun widget can save: "none", "ask", one code, or two different codes
    public static bool IsValidPronouns(string? pronouns)
    {
        if (pronouns is NoPronouns or AskForPronouns) return true;
        if (pronouns is null || (pronouns.Length != 2 && pronouns.Length != 4)) return false;

        var first = pronouns[..2];
        if (!PronounCodes.Contains(first)) return false;
        if (pronouns.Length == 2) return true;

        var second = pronouns[2..];
        return PronounCodes.Contains(second) && second != first;
    }

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
