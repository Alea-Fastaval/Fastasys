namespace Fastasys.ApiService.Services;

/// <summary>
/// When the public signup is open. Infosys had no such check: the WordPress plugin (fv-tilmelding) had an "åben"
/// setting per signup page. Fastasys replaces it with a switch in signup_settings plus the convention dates
/// (ConventionOptions), as agreed on 2026-11-24.
/// </summary>
public static class SignupOpeningRules
{
    private static readonly TimeZoneInfo Copenhagen = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");

    // Open only when a person has switched it on AND the opening day has begun (00:00 Danish time); both must be true.
    // Closed while the opening day isn't set. There is no automatic end: after the signup end it stays open as a late
    // signup until it is switched off.
    public static bool IsOpen(bool isSwitchedOn, DateOnly? signupStart, DateTime nowUtc) =>
        isSwitchedOn && signupStart is DateOnly start && DanishDate(nowUtc) >= start;

    // Infosys left the junior page out before con.juniorsignup. The junior signup is part of the signup, so it also
    // needs the signup to be open. Closed while its start isn't set (the 2027 date isn't known yet).
    public static bool IsJuniorOpen(bool isSignupOpen, DateTime? juniorSignupStart, DateTime nowUtc) =>
        isSignupOpen && juniorSignupStart is DateTime start && nowUtc >= CopenhagenToUtc(start);

    // Late from 00:00 Danish time the day after the signup end, the same rule as the participant page's red "Tilmeldt"
    public static bool IsLate(DateOnly? signupEnd, DateTime nowUtc) => ParticipantRules.IsLateSignup(nowUtc, signupEnd);

    // A Danish wall-clock time as a UTC instant. A time that doesn't exist because the clocks are put forward
    // (02:00-03:00 on the last Sunday of March) is moved an hour later, as agreed on 2026-11-11 for the datetime
    // forms (Infosys's strtotime does the same). A time that exists twice in October is read as winter time.
    public static DateTime CopenhagenToUtc(DateTime wallClock)
    {
        var local = DateTime.SpecifyKind(wallClock, DateTimeKind.Unspecified);
        if (Copenhagen.IsInvalidTime(local))
            local = local.AddHours(1);
        return TimeZoneInfo.ConvertTimeToUtc(local, Copenhagen);
    }

    private static DateOnly DanishDate(DateTime utc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Copenhagen));
}
