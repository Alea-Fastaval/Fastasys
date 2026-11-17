using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Fastasys.ApiService.Data.Entities;

/// <summary>
/// A run (Infosys: hold) is one team playing a <see cref="Schedule"/> (Infosys: afvikling) of an activity.
/// A schedule can have several runs; each run has its own number and room.
/// </summary>
[Table("runs")]
public class Run
{
    public int Id { get; set; }

    public int ScheduleId { get; set; }
    public virtual Schedule Schedule { get; set; } = null!;

    // Infosys: holdnummer, unique within the schedule
    public int Number { get; set; }

    public int RoomId { get; set; }
    public virtual Room Room { get; set; } = null!;

    public virtual ICollection<RunSeat> Seats { get; set; } = new List<RunSeat>();
}

/// <summary>A participant's seat on a run (Infosys: pladser).</summary>
[Table("run_seats")]
public class RunSeat
{
    public int RunId { get; set; }
    public virtual Run Run { get; set; } = null!;

    // Infosys: pladsnummer, counted from 1 within the run
    public int SeatNumber { get; set; }

    public RunSeatRole Role { get; set; }

    public int ParticipantId { get; set; }
    public virtual Participant Participant { get; set; } = null!;
}

/// <summary>Infosys pladser.type: spiller, spilleder.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RunSeatRole>))]
public enum RunSeatRole
{
    Player,
    GameMaster
}
