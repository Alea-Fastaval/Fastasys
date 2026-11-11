using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Fastasys.ApiService.Data.Entities;

/// <summary>
/// A participant category (Infosys: brugerkategorier). Infosys has no screen for them; they are maintained in
/// the database. The list was given by the user on 2026-11-11.
/// </summary>
[Table("participant_categories")]
public class ParticipantCategory
{
    // Infosys code assumes 1 = Deltager and 2 = Arrangør; the ids are kept
    public const int ParticipantId = 1;
    public const int OrganizerId = 2;

    public int Id { get; set; }

    [MaxLength(256)]
    public string Name { get; set; } = string.Empty;

    // Infosys: arrangoer
    public bool IsOrganizer { get; set; }

    [MaxLength(512)]
    public string? Description { get; set; }
}

/// <summary>
/// An organizer area (Infosys: organizer_categories, the participant's work_area). The list was given by the user
/// on 2026-11-11 without English names.
/// </summary>
[Table("organizer_areas")]
public class OrganizerArea
{
    // Infosys code assumes 1 = Scenarieforfatter and 2 = Brætspilsdesigner; the ids are kept
    public const int ScenarioAuthorId = 1;
    public const int BoardGameDesignerId = 2;

    public int Id { get; set; }

    public string NameDanish { get; set; } = string.Empty;

    public string? NameEnglish { get; set; }
}

/// <summary>A country (Infosys: countries).</summary>
[Table("countries")]
public class Country
{
    [Key]
    [MaxLength(2)]
    public string Code { get; set; } = string.Empty;

    public string NameDanish { get; set; } = string.Empty;

    public string NameEnglish { get; set; } = string.Empty;
}
