using System.Security.Claims;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fastasys.ApiService.Controllers;

public record RunSeatDto(
    int SeatNumber,
    int ParticipantId,
    string ParticipantName,
    // Karma is not ported yet. Infosys shows the participant's karma here; until then it is always null.
    int? Karma,
    RunSeatRole Role
);

public record RunDto(
    int Id,
    int Number,
    int ScheduleId,
    int ActivityId,
    string ActivityTitle,
    DateTime StartTime,
    DateTime EndTime,
    int RoomId,
    string RoomName,
    bool NeedsPlayers,
    bool NeedsGameMasters,
    bool CanUsePlayers,
    IReadOnlyList<RunSeatDto> Seats
);

public record RunSummaryDto(
    int Id,
    int Number,
    int ActivityId,
    string ActivityTitle,
    // Not in Infosys hold/visalle. The runs list filters on it.
    ActivityType ActivityType,
    DateTime StartTime,
    DateTime EndTime,
    int RoomId,
    string RoomName,
    bool NeedsPlayers,
    bool NeedsGameMasters
);

public record RunRoomDto(int Id, string Name, string Location);

public record CreateRunDto(int ScheduleId);

public record UpdateRunDto(int RoomId);

public record CreateRunSeatDto(int ParticipantId, RunSeatRole Role);

// Infosys: hold (GroupsController, GroupsModel, entities Hold and Pladser)
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "ActivityView")]
public class RunsController : ControllerBase
{
    private readonly InfosysDbContext _db;
    private readonly ILogger<RunsController> _logger;

    public RunsController(InfosysDbContext db, ILogger<RunsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // Infosys hold/visalle: every run, in id order
    [HttpGet]
    public async Task<ActionResult<IEnumerable<RunSummaryDto>>> GetAll()
    {
        var rows = await _db.Runs
            .AsNoTracking()
            .OrderBy(r => r.Id)
            .Select(r => new
            {
                r.Id,
                r.Number,
                r.Schedule.ActivityId,
                ActivityTitle = r.Schedule.Activity.Title,
                ActivityType = r.Schedule.Activity.Type,
                r.Schedule.StartTime,
                r.Schedule.EndTime,
                r.RoomId,
                RoomName = r.Room.Name,
                r.Schedule.Activity.MinPlayersPerTeam,
                r.Schedule.Activity.GameMastersPerTeam,
                Players = r.Seats.Count(s => s.Role == RunSeatRole.Player),
                GameMasters = r.Seats.Count(s => s.Role == RunSeatRole.GameMaster)
            })
            .ToListAsync();

        var runs = rows.Select(r => new RunSummaryDto(
            r.Id,
            r.Number,
            r.ActivityId,
            r.ActivityTitle,
            r.ActivityType,
            r.StartTime,
            r.EndTime,
            r.RoomId,
            r.RoomName,
            RunRules.NeedsPlayers(r.Players, r.MinPlayersPerTeam),
            RunRules.NeedsGameMasters(r.GameMasters, r.GameMastersPerTeam)
        ));

        return Ok(runs);
    }

    // Infosys hold/vis/:id:
    [HttpGet("{id:int}")]
    public async Task<ActionResult<RunDto>> Get(int id)
    {
        var run = await LoadRunAsync(id);
        if (run == null) return NotFound(new { message = "Run not found" });
        return Ok(ToDto(run, run.Schedule));
    }

    // Infosys hold/ajaxcreategroup/:afvikling_id: ("Tilføj hold" on the activity page): the next number in the
    // schedule and the first room, in id order, that is free during the schedule.
    [HttpPost]
    [Authorize(Policy = "ActivityManagement")]
    public async Task<ActionResult<RunDto>> Create([FromBody] CreateRunDto dto)
    {
        var schedule = await _db.Schedules
            .Include(s => s.Activity)
            .FirstOrDefaultAsync(s => s.Id == dto.ScheduleId);
        if (schedule == null) return NotFound(new { message = "Could not find schedule" });

        var bookings = await LoadRoomBookingsAsync();
        var roomIds = await _db.Rooms.OrderBy(r => r.Id).Select(r => r.Id).ToListAsync();
        var freeRoomId = roomIds
            .Where(roomId => !RunRules.IsRoomOccupied(roomId, schedule.StartTime, schedule.EndTime, schedule.Activity.IsRoomExclusive, bookings))
            .Cast<int?>()
            .FirstOrDefault();
        if (freeRoomId == null) return Conflict(new { message = "No available room" });

        var numbers = await _db.Runs.Where(r => r.ScheduleId == schedule.Id).Select(r => r.Number).ToListAsync();
        var run = new Run
        {
            ScheduleId = schedule.Id,
            Number = RunRules.NextRunNumber(numbers),
            RoomId = freeRoomId.Value
        };

        _db.Runs.Add(run);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Another run got the same number at the same time (unique schedule + number)
            return Conflict(new { message = "Failed to create group" });
        }

        _logger.LogInformation("Run {RunId} created by user {UserId}", run.Id, CurrentUserId());

        var created = await LoadRunAsync(run.Id);
        return CreatedAtAction(nameof(Get), new { id = run.Id }, ToDto(created!, created!.Schedule));
    }

    // Infosys hold/edit/:id: offers the run's current room first, then every room free during the schedule
    [HttpGet("{id:int}/available-rooms")]
    [Authorize(Policy = "ActivityManagement")]
    public async Task<ActionResult<IEnumerable<RunRoomDto>>> GetAvailableRooms(int id)
    {
        var run = await _db.Runs
            .AsNoTracking()
            .Include(r => r.Room)
            .Include(r => r.Schedule).ThenInclude(s => s.Activity)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (run == null) return NotFound(new { message = "Run not found" });

        var bookings = await LoadRoomBookingsAsync();
        var rooms = await _db.Rooms.AsNoTracking().OrderBy(r => r.Id).ToListAsync();

        // Infosys lists the current room twice when its activity doesn't fill the room; it is listed once here.
        var available = rooms
            .Where(room => room.Id != run.RoomId)
            .Where(room => !RunRules.IsRoomOccupied(room.Id, run.Schedule.StartTime, run.Schedule.EndTime, run.Schedule.Activity.IsRoomExclusive, bookings))
            .Select(room => new RunRoomDto(room.Id, room.Name, room.Location));

        return Ok(available.Prepend(new RunRoomDto(run.Room.Id, run.Room.Name, run.Room.Location)).ToList());
    }

    // Infosys hold/edit/:id: only the room can be changed
    [HttpPut("{id:int}")]
    [Authorize(Policy = "ActivityManagement")]
    public async Task<ActionResult<RunDto>> Update(int id, [FromBody] UpdateRunDto dto)
    {
        var run = await _db.Runs
            .Include(r => r.Schedule).ThenInclude(s => s.Activity)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (run == null) return NotFound(new { message = "Run not found" });

        var (isValid, error) = await ValidateRoomAsync(run, dto.RoomId);
        if (!isValid) return BadRequest(new { message = error });

        run.RoomId = dto.RoomId;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Run {RunId} updated by user {UserId}", run.Id, CurrentUserId());

        var updated = await LoadRunAsync(id);
        return Ok(ToDto(updated!, updated!.Schedule));
    }

    // Infosys hold/slet/:id: and hold/ajaxdeletegroup/:id:. A run with participants can only be deleted when
    // their seats are removed too (Infosys "override"); otherwise the foreign key makes the delete fail.
    [HttpDelete("{id:int}")]
    [Authorize(Policy = "ActivityManagement")]
    public async Task<IActionResult> Delete(int id, [FromQuery] bool removeParticipants = false)
    {
        var run = await _db.Runs.Include(r => r.Seats).FirstOrDefaultAsync(r => r.Id == id);
        if (run == null) return NotFound(new { message = "Run not found" });

        if (run.Seats.Count > 0)
        {
            if (!removeParticipants) return Conflict(new { message = "Run has participants and cannot be deleted" });
            _db.RunSeats.RemoveRange(run.Seats);
        }

        _db.Runs.Remove(run);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Run {RunId} deleted by user {UserId}", id, CurrentUserId());

        return Ok(new { message = "Run deleted successfully" });
    }

    // Infosys groups/scheduleparticipant with to_group
    [HttpPost("{id:int}/seats")]
    [Authorize(Policy = "ActivityManagement")]
    public async Task<ActionResult<RunDto>> CreateSeat(int id, [FromBody] CreateRunSeatDto dto)
    {
        if (!Enum.IsDefined(dto.Role)) return BadRequest(new { message = "Unknown role" });

        var run = await _db.Runs
            .Include(r => r.Schedule).ThenInclude(s => s.Activity)
            .Include(r => r.Seats)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (run == null) return NotFound(new { message = "Run not found" });

        if (!await _db.Participants.AnyAsync(p => p.Id == dto.ParticipantId))
        {
            return NotFound(new { message = "Participant not found" });
        }

        var (isValid, error) = ValidateSeat(run, dto);
        if (!isValid) return Conflict(new { message = error });

        _db.RunSeats.Add(new RunSeat
        {
            RunId = run.Id,
            SeatNumber = RunRules.NextSeatNumber(run.Seats.Select(s => s.SeatNumber)),
            Role = dto.Role,
            ParticipantId = dto.ParticipantId
        });

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Another seat was taken at the same time (primary key run + seat number, unique run + participant)
            return Conflict(new { message = "Could not add to group" });
        }

        _logger.LogInformation("Participant {ParticipantId} added to run {RunId} by user {UserId}", dto.ParticipantId, run.Id, CurrentUserId());

        var updated = await LoadRunAsync(id);
        return Ok(ToDto(updated!, updated!.Schedule));
    }

    // Infosys groups/scheduleparticipant with from_group. Like Infosys, removing a participant who is not on
    // the run is not an error.
    [HttpDelete("{id:int}/seats/{participantId:int}")]
    [Authorize(Policy = "ActivityManagement")]
    public async Task<ActionResult<RunDto>> DeleteSeat(int id, int participantId)
    {
        if (!await _db.Runs.AnyAsync(r => r.Id == id)) return NotFound(new { message = "Run not found" });

        if (!await _db.Participants.AnyAsync(p => p.Id == participantId))
        {
            return NotFound(new { message = "Participant not found" });
        }

        var seat = await _db.RunSeats.FirstOrDefaultAsync(s => s.RunId == id && s.ParticipantId == participantId);
        if (seat != null)
        {
            _db.RunSeats.Remove(seat);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Participant {ParticipantId} removed from run {RunId} by user {UserId}", participantId, id, CurrentUserId());
        }

        var updated = await LoadRunAsync(id);
        return Ok(ToDto(updated!, updated!.Schedule));
    }

    // Maps a run with its room and seats (with participants) loaded. The schedule is passed separately and must
    // have its activity loaded.
    internal static RunDto ToDto(Run run, Schedule schedule)
    {
        var activity = schedule.Activity;
        var players = run.Seats.Count(s => s.Role == RunSeatRole.Player);
        var gameMasters = run.Seats.Count(s => s.Role == RunSeatRole.GameMaster);

        // Infosys Hold::getPladser orders by pladsnummer and shows "fornavn efternavn"
        var seats = run.Seats
            .OrderBy(s => s.SeatNumber)
            .Select(s => new RunSeatDto(s.SeatNumber, s.ParticipantId, s.Participant.LegalName(), null, s.Role))
            .ToList();

        return new RunDto(
            run.Id,
            run.Number,
            schedule.Id,
            activity.Id,
            activity.Title,
            schedule.StartTime,
            schedule.EndTime,
            run.RoomId,
            run.Room.Name,
            RunRules.NeedsPlayers(players, activity.MinPlayersPerTeam),
            RunRules.NeedsGameMasters(gameMasters, activity.GameMastersPerTeam),
            RunRules.CanUsePlayers(players, activity.MaxPlayersPerTeam),
            seats
        );
    }

    // Returns whether the participant can get a seat on the run and, if not, why
    private static (bool IsValid, string Error) ValidateSeat(Run run, CreateRunSeatDto dto)
    {
        if (run.Seats.Any(s => s.ParticipantId == dto.ParticipantId))
        {
            return (false, "Participant is already on the run");
        }

        var players = run.Seats.Count(s => s.Role == RunSeatRole.Player);
        var gameMasters = run.Seats.Count(s => s.Role == RunSeatRole.GameMaster);
        if (!RunRules.HasRoomFor(dto.Role, players, gameMasters, run.Schedule.Activity))
        {
            return (false, dto.Role == RunSeatRole.Player ? "The run cannot use more players" : "The run needs no more game masters");
        }

        return (true, string.Empty);
    }

    // Infosys only offers the current room and the free rooms in the edit form; the server didn't check.
    // Returns whether the room can be used and, if not, why.
    private async Task<(bool IsValid, string Error)> ValidateRoomAsync(Run run, int roomId)
    {
        if (roomId == run.RoomId) return (true, string.Empty);

        if (!await _db.Rooms.AnyAsync(r => r.Id == roomId)) return (false, "Unknown room");

        var bookings = await LoadRoomBookingsAsync();
        if (RunRules.IsRoomOccupied(roomId, run.Schedule.StartTime, run.Schedule.EndTime, run.Schedule.Activity.IsRoomExclusive, bookings))
        {
            return (false, "Room is not available at that time");
        }

        return (true, string.Empty);
    }

    private async Task<List<RoomBooking>> LoadRoomBookingsAsync() =>
        await _db.Runs
            .AsNoTracking()
            .Select(r => new RoomBooking(r.RoomId, r.Schedule.StartTime, r.Schedule.EndTime, r.Schedule.Activity.IsRoomExclusive))
            .ToListAsync();

    private async Task<Run?> LoadRunAsync(int id) =>
        await _db.Runs
            .AsNoTracking()
            .Include(r => r.Schedule).ThenInclude(s => s.Activity)
            .Include(r => r.Room)
            .Include(r => r.Seats).ThenInclude(s => s.Participant)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == id);

    private string? CurrentUserId() => User?.FindFirstValue(ClaimTypes.NameIdentifier);
}
