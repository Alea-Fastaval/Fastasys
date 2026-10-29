using System.Security.Claims;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fastasys.ApiService.Controllers;

public record ActivitySummaryDto(
    int Id,
    string Title,
    string TitleEnglish,
    ActivityType Type
);

public record ActivityDto(
    int Id,
    string Title,
    string TitleEnglish,
    string Author,
    ActivityType Type,
    string Description,
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

// The fields of the Infosys create/edit form. Teasers are not part of it.
public record SaveActivityDto(
    string? Title,
    string? TitleEnglish,
    string? Author,
    ActivityType Type,
    string? Description,
    string? DescriptionEnglish,
    string? Note,
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
    int WordPressId
);

public record ScheduleDto(
    int Id,
    int ActivityId,
    string ActivityTitle,
    DateTime StartTime,
    DateTime EndTime,
    int? RoomId,
    string? RoomName,
    int ParticipantCount
);

public record CreateScheduleDto(
    DateTime StartTime,
    DateTime EndTime,
    int? RoomId
);

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "ActivityView")]
public class ActivitiesController : ControllerBase
{
    public const int MaxTextLength = 256;
    public const int HighestAllowedAge = 200;
    public const int MaxSignupsLimit = 1000;

    private readonly InfosysDbContext _db;
    private readonly ILogger<ActivitiesController> _logger;

    public ActivitiesController(InfosysDbContext db, ILogger<ActivitiesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ActivitySummaryDto>>> GetAll()
    {
        var activities = await _db.Activities
            .AsNoTracking()
            .Select(a => new ActivitySummaryDto(a.Id, a.Title, a.TitleEnglish, a.Type))
            .ToListAsync();

        // Infosys lists every activity, hidden ones included, sorted with strcmp (byte-wise, case-sensitive).
        // Ordinal comparison gives the same order and avoids depending on the database collation.
        var sorted = activities
            .OrderBy(a => a.Title, StringComparer.Ordinal)
            .ThenBy(a => a.Id)
            .ToList();

        return Ok(sorted);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ActivityDto>> Get(int id)
    {
        var activity = await _db.Activities.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        if (activity == null) return NotFound(new { message = "Activity not found" });
        return Ok(ToDto(activity));
    }

    [HttpPost]
    [Authorize(Policy = "ActivityManagement")]
    public async Task<ActionResult<ActivityDto>> Create([FromBody] SaveActivityDto dto)
    {
        var error = Validate(dto);
        if (error != null) return BadRequest(new { message = error });

        var activity = new Activity();
        Apply(activity, dto);

        _db.Activities.Add(activity);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Activity {ActivityId} created by user {UserId}", activity.Id, CurrentUserId());

        return CreatedAtAction(nameof(Get), new { id = activity.Id }, ToDto(activity));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "ActivityManagement")]
    public async Task<ActionResult<ActivityDto>> Update(int id, [FromBody] SaveActivityDto dto)
    {
        var activity = await _db.Activities.FindAsync(id);
        if (activity == null) return NotFound(new { message = "Activity not found" });

        var error = Validate(dto);
        if (error != null) return BadRequest(new { message = error });

        Apply(activity, dto);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Activity {ActivityId} updated by user {UserId}", activity.Id, CurrentUserId());

        return Ok(ToDto(activity));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "ActivityManagement")]
    public async Task<IActionResult> Delete(int id)
    {
        var activity = await _db.Activities.FindAsync(id);
        if (activity == null) return NotFound(new { message = "Activity not found" });

        if (await _db.Schedules.AnyAsync(s => s.ActivityId == id))
        {
            return Conflict(new { message = "Activity has schedules and cannot be deleted" });
        }

        _db.Activities.Remove(activity);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Activity {ActivityId} deleted by user {UserId}", id, CurrentUserId());

        return Ok(new { message = "Activity deleted successfully" });
    }

    [HttpGet("{id:int}/schedules")]
    public async Task<ActionResult<IEnumerable<ScheduleDto>>> GetSchedules(int id)
    {
        var schedules = await _db.Schedules
            .Include(s => s.Activity)
            .Include(s => s.Room)
            .Include(s => s.ParticipantSchedules)
            .Where(s => s.ActivityId == id)
            .Select(s => new ScheduleDto(
                s.Id,
                s.ActivityId,
                s.Activity.Title,
                s.StartTime,
                s.EndTime,
                s.RoomId,
                s.Room != null ? s.Room.Name : null,
                s.ParticipantSchedules.Count
            ))
            .ToListAsync();

        return Ok(schedules);
    }

    [HttpPost("{id:int}/schedules")]
    [Authorize(Policy = "ActivityManagement")]
    public async Task<ActionResult<ScheduleDto>> CreateSchedule(int id, [FromBody] CreateScheduleDto dto)
    {
        var activity = await _db.Activities.FindAsync(id);
        if (activity == null) return NotFound("Activity not found");

        var schedule = new Schedule
        {
            ActivityId = id,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            RoomId = dto.RoomId
        };

        _db.Schedules.Add(schedule);
        await _db.SaveChangesAsync();

        return Ok(new ScheduleDto(
            schedule.Id,
            schedule.ActivityId,
            activity.Title,
            schedule.StartTime,
            schedule.EndTime,
            schedule.RoomId,
            null,
            0
        ));
    }

    // Infosys only validates in the browser (number inputs with min/max); the same limits are enforced here.
    private static string? Validate(SaveActivityDto dto)
    {
        if ((dto.Title?.Length ?? 0) > MaxTextLength) return $"Title can be at most {MaxTextLength} characters";
        if ((dto.TitleEnglish?.Length ?? 0) > MaxTextLength) return $"English title can be at most {MaxTextLength} characters";
        if ((dto.Author?.Length ?? 0) > MaxTextLength) return $"Author can be at most {MaxTextLength} characters";

        if (!Enum.IsDefined(dto.Type)) return "Unknown activity type";
        if (!Enum.IsDefined(dto.PlayerLanguage)) return "Unknown player language";
        if (!Enum.IsDefined(dto.GameMasterLanguage)) return "Unknown game master language";
        if (!Enum.IsDefined(dto.KarmaType)) return "Unknown karma type";
        if (!Enum.IsDefined(dto.AutoSignupCategory)) return "Unknown auto signup category";

        if (dto.MinAge is < 0 or > HighestAllowedAge) return $"Minimum age must be between 0 and {HighestAllowedAge}";
        if (dto.MaxSignups is < 0 or > MaxSignupsLimit) return $"Max signups must be between 0 and {MaxSignupsLimit}";

        return null;
    }

    private static void Apply(Activity activity, SaveActivityDto dto)
    {
        activity.Title = dto.Title ?? string.Empty;
        activity.TitleEnglish = dto.TitleEnglish ?? string.Empty;
        activity.Author = dto.Author ?? string.Empty;
        activity.Type = dto.Type;
        activity.Description = dto.Description ?? string.Empty;
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

    private static ActivityDto ToDto(Activity a) => new(
        a.Id,
        a.Title,
        a.TitleEnglish,
        a.Author,
        a.Type,
        a.Description,
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
