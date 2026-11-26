using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Fastasys.ApiService.Data.Entities;

/// <summary>
/// A category of hero force tasks (Infosys: gdscategories), e.g. "Forplejning". Infosys has no page for them; the 7
/// Infosys categories are seeded. The schema was approved on 2026-11-26. The scaffold HeroForceCategory stays until the
/// hero force itself is ported.
/// </summary>
[Table("hero_force_categories")]
public class HeroTaskCategory
{
    public const int MaxNameLength = 64;

    // Ids 1-7 are the seeded Infosys categories; new ones start at 8
    public const int FirstNewId = 8;

    public int Id { get; set; }

    // Infosys: name_da / name_en, varchar(64)
    [MaxLength(MaxNameLength)]
    public string NameDanish { get; set; } = string.Empty;

    [MaxLength(MaxNameLength)]
    public string NameEnglish { get; set; } = string.Empty;
}

/// <summary>
/// The parts of a day a participant can say they can help the hero force (Infosys: the hours of
/// deltagere_gdstilmeldinger.period): 7-12, 12-17 and 17-24 in Danish time.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<HeroPeriod>))]
public enum HeroPeriod
{
    // "7-12", Morgen
    Morning,

    // "12-17", Eftermiddag
    Afternoon,

    // "17-24", Aften
    Evening,
}

/// <summary>
/// When a participant can help the hero force (Infosys: deltagere_gdstilmeldinger, "Heltestyrken, Tilmeldinger"): a
/// day, a part of it and a task category, or any category (Infosys category 0, which the signup writes).
/// </summary>
[Table("participant_hero_availability")]
public class ParticipantHeroAvailability
{
    public int Id { get; set; }

    public int ParticipantId { get; set; }
    public virtual Participant Participant { get; set; } = null!;

    // Infosys: the date of period ("2027-03-24 7-12"), a day in Denmark
    public DateOnly Date { get; set; }

    // Infosys: the hours of period
    public HeroPeriod Period { get; set; }

    // Infosys: category_id; null is any category (Infosys 0)
    public int? CategoryId { get; set; }
    public virtual HeroTaskCategory? Category { get; set; }
}
