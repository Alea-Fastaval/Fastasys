using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Fastasys.ApiService.Controllers;

public record ActivitySignupDto(
    int Id,
    int ParticipantId,
    int ScheduleId,
    int ActivityId,
    string ActivityTitle,
    DateTime StartTime,
    DateTime EndTime,
    string Role,
    int? Priority,
    DateTime SignedUpAt,
    bool IsCancelled
);

// Null role/priority makes omitted JSON fields invalid, rather than silently creating a first preference.
public record ActivitySignupFieldsDto(int ScheduleId, RunSeatRole? Role, int? Priority);

/// <summary>Admin-managed preferences (Infosys: deltagere_tilmeldinger), distinct from assigned run seats.</summary>
[ApiController]
[Route("api/participants/{participantId:int}/activity-signups")]
[Authorize(Policy = "ActivityView")]
public class ActivitySignupsController : ControllerBase
{
    private const string DuplicateMessage = "Participant already has a signup for this schedule and priority";
    private readonly InfosysDbContext _db;

    public ActivitySignupsController(InfosysDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ActivitySignupDto>>> GetAll(int participantId)
    {
        if (!await _db.Participants.AnyAsync(p => p.Id == participantId))
            return NotFound(new { message = "Participant not found" });

        return Ok(await Signups(participantId).ToListAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ActivitySignupDto>> Get(int participantId, int id)
    {
        var signup = await Signups(participantId, id).SingleOrDefaultAsync();
        return signup == null ? NotFound(new { message = "Signup not found" }) : Ok(signup);
    }

    [HttpPost]
    [Authorize(Policy = "ActivityManagement")]
    public async Task<ActionResult<ActivitySignupDto>> Create(int participantId, [FromBody] ActivitySignupFieldsDto dto)
    {
        if (!await _db.Participants.AnyAsync(p => p.Id == participantId))
            return NotFound(new { message = "Participant not found" });

        await using var transaction = await ScheduleWriteLock.AcquireAsync(_db, dto.ScheduleId);
        var error = await ValidateAsync(dto);
        if (error != null) return BadRequest(new { message = error });
        if (await IsDuplicateAsync(participantId, dto)) return Conflict(new { message = DuplicateMessage });

        var signup = new ParticipantSchedule
        {
            ParticipantId = participantId,
            ScheduleId = dto.ScheduleId,
            Role = dto.Role!.Value.ToString(),
            Priority = dto.Priority
        };
        _db.ParticipantSchedules.Add(signup);
        var conflict = await SaveAsync();
        if (conflict != null) return conflict;

        if (transaction != null) await transaction.CommitAsync();

        var result = await Signups(participantId, signup.Id).SingleAsync();
        return CreatedAtAction(nameof(Get), new { participantId, id = signup.Id }, result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "ActivityManagement")]
    public async Task<ActionResult<ActivitySignupDto>> Update(int participantId, int id, [FromBody] ActivitySignupFieldsDto dto)
    {
        var signup = await _db.ParticipantSchedules.SingleOrDefaultAsync(s => s.ParticipantId == participantId && s.Id == id);
        if (signup == null) return NotFound(new { message = "Signup not found" });

        await using var transaction = await ScheduleWriteLock.AcquireAsync(_db, dto.ScheduleId);
        var error = await ValidateAsync(dto);
        if (error != null) return BadRequest(new { message = error });
        if (await IsDuplicateAsync(participantId, dto, id)) return Conflict(new { message = DuplicateMessage });

        signup.ScheduleId = dto.ScheduleId;
        signup.Role = dto.Role!.Value.ToString();
        signup.Priority = dto.Priority;
        var conflict = await SaveAsync();
        if (conflict != null) return conflict;

        if (transaction != null) await transaction.CommitAsync();
        return Ok(await Signups(participantId, id).SingleAsync());
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "ActivityManagement")]
    public async Task<IActionResult> Delete(int participantId, int id)
    {
        var signup = await _db.ParticipantSchedules.SingleOrDefaultAsync(s => s.ParticipantId == participantId && s.Id == id);
        if (signup == null) return NotFound(new { message = "Signup not found" });

        _db.ParticipantSchedules.Remove(signup);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return NotFound(new { message = "Signup not found" });
        }
        return NoContent();
    }

    private async Task<string?> ValidateAsync(ActivitySignupFieldsDto dto)
    {
        if (dto.Role is null || !Enum.IsDefined(dto.Role.Value)) return "Role must be Player or GameMaster";
        if (dto.Priority is null) return "Priority is required";
        if (dto.Priority < (dto.Role == RunSeatRole.GameMaster ? 0 : 1) || dto.Priority > 4)
            return "Player priority must be 1–4; game-master priority must be 0–4";

        var activity = await _db.Schedules.Where(s => s.Id == dto.ScheduleId)
            .Select(s => new { s.Activity.GameMastersPerTeam, s.IsCancelled }).SingleOrDefaultAsync();
        if (activity == null) return "Schedule not found";
        if (activity.IsCancelled) return "Schedule is cancelled";
        if (dto.Role == RunSeatRole.GameMaster && activity.GameMastersPerTeam <= 0)
            return "Activity does not use game masters";
        return null;
    }

    private Task<bool> IsDuplicateAsync(int participantId, ActivitySignupFieldsDto dto, int? exceptId = null) =>
        _db.ParticipantSchedules.AnyAsync(s => s.ParticipantId == participantId && s.ScheduleId == dto.ScheduleId
            && s.Priority == dto.Priority && (exceptId == null || s.Id != exceptId));

    private async Task<ActionResult?> SaveAsync()
    {
        try
        {
            await _db.SaveChangesAsync();
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            return NotFound(new { message = "Signup not found" });
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // The unique index also protects against two concurrent requests passing the initial check.
            return Conflict(new { message = DuplicateMessage });
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        {
            return Conflict(new { message = "Participant or schedule was removed; reload before retrying" });
        }
    }

    private IQueryable<ActivitySignupDto> Signups(int participantId, int? id = null) =>
        _db.ParticipantSchedules.AsNoTracking()
            .Where(s => s.ParticipantId == participantId && (id == null || s.Id == id))
            .OrderBy(s => s.Schedule.StartTime).ThenBy(s => s.ScheduleId).ThenBy(s => s.Priority).ThenBy(s => s.Id)
            .Select(s => new ActivitySignupDto(s.Id, s.ParticipantId, s.ScheduleId, s.Schedule.ActivityId,
                s.Schedule.Activity.Title, s.Schedule.StartTime, s.Schedule.EndTime, s.Role, s.Priority, s.SignedUpAt, s.Schedule.IsCancelled));
}
