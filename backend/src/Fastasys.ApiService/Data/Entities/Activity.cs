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
    public string DescriptionDanish { get; set; } = string.Empty;
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

/// <summary>
/// A schedule (Infosys: afvikling) is one time slot of an activity. Its room is the meeting room;
/// the teams playing it are its <see cref="Run"/>s, each in its own room.
/// </summary>
[Table("schedule")]
public class Schedule
{
    public int Id { get; set; }
    public int ActivityId { get; set; }
    public virtual Activity Activity { get; set; } = null!;

    // Stored in UTC
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    // Meeting room; null means none (Infosys shows "Mødested: spørg venligst i informationen.")
    public int? RoomId { get; set; }
    public virtual Room? Room { get; set; }

    public virtual ICollection<Run> Runs { get; set; } = new List<Run>();

    public virtual ICollection<ScheduleSignup> Signups { get; set; } = new List<ScheduleSignup>();
}

/// <summary>
/// A participant's wish to play or run a schedule (Infosys: deltagere_tilmeldinger). Agreed on 2026-11-13: one wish
/// per participant, schedule and role, so a participant can wish to both run and play the same schedule, as the
/// Infosys signup's "SL + 1. prioritet" does.
/// </summary>
[Table("schedule_signups")]
public class ScheduleSignup
{
    public int ParticipantId { get; set; }
    public virtual Participant Participant { get; set; } = null!;
    public int ScheduleId { get; set; }
    public virtual Schedule Schedule { get; set; } = null!;

    // Infosys tilmeldingstype: spiller, spilleder
    public RunSeatRole Role { get; set; }

    // Infosys prioritet: 1-4 for players. Game master wishes have no real priority: the Infosys signup gives them 0,
    // the participant page 1.
    public int Priority { get; set; }
}
