using System.Security.Claims;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fastasys.ApiService.Controllers;

public record ActivitySummaryDto(int Id, string Title, string TitleEnglish, ActivityType Type);

public record ActivityDto(
    int Id,
    string Title,
    string TitleEnglish,
    string Author,
    ActivityType Type,
    string DescriptionDanish,
    string DescriptionEnglish,
    string TeaserDanish,
    string TeaserEnglish,
    string Note,
    decimal DurationHours,
    int MinPlayersPerTeam,
    int MaxPlayersPerTeam,
    int GameMastersPerTeam,
    int Price,
    int MaxSignups,
    int? MinAge,
    bool CanSignUp,
    bool IsHidden,
    bool IsRoomExclusive,
    bool IsTimeExclusive,
    bool IsReplayable,
    ActivityLanguage PlayerLanguage,
    ActivityLanguage GameMasterLanguage,
    KarmaType KarmaType,
    AutoSignupCategory AutoSignupCategory,
    int WordPressId,
    DateTimeOffset UpdatedAt
);

// The fields of the Infosys create and edit forms, which are the same. Teasers are not part of them.
public abstract record ActivityFieldsDto
{
    public string? Title { get; init; }
    public string? TitleEnglish { get; init; }
    public string? Author { get; init; }
    public ActivityType Type { get; init; }
    public string? DescriptionDanish { get; init; }
    public string? DescriptionEnglish { get; init; }
    public string? Note { get; init; }
    public decimal DurationHours { get; init; }
    public int MinPlayersPerTeam { get; init; }
    public int MaxPlayersPerTeam { get; init; }
    public int GameMastersPerTeam { get; init; }
    public int Price { get; init; }
    public int MaxSignups { get; init; }
    public int? MinAge { get; init; }
    public bool CanSignUp { get; init; }
    public bool IsHidden { get; init; }
    public bool IsRoomExclusive { get; init; }
    public bool IsTimeExclusive { get; init; }
    public bool IsReplayable { get; init; }
    public ActivityLanguage PlayerLanguage { get; init; }
    public ActivityLanguage GameMasterLanguage { get; init; }
    public KarmaType KarmaType { get; init; }
    public AutoSignupCategory AutoSignupCategory { get; init; }
    public int WordPressId { get; init; }
}

public record CreateActivityDto : ActivityFieldsDto;

public record UpdateActivityDto : ActivityFieldsDto;

// Infosys: afvikling. The room is the meeting room; each run has its own room.
public record ScheduleDto(
    int Id,
    int ActivityId,
    DateTime StartTime,
    DateTime EndTime,
    int? RoomId,
    string? RoomName,
    IReadOnlyList<RunDto> Runs,
    bool IsCancelled,
    DateTimeOffset? CancelledAt
);

// Infosys aktivitet/opretafvikling: start, slut and mødelokale. Times are instants; they are stored in UTC.
public record CreateScheduleDto(DateTimeOffset? StartTime, DateTimeOffset? EndTime, int? RoomId);

// Infosys aktivitet/editafvikling: the same fields as creating
public record UpdateScheduleDto(DateTimeOffset? StartTime, DateTimeOffset? EndTime, int? RoomId);

public record UpdateScheduleCancellationDto(bool? IsCancelled);

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "ActivityView")]
public class ActivitiesController(InfosysDbContext db, ILogger<ActivitiesController> logger, KarmaService karma)
    : ControllerBase
{
    public const int MaxTextLength = 256;
    public const int HighestAllowedAge = 200;
    public const int MaxSignupsLimit = 1000;
    private const string ScheduleHasRunsMessage = "Schedule has runs and cannot be deleted";
    private readonly KarmaService _karma = karma;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ActivitySummaryDto>>> GetAll()
    {
        var activities = await db
            .Activities.AsNoTracking()
            .Select(a => new ActivitySummaryDto(a.Id, a.Title, a.TitleEnglish, a.Type))
            .ToListAsync();

        // Infosys lists every activity, hidden ones included, sorted with strcmp (byte-wise, case-sensitive).
        // Ordinal comparison gives the same order and avoids depending on the database collation.
        var sorted = activities.OrderBy(a => a.Title, StringComparer.Ordinal).ThenBy(a => a.Id).ToList();

        return Ok(sorted);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ActivityDto>> Get(int id)
    {
        var activity = await db.Activities.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        if (activity == null)
            return NotFound(new { message = "Activity not found" });
        return Ok(ToDto(activity));
    }

    [HttpPost]
    [Authorize(Policy = "ActivityManagement")]
    public async Task<ActionResult<ActivityDto>> Create([FromBody] CreateActivityDto dto)
    {
        var (isValid, error) = Validate(dto);
        if (!isValid)
            return BadRequest(new { message = error });

        var activity = new Activity();
        Apply(activity, dto);

        db.Activities.Add(activity);
        await db.SaveChangesAsync();

        logger.LogInformation("Activity {ActivityId} created by user {UserId}", activity.Id, CurrentUserId());

        return CreatedAtAction(nameof(Get), new { id = activity.Id }, ToDto(activity));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "ActivityManagement")]
    public async Task<ActionResult<ActivityDto>> Update(int id, [FromBody] UpdateActivityDto dto)
    {
        var activity = await db.Activities.FindAsync(id);
        if (activity == null)
            return NotFound(new { message = "Activity not found" });

        var (isValid, error) = Validate(dto);
        if (!isValid)
            return BadRequest(new { message = error });

        Apply(activity, dto);
        await db.SaveChangesAsync();

        logger.LogInformation("Activity {ActivityId} updated by user {UserId}", activity.Id, CurrentUserId());

        return Ok(ToDto(activity));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "ActivityManagement")]
    public async Task<IActionResult> Delete(int id)
    {
        var activity = await db.Activities.FindAsync(id);
        if (activity == null)
            return NotFound(new { message = "Activity not found" });

        if (await db.Schedules.AnyAsync(s => s.ActivityId == id))
        {
            return Conflict(new { message = "Activity has schedules and cannot be deleted" });
        }

        db.Activities.Remove(activity);
        await db.SaveChangesAsync();

        logger.LogInformation("Activity {ActivityId} deleted by user {UserId}", id, CurrentUserId());

        return Ok(new { message = "Activity deleted successfully" });
    }

    // Infosys aktivitet/vis/:id: lists the activity's schedules by start time, each with its runs and their seats
    [HttpGet("{id:int}/schedules")]
    public async Task<ActionResult<IEnumerable<ScheduleDto>>> GetSchedules(int id)
    {
        if (!await db.Activities.AnyAsync(a => a.Id == id))
            return NotFound(new { message = "Activity not found" });

        var schedules = await SchedulesWithRuns()
            .Where(s => s.ActivityId == id)
            .OrderBy(s => s.StartTime)
            .ThenBy(s => s.Id)
            .ToListAsync();

        var karma = await _karma.CalculateAsync(
            schedules.SelectMany(s => s.Runs).SelectMany(r => r.Seats).Select(s => s.ParticipantId)
        );
        return Ok(schedules.Select(s => ToDto(s, karma)).ToList());
    }

    // Infosys aktivitet/editafvikling/:id: shows the schedule in the edit form
    [HttpGet("{id:int}/schedules/{scheduleId:int}")]
    public async Task<ActionResult<ScheduleDto>> GetSchedule(int id, int scheduleId)
    {
        var schedule = await SchedulesWithRuns().FirstOrDefaultAsync(s => s.Id == scheduleId && s.ActivityId == id);
        if (schedule == null)
            return NotFound(new { message = "Schedule not found" });
        return Ok(await ToDtoAsync(schedule));
    }

    [HttpPut("{id:int}/schedules/{scheduleId:int}/cancellation")]
    [Authorize(Policy = "ActivityManagement")]
    public async Task<ActionResult<ScheduleDto>> SetCancellation(
        int id,
        int scheduleId,
        [FromBody] UpdateScheduleCancellationDto dto
    )
    {
        if (dto.IsCancelled == null)
            return BadRequest(new { message = "IsCancelled is required" });
        await using var transaction = await ScheduleWriteLock.AcquireAsync(db, scheduleId);
        var schedule = await db
            .Schedules.Include(s => s.Activity)
            .SingleOrDefaultAsync(s => s.Id == scheduleId && s.ActivityId == id);
        if (schedule == null)
            return NotFound(new { message = "Schedule not found" });

        if (schedule.IsCancelled != dto.IsCancelled.Value)
        {
            if (!dto.IsCancelled.Value)
            {
                await ScheduleWriteLock.AcquireRoomsAsync(db);
                var bookings = await db
                    .Runs.AsNoTracking()
                    .Where(r => r.ScheduleId != scheduleId && !r.Schedule.IsCancelled)
                    .Select(r => new RoomBooking(
                        r.RoomId,
                        r.Schedule.StartTime,
                        r.Schedule.EndTime,
                        r.Schedule.Activity.IsRoomExclusive
                    ))
                    .ToListAsync();
                var rooms = await db
                    .Runs.AsNoTracking()
                    .Where(r => r.ScheduleId == scheduleId)
                    .OrderBy(r => r.Id)
                    .Select(r => r.RoomId)
                    .ToListAsync();
                foreach (var room in rooms)
                {
                    if (
                        RunRules.IsRoomOccupied(
                            room,
                            schedule.StartTime,
                            schedule.EndTime,
                            schedule.Activity.IsRoomExclusive,
                            bookings
                        )
                    )
                        return Conflict(new { message = "Cannot restore schedule: a retained run's room is occupied" });
                    bookings.Add(new(room, schedule.StartTime, schedule.EndTime, schedule.Activity.IsRoomExclusive));
                }
            }
            schedule.IsCancelled = dto.IsCancelled.Value;
            schedule.CancelledAt = schedule.IsCancelled ? DateTimeOffset.UtcNow : null;
            schedule.Activity.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            logger.LogInformation(
                "Schedule {ScheduleId} cancellation set to {IsCancelled} by user {UserId}",
                scheduleId,
                schedule.IsCancelled,
                CurrentUserId()
            );
        }
        if (transaction != null)
            await transaction.CommitAsync();
        var updated = await SchedulesWithRuns().SingleAsync(s => s.Id == scheduleId);
        return Ok(await ToDtoAsync(updated));
    }

    // Infosys aktivitet/editafvikling/:id: ("Opdater"). The checks are the ones for creating a schedule; the
    // schedule itself doesn't count as a duplicate. Infosys leaves the runs and their rooms as they are, also when
    // the times change.
    [HttpPut("{id:int}/schedules/{scheduleId:int}")]
    [Authorize(Policy = "ActivityManagement")]
    public async Task<ActionResult<ScheduleDto>> UpdateSchedule(
        int id,
        int scheduleId,
        [FromBody] UpdateScheduleDto dto
    )
    {
        var schedule = await db
            .Schedules.Include(s => s.Activity)
            .FirstOrDefaultAsync(s => s.Id == scheduleId && s.ActivityId == id);
        if (schedule == null)
            return NotFound(new { message = "Schedule not found" });

        var (isValid, error) = ValidateSchedule(dto.StartTime, dto.EndTime);
        if (!isValid)
            return BadRequest(new { message = error });

        var start = dto.StartTime!.Value.UtcDateTime;
        var end = dto.EndTime!.Value.UtcDateTime;
        var roomId = NormalizeRoomId(dto.RoomId);

        if (roomId != null && !await db.Rooms.AnyAsync(r => r.Id == roomId))
        {
            return BadRequest(new { message = "Unknown room" });
        }

        if (
            await db.Schedules.AnyAsync(s =>
                s.ActivityId == id && s.Id != scheduleId && s.StartTime == start && s.EndTime == end
            )
        )
        {
            return Conflict(new { message = "The activity already has a schedule with that start and end" });
        }

        schedule.StartTime = start;
        schedule.EndTime = end;
        schedule.RoomId = roomId;
        schedule.Activity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Schedule {ScheduleId} for activity {ActivityId} updated by user {UserId}",
            scheduleId,
            id,
            CurrentUserId()
        );

        var updated = await SchedulesWithRuns().FirstAsync(s => s.Id == scheduleId);
        return Ok(await ToDtoAsync(updated));
    }

    // Infosys aktivitet/sletafvikling/:id:. The foreign keys on the runs make Infosys refuse to delete a schedule
    // that has runs (with a generic error page); here that is checked first so the user is told why.
    // Unlike creating and updating, Infosys doesn't touch the activity's updated time when deleting.
    [HttpDelete("{id:int}/schedules/{scheduleId:int}")]
    [Authorize(Policy = "ActivityManagement")]
    public async Task<IActionResult> DeleteSchedule(int id, int scheduleId)
    {
        var schedule = await db.Schedules.FirstOrDefaultAsync(s => s.Id == scheduleId && s.ActivityId == id);
        if (schedule == null)
            return NotFound(new { message = "Schedule not found" });

        if (await db.Runs.AnyAsync(r => r.ScheduleId == scheduleId))
        {
            return Conflict(new { message = ScheduleHasRunsMessage });
        }

        // Infosys's foreign key from deltagere_tilmeldinger refuses it too (agreed 2026-11-13). The reason tells the
        // page which message to show.
        if (await db.ScheduleSignups.AnyAsync(s => s.ScheduleId == scheduleId))
        {
            return Conflict(new { message = "Schedule has signups and cannot be deleted", reason = "signups" });
        }

        db.Schedules.Remove(schedule);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // A run or a signup was added to the schedule at the same time (both are RESTRICT)
            return Conflict(new { message = ScheduleHasRunsMessage });
        }

        logger.LogInformation(
            "Schedule {ScheduleId} for activity {ActivityId} deleted by user {UserId}",
            scheduleId,
            id,
            CurrentUserId()
        );

        return Ok(new { message = "Schedule deleted successfully" });
    }

    // Infosys aktivitet/opretafvikling/:aktivitet_id:
    [HttpPost("{id:int}/schedules")]
    [Authorize(Policy = "ActivityManagement")]
    public async Task<ActionResult<ScheduleDto>> CreateSchedule(int id, [FromBody] CreateScheduleDto dto)
    {
        var activity = await db.Activities.FindAsync(id);
        if (activity == null)
            return NotFound(new { message = "Activity not found" });

        var (isValid, error) = ValidateSchedule(dto.StartTime, dto.EndTime);
        if (!isValid)
            return BadRequest(new { message = error });

        var start = dto.StartTime!.Value.UtcDateTime;
        var end = dto.EndTime!.Value.UtcDateTime;
        var roomId = NormalizeRoomId(dto.RoomId);

        if (roomId != null && !await db.Rooms.AnyAsync(r => r.Id == roomId))
        {
            return BadRequest(new { message = "Unknown room" });
        }

        // Infosys refuses a second schedule with the same start and end for the activity
        if (await db.Schedules.AnyAsync(s => s.ActivityId == id && s.StartTime == start && s.EndTime == end))
        {
            return Conflict(new { message = "The activity already has a schedule with that start and end" });
        }

        var schedule = new Schedule
        {
            ActivityId = id,
            StartTime = start,
            EndTime = end,
            RoomId = roomId,
        };

        db.Schedules.Add(schedule);
        activity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Schedule {ScheduleId} for activity {ActivityId} created by user {UserId}",
            schedule.Id,
            id,
            CurrentUserId()
        );

        var created = await db
            .Schedules.AsNoTracking()
            .Include(s => s.Activity)
            .Include(s => s.Room)
            .FirstAsync(s => s.Id == schedule.Id);

        return CreatedAtAction(nameof(GetSchedules), new { id }, await ToDtoAsync(created));
    }

    // Infosys only validates in the browser (number inputs with min/max); the same limits are enforced here.
    // Returns whether the input is valid and, if not, why. Invalid input is expected, so it never throws.
    private static (bool IsValid, string Error) Validate(ActivityFieldsDto dto)
    {
        if ((dto.Title?.Length ?? 0) > MaxTextLength)
            return (false, $"Title can be at most {MaxTextLength} characters");
        if ((dto.TitleEnglish?.Length ?? 0) > MaxTextLength)
            return (false, $"English title can be at most {MaxTextLength} characters");
        if ((dto.Author?.Length ?? 0) > MaxTextLength)
            return (false, $"Author can be at most {MaxTextLength} characters");

        if (!Enum.IsDefined(dto.Type))
            return (false, "Unknown activity type");
        if (!Enum.IsDefined(dto.PlayerLanguage))
            return (false, "Unknown player language");
        if (!Enum.IsDefined(dto.GameMasterLanguage))
            return (false, "Unknown game master language");
        if (!Enum.IsDefined(dto.KarmaType))
            return (false, "Unknown karma type");
        if (!Enum.IsDefined(dto.AutoSignupCategory))
            return (false, "Unknown auto signup category");

        if (dto.MinAge is < 0 or > HighestAllowedAge)
            return (false, $"Minimum age must be between 0 and {HighestAllowedAge}");
        if (dto.MaxSignups is < 0 or > MaxSignupsLimit)
            return (false, $"Max signups must be between 0 and {MaxSignupsLimit}");

        return (true, string.Empty);
    }

    // Infosys only creates or updates the schedule when both times are posted and start is before end.
    // Returns whether the input is valid and, if not, why.
    private static (bool IsValid, string Error) ValidateSchedule(DateTimeOffset? startTime, DateTimeOffset? endTime)
    {
        if (startTime == null || endTime == null)
            return (false, "Start and end are required");
        if (startTime >= endTime)
            return (false, "Start must be before end");

        return (true, string.Empty);
    }

    // Infosys stores intval(lokale_id), and 0 means no meeting room
    private static int? NormalizeRoomId(int? roomId) => roomId is null or 0 ? null : roomId;

    // Schedules with what ToDto needs: the activity, the meeting room and the runs with their rooms and seats
    private IQueryable<Schedule> SchedulesWithRuns() =>
        db
            .Schedules.AsNoTracking()
            .Include(s => s.Activity)
            .Include(s => s.Room)
            .Include(s => s.Runs)
                .ThenInclude(r => r.Room)
            .Include(s => s.Runs)
                .ThenInclude(r => r.Seats)
                    .ThenInclude(seat => seat.Participant)
            .AsSplitQuery();

    private async Task<ScheduleDto> ToDtoAsync(Schedule schedule) =>
        ToDto(
            schedule,
            await _karma.CalculateAsync(schedule.Runs.SelectMany(r => r.Seats).Select(s => s.ParticipantId))
        );

    private static ScheduleDto ToDto(Schedule s, IReadOnlyDictionary<int, int> karma) =>
        new(
            s.Id,
            s.ActivityId,
            s.StartTime,
            s.EndTime,
            s.RoomId,
            s.Room?.Name,
            s.Runs.OrderBy(r => r.Number).ThenBy(r => r.Id).Select(r => RunsController.ToDto(r, s, karma)).ToList(),
            s.IsCancelled,
            s.CancelledAt
        );

    private static void Apply(Activity activity, ActivityFieldsDto dto)
    {
        activity.Title = dto.Title ?? string.Empty;
        activity.TitleEnglish = dto.TitleEnglish ?? string.Empty;
        activity.Author = dto.Author ?? string.Empty;
        activity.Type = dto.Type;
        activity.DescriptionDanish = dto.DescriptionDanish ?? string.Empty;
        activity.DescriptionEnglish = dto.DescriptionEnglish ?? string.Empty;
        activity.Note = dto.Note ?? string.Empty;
        activity.DurationHours = dto.DurationHours;
        activity.MinPlayersPerTeam = dto.MinPlayersPerTeam;
        activity.MaxPlayersPerTeam = dto.MaxPlayersPerTeam;
        activity.GameMastersPerTeam = dto.GameMastersPerTeam;
        activity.Price = dto.Price;
        activity.MaxSignups = dto.MaxSignups;
        activity.MinAge = NormalizeAge(dto.MinAge);
        activity.CanSignUp = dto.CanSignUp;
        activity.IsHidden = dto.IsHidden;
        activity.IsRoomExclusive = dto.IsRoomExclusive;
        activity.IsTimeExclusive = dto.IsTimeExclusive;
        activity.IsReplayable = dto.IsReplayable;
        activity.PlayerLanguage = dto.PlayerLanguage;
        activity.GameMasterLanguage = dto.GameMasterLanguage;
        activity.KarmaType = dto.KarmaType;
        activity.AutoSignupCategory = dto.AutoSignupCategory;
        activity.WordPressId = dto.WordPressId;
        activity.UpdatedAt = DateTimeOffset.UtcNow;
    }

    // Infosys removes the age requirement when the posted age is empty(), which includes 0
    private static int? NormalizeAge(int? age) => age is null or 0 ? null : age;

    private string? CurrentUserId() => User?.FindFirstValue(ClaimTypes.NameIdentifier);

    private static ActivityDto ToDto(Activity a) =>
        new(
            a.Id,
            a.Title,
            a.TitleEnglish,
            a.Author,
            a.Type,
            a.DescriptionDanish,
            a.DescriptionEnglish,
            a.TeaserDanish,
            a.TeaserEnglish,
            a.Note,
            a.DurationHours,
            a.MinPlayersPerTeam,
            a.MaxPlayersPerTeam,
            a.GameMastersPerTeam,
            a.Price,
            a.MaxSignups,
            a.MinAge,
            a.CanSignUp,
            a.IsHidden,
            a.IsRoomExclusive,
            a.IsTimeExclusive,
            a.IsReplayable,
            a.PlayerLanguage,
            a.GameMasterLanguage,
            a.KarmaType,
            a.AutoSignupCategory,
            a.WordPressId,
            a.UpdatedAt
        );
}
