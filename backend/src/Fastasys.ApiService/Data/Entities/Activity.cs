using System.ComponentModel.DataAnnotations.Schema;

namespace Fastasys.ApiService.Data.Entities;

[Table("activities")]
public class Activity
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TitleEnglish { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public int MinParticipants { get; set; } = 1;
    public int MaxParticipants { get; set; } = 6;
    public int DurationMinutes { get; set; } = 240;
    public string Category { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public virtual ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
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
