using System.ComponentModel.DataAnnotations.Schema;

namespace Fastasys.ApiService.Data.Entities;

/// <summary>
/// The signup's manual switch, a single row (schema approved on 2026-11-24). It replaces the WordPress plugin's "åben"
/// setting: a person decides when the signup opens. See SignupOpeningRules.
/// </summary>
[Table("signup_settings")]
public class SignupSettings
{
    public const int SingletonId = 1;
    public int Id { get; set; } = SingletonId;

    // Closed until someone switches it on
    public bool IsOpen { get; set; }
}
