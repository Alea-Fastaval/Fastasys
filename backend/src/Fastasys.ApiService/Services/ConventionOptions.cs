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
}
