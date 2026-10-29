using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace Fastasys.ApiService.Data.Entities;

/// <summary>
/// An activity (Infosys: aktiviteter) is the abstraction over scenarios, board games and other events.
/// A concrete run of it is a <see cref="Schedule"/>.
/// </summary>
[Table("activities")]
public class Activity
{
    public int Id { get; set; }

    [MaxLength(256)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(256)]
    public string TitleEnglish { get; set; } = string.Empty;

    [MaxLength(256)]
    public string Author { get; set; } = string.Empty;

    public ActivityType Type { get; set; } = ActivityType.BoardGame;

    // Infosys: foromtale / description_en
    public string Description { get; set; } = string.Empty;
    public string DescriptionEnglish { get; set; } = string.Empty;

    // Infosys: teaser_dk / teaser_en. Not editable in the admin UI, only sent to the external APIs.
    public string TeaserDanish { get; set; } = string.Empty;
    public string TeaserEnglish { get; set; } = string.Empty;

    // Admin-only note
    public string Note { get; set; } = string.Empty;

    [Precision(5, 2)]
    public decimal DurationHours { get; set; }

    public int MinPlayersPerTeam { get; set; }
    public int MaxPlayersPerTeam { get; set; }
    public int GameMastersPerTeam { get; set; }

    // Whole DKK, like Infosys
    public int Price { get; set; }

    // 0 means no limit
    public int MaxSignups { get; set; }

    // null means no requirement. Infosys also had a maximum age; dropping it was agreed.
    public int? MinAge { get; set; }

    public bool CanSignUp { get; set; } = true;
    public bool IsHidden { get; set; }
    public bool IsRoomExclusive { get; set; } = true;
    public bool IsTimeExclusive { get; set; } = true;
    public bool IsReplayable { get; set; }

    public ActivityLanguage PlayerLanguage { get; set; } = ActivityLanguage.Danish;
    public ActivityLanguage GameMasterLanguage { get; set; } = ActivityLanguage.Danish;

    public KarmaType KarmaType { get; set; } = KarmaType.None;
    public AutoSignupCategory AutoSignupCategory { get; set; } = AutoSignupCategory.None;

    // WordPress post id on fastaval.dk, 0 means none
    public int WordPressId { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public virtual ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
}

/// <summary>Infosys aktiviteter.type: rolle, ottoviteter, braet, junior, system.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ActivityType>))]
public enum ActivityType
{
    Scenario,
    Quest,
    BoardGame,
    Junior,
    System
}

/// <summary>Infosys aktiviteter.sprog (dansk, engelsk, dansk+engelsk) and lang_gm (da, en, both).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ActivityLanguage>))]
public enum ActivityLanguage
{
    Danish,
    English,
    DanishAndEnglish
}

/// <summary>Infosys aktiviteter.karmatype. The numeric values are the legacy ones used by the karma calculation.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<KarmaType>))]
public enum KarmaType
{
    None = 0,
    Big = 1,
    Medium = 2,
    Small = 3
}

/// <summary>Infosys aktiviteter.auto_signup_category.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AutoSignupCategory>))]
public enum AutoSignupCategory
{
    None,
    All,
    Organizer,
    Author,
    Designer
}

[Table("schedule")]
public class Schedule
{
    public int Id { get; set; }
    public int ActivityId { get; set; }
    public virtual Activity Activity { get; set; } = null!;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int? RoomId { get; set; }
    public virtual Room? Room { get; set; }

    public virtual ICollection<ParticipantSchedule> ParticipantSchedules { get; set; } = new List<ParticipantSchedule>();
}

[Table("participant_schedules")]
public class ParticipantSchedule
{
    public int ParticipantId { get; set; }
    public virtual Participant Participant { get; set; } = null!;
    public int ScheduleId { get; set; }
    public virtual Schedule Schedule { get; set; } = null!;
    public string Role { get; set; } = "Player";
    public DateTime SignedUpAt { get; set; } = DateTime.UtcNow;
}
