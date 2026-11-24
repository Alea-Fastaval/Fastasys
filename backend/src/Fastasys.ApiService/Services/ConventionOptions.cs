namespace Fastasys.ApiService.Services;

/// <summary>
/// The convention's dates (Infosys config.ini con.*), from the "Convention" section of the configuration: appsettings.json,
/// or for example the environment variable <c>Convention__Start=2027-03-24</c>. A date that isn't set is null, and the
/// rules that need it don't apply.
/// </summary>
public class ConventionOptions
{
    public const string Section = "Convention";

    // Infosys con.start: the convention's first day. Ages are measured on this day, as in Infosys.
    public DateOnly? Start { get; set; }

    // Infosys con.end: the convention's last day
    public DateOnly? End { get; set; }

    // Infosys con.signupstart: the day the signup opens, from 00:00 Danish time. Infosys only used it for statistics; the
    // WordPress plugin's "åben" setting opened the signup. Agreed on 2026-11-24: the signup is open only when this day
    // has begun AND a person has switched it on (see SignupOpeningRules).
    public DateOnly? SignupStart { get; set; }

    // Infosys con.signupend: the last day of the ordinary signup. A signup later than this day (Danish time) is a late
    // signup. Infosys already counted the day itself as late; agreed on 2026-11-18 that it is still on time.
    public DateOnly? SignupEnd { get; set; }

    // Infosys con.juniorsignup: when Fastaval Junior's signup opens, as Danish wall-clock time, e.g.
    // "2027-01-15T10:00". It opens separately from the main signup (the user, 2026-11-24).
    public DateTime? JuniorSignupStart { get; set; }

    // Infosys con.paybyday: the last day of payment
    public DateOnly? PayByDay { get; set; }
}
