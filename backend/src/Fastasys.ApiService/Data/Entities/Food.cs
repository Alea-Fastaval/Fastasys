using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Fastasys.ApiService.Data.Entities;

/// <summary>
/// A food type (Infosys: mad), e.g. "Aftensmad - Pizza", with the times it is served (meal times). The schema was
/// approved on 2026-11-25.
/// </summary>
[Table("food_types")]
public class FoodType
{
    public const int MaxNameLength = 100;

    public int Id { get; set; }

    // Infosys: kategori, varchar(100), unique
    [MaxLength(MaxNameLength)]
    public string NameDanish { get; set; } = string.Empty;

    // Infosys: title_en, varchar(100)
    [MaxLength(MaxNameLength)]
    public string NameEnglish { get; set; } = string.Empty;

    // Infosys: pris, whole DKK. 0 is a free meal.
    public int Price { get; set; }

    // Infosys: hidden ("Skjult"). A hidden food type isn't offered in the signup, and a participant keeps it when they
    // change their signup (Deltagere::removeOrderedFood); staff give it out.
    public bool IsHidden { get; set; }

    public virtual ICollection<MealTime> MealTimes { get; set; } = new List<MealTime>();
}

/// <summary>A time a food type is served (Infosys: madtider). A food type is served at most once at a time.</summary>
[Table("meal_times")]
public class MealTime
{
    public const int MaxDescriptionLength = 128;

    public int Id { get; set; }

    public int FoodTypeId { get; set; }
    public virtual FoodType FoodType { get; set; } = null!;

    // Infosys: dato. A UTC instant; the hour in Copenhagen says whether it is breakfast (before 10), dinner (16 or
    // later) or lunch (Madtider::isBreakfast/isDinner/isLunch).
    public DateTime Time { get; set; }

    // Infosys: description_da / description_en, varchar(128)
    [MaxLength(MaxDescriptionLength)]
    public string DescriptionDanish { get; set; } = string.Empty;

    [MaxLength(MaxDescriptionLength)]
    public string DescriptionEnglish { get; set; } = string.Empty;
}

/// <summary>A meal a participant has ordered (Infosys: deltagere_madtider), at most once per meal time.</summary>
[Table("participant_meals")]
public class ParticipantMeal
{
    public int ParticipantId { get; set; }
    public virtual Participant Participant { get; set; } = null!;

    public int MealTimeId { get; set; }
    public virtual MealTime MealTime { get; set; } = null!;

    // Infosys: received, set when the meal is handed out
    public bool IsReceived { get; set; }
}
