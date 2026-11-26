using System.Security.Claims;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Fastasys.ApiService.Controllers;

// A row of Infosys's "Mad-typer" (mad/foodtypes): Id, Skjult, Kategori and Pris
public record FoodTypeSummaryDto(int Id, string NameDanish, string NameEnglish, int Price, bool IsHidden);

// Infosys mad/show/:id: a food type with its meal times by day and time. Mad::getMadtider has no order, so Infosys
// listed them in the order they were added (changed 2026-11-26).
public record FoodTypeDto(
    int Id,
    string NameDanish,
    string NameEnglish,
    int Price,
    bool IsHidden,
    IReadOnlyList<MealTimeDto> MealTimes
);

// OrderCount: how many participants have ordered the meal (annulled ones too); an ordered meal time can't be removed
public record MealTimeDto(int Id, DateTime Time, string DescriptionDanish, string DescriptionEnglish, int OrderCount);

// The fields of the Infosys create/edit form (mad/create, mad/edit/:id:): Kategori, Engelsk, Pris, Skjult and the meal
// times ("Mad tider"). A meal time without an Id is new; a meal time of the type that isn't listed is removed.
public abstract record FoodTypeFieldsDto
{
    public string? NameDanish { get; init; }
    public string? NameEnglish { get; init; }
    public int? Price { get; init; }
    public bool IsHidden { get; init; }
    public IReadOnlyList<MealTimeFieldsDto>? MealTimes { get; init; }
}

public record CreateFoodTypeDto : FoodTypeFieldsDto;

public record UpdateFoodTypeDto : FoodTypeFieldsDto;

// A meal time in the form: picked as a convention day and a time of day in Danish time, sent as an instant
public record MealTimeFieldsDto(int? Id, DateTimeOffset? Time, string? DescriptionDanish, string? DescriptionEnglish);

// One food type of the Infosys statistics page (food/stats): how many have ordered each meal time and how many have
// had it handed out, and the total over all meal times. Annulled participants aren't counted (agreed 2026-11-25).
public record FoodStatsDto(int Id, string NameDanish, int Total, IReadOnlyList<FoodStatsMealTimeDto> MealTimes);

public record FoodStatsMealTimeDto(int Id, DateTime Time, int Total, int Received);

// Infosys FoodController: Mad-typer, one type, create, edit, delete and statistics. Infosys lets only Infonaut and
// admin create, change and delete food types; here food_view may look, food_edit may change, and food_stats_view may
// see the statistics. Mad-udlevering, Madbørs and the handout times come in a later step (agreed 2026-11-25).
// Each endpoint names its policy: a controller-wide policy would also apply to the statistics, which food_stats_view
// alone may see.
[ApiController]
[Route("api/food-types")]
[Authorize]
public class FoodTypesController(
    InfosysDbContext db,
    IOptions<ConventionOptions> convention,
    ILogger<FoodTypesController> logger
) : ControllerBase
{
    private const string NotFoundMessage = "Food type not found";

    // The reasons tell the page which message to show
    private static readonly object InUse = new
    {
        message = "Participants have ordered the food type, so it can't be deleted",
        reason = "inUse",
    };

    private static readonly object MealTimeOrdered = new
    {
        message = "Participants have ordered the meal time, so it can't be removed",
        reason = "mealTimeOrdered",
    };

    private static readonly object NameTaken = new
    {
        message = "Another food type has that name",
        reason = "nameTaken",
    };

    // Infosys mad/foodtypes ("Mad-typer"): every food type in id order (FoodModel::getAllFood has no order)
    [HttpGet]
    [Authorize(Policy = "FoodView")]
    public async Task<ActionResult<IEnumerable<FoodTypeSummaryDto>>> GetAll() =>
        Ok(
            await db
                .FoodTypes.AsNoTracking()
                .OrderBy(f => f.Id)
                .Select(f => new FoodTypeSummaryDto(f.Id, f.NameDanish, f.NameEnglish, f.Price, f.IsHidden))
                .ToListAsync()
        );

    // Infosys mad/show/:id:
    [HttpGet("{id:int}")]
    [Authorize(Policy = "FoodView")]
    public async Task<ActionResult<FoodTypeDto>> Get(int id)
    {
        var dto = await LoadDtoAsync(id);
        return dto == null ? NotFound(new { message = NotFoundMessage }) : Ok(dto);
    }

    // Infosys food/stats ("Statistik")
    [HttpGet("stats")]
    [Authorize(Policy = "FoodStatsView")]
    public async Task<ActionResult<IEnumerable<FoodStatsDto>>> GetStats()
    {
        var types = await db
            .FoodTypes.AsNoTracking()
            .OrderBy(f => f.Id)
            .Select(f => new
            {
                f.Id,
                f.NameDanish,
                MealTimes = f
                    .MealTimes.OrderBy(m => m.Time)
                    .ThenBy(m => m.Id)
                    .Select(m => new { m.Id, m.Time })
                    .ToList(),
            })
            .ToListAsync();
        var counts = await db
            .ParticipantMeals.Where(m => !m.Participant.IsAnnulled)
            .GroupBy(m => m.MealTimeId)
            .Select(g => new
            {
                MealTimeId = g.Key,
                Total = g.Count(),
                Received = g.Count(m => m.IsReceived),
            })
            .ToDictionaryAsync(c => c.MealTimeId);

        var stats = types
            .Select(f =>
            {
                var mealTimes = f
                    .MealTimes.Select(m =>
                    {
                        var count = counts.GetValueOrDefault(m.Id);
                        return new FoodStatsMealTimeDto(m.Id, m.Time, count?.Total ?? 0, count?.Received ?? 0);
                    })
                    .ToList();
                return new FoodStatsDto(f.Id, f.NameDanish, mealTimes.Sum(m => m.Total), mealTimes);
            })
            .ToList();

        return Ok(stats);
    }

    // Infosys mad/create ("Opret mad-type")
    [HttpPost]
    [Authorize(Policy = "FoodManagement")]
    public async Task<ActionResult<FoodTypeDto>> Create([FromBody] CreateFoodTypeDto dto)
    {
        var (isValid, error) = Validate(dto, convention.Value, new Dictionary<int, DateTime>());
        if (!isValid)
            return BadRequest(new { message = error });

        // Infosys mad.kategori is unique
        if (await db.FoodTypes.AnyAsync(f => f.NameDanish == dto.NameDanish))
            return Conflict(NameTaken);

        var food = new FoodType();
        Apply(food, dto);
        foreach (var mealTime in dto.MealTimes ?? [])
        {
            food.MealTimes.Add(NewMealTime(mealTime));
        }

        db.FoodTypes.Add(food);
        await db.SaveChangesAsync();

        logger.LogInformation("Food type {FoodTypeId} created by user {UserId}", food.Id, CurrentUserId());

        return CreatedAtAction(nameof(Get), new { id = food.Id }, await LoadDtoAsync(food.Id));
    }

    // Infosys mad/edit/:id: ("Opdater"). Infosys matched the meal times kept on the form against its own list by
    // comparing only the first one, so removing one meal time removed every other one too; now only the removed meal
    // times are deleted (fixed 2026-11-25). A meal time that participants have ordered can't be removed (in Infosys
    // the foreign key gave an error page). Like Infosys, an Id that isn't one of the type's meal times is skipped.
    [HttpPut("{id:int}")]
    [Authorize(Policy = "FoodManagement")]
    public async Task<ActionResult<FoodTypeDto>> Update(int id, [FromBody] UpdateFoodTypeDto dto)
    {
        var food = await db.FoodTypes.Include(f => f.MealTimes).FirstOrDefaultAsync(f => f.Id == id);
        if (food == null)
            return NotFound(new { message = NotFoundMessage });

        var current = food.MealTimes.ToDictionary(m => m.Id, m => m.Time);
        var (isValid, error) = Validate(dto, convention.Value, current);
        if (!isValid)
            return BadRequest(new { message = error });

        if (await db.FoodTypes.AnyAsync(f => f.Id != id && f.NameDanish == dto.NameDanish))
            return Conflict(NameTaken);

        var posted = dto.MealTimes ?? [];
        var keptIds = posted.Select(m => m.Id ?? 0).Where(current.ContainsKey).ToHashSet();
        var removed = food.MealTimes.Where(m => !keptIds.Contains(m.Id)).ToList();
        var removedIds = removed.Select(m => m.Id).ToList();
        if (await db.ParticipantMeals.AnyAsync(m => removedIds.Contains(m.MealTimeId)))
            return Conflict(MealTimeOrdered);

        Apply(food, dto);
        foreach (var mealTime in posted)
        {
            if (mealTime.Id is int mealTimeId && mealTimeId > 0)
            {
                var existing = food.MealTimes.FirstOrDefault(m => m.Id == mealTimeId);
                if (existing != null)
                    ApplyMealTime(existing, mealTime);
                continue;
            }

            food.MealTimes.Add(NewMealTime(mealTime));
        }

        db.MealTimes.RemoveRange(removed);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // A participant ordered a removed meal time at the same time (the foreign key is RESTRICT)
            return Conflict(MealTimeOrdered);
        }

        logger.LogInformation("Food type {FoodTypeId} updated by user {UserId}", id, CurrentUserId());

        return Ok(await LoadDtoAsync(id));
    }

    // Infosys mad/delete/:id:. Infosys's foreign key from madtider made deleting a food type with meal times an error
    // page; now its meal times are deleted with it, unless participants have ordered one of them (agreed 2026-11-25).
    [HttpDelete("{id:int}")]
    [Authorize(Policy = "FoodManagement")]
    public async Task<IActionResult> Delete(int id)
    {
        var food = await db.FoodTypes.Include(f => f.MealTimes).FirstOrDefaultAsync(f => f.Id == id);
        if (food == null)
            return NotFound(new { message = NotFoundMessage });

        if (await db.ParticipantMeals.AnyAsync(m => m.MealTime.FoodTypeId == id))
            return Conflict(InUse);

        db.MealTimes.RemoveRange(food.MealTimes);
        db.FoodTypes.Remove(food);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // A participant ordered one of its meals at the same time
            return Conflict(InUse);
        }

        logger.LogInformation("Food type {FoodTypeId} deleted by user {UserId}", id, CurrentUserId());

        return Ok(new { message = "Food type deleted successfully" });
    }

    // Returns whether the form fields are valid and, if not, why. Invalid input is expected, so it never throws.
    // currentTimes are the type's meal times (id → time) when it is updated.
    public static (bool IsValid, string Error) Validate(
        FoodTypeFieldsDto dto,
        ConventionOptions convention,
        IReadOnlyDictionary<int, DateTime> currentTimes
    )
    {
        // Infosys refuses a kategori that PHP's empty() calls empty, which includes "0"
        if (string.IsNullOrEmpty(dto.NameDanish) || dto.NameDanish == "0")
            return (false, "The Danish name is required");
        if (dto.NameDanish.Length > FoodType.MaxNameLength)
            return (false, $"The Danish name can be at most {FoodType.MaxNameLength} characters");
        if ((dto.NameEnglish?.Length ?? 0) > FoodType.MaxNameLength)
            return (false, $"The English name can be at most {FoodType.MaxNameLength} characters");

        // Infosys took any number (is_numeric), also negative and decimal ones; whole kroner, 0 or more (fixed
        // 2026-11-25). A price of 0 is a free meal.
        if (dto.Price is null or < 0)
            return (false, "Price must be a whole number of 0 or more");

        var times = new List<DateTime>();
        foreach (var mealTime in dto.MealTimes ?? [])
        {
            if (mealTime.Time is not { } time)
                return (false, "A meal time needs a time");
            if (
                (mealTime.DescriptionDanish?.Length ?? 0) > MealTime.MaxDescriptionLength
                || (mealTime.DescriptionEnglish?.Length ?? 0) > MealTime.MaxDescriptionLength
            )
            {
                return (false, $"A description can be at most {MealTime.MaxDescriptionLength} characters");
            }

            var utc = time.UtcDateTime;
            // Infosys typed the date and time as text; the day is picked from the convention's days (agreed
            // 2026-11-25). A meal time keeps a time outside them when it isn't changed. Without the dates in the
            // configuration it can't be checked.
            var isUnchanged =
                mealTime.Id is int mealTimeId
                && currentTimes.TryGetValue(mealTimeId, out var currentTime)
                && currentTime == utc;
            var day = ParticipantRules.DanishDate(utc);
            if (
                !isUnchanged
                && convention.Start is { } start
                && convention.End is { } end
                && (day < start || day > end)
            )
            {
                return (false, "A meal time must be on one of the convention's days");
            }

            times.Add(utc);
        }

        // Infosys madtider: unique (mad_id, dato)
        if (times.Distinct().Count() != times.Count)
            return (false, "The food type has two meal times at the same time");

        return (true, string.Empty);
    }

    private static void Apply(FoodType food, FoodTypeFieldsDto dto)
    {
        food.NameDanish = dto.NameDanish!;
        food.NameEnglish = dto.NameEnglish ?? string.Empty;
        food.Price = dto.Price!.Value;
        food.IsHidden = dto.IsHidden;
    }

    private static MealTime NewMealTime(MealTimeFieldsDto dto)
    {
        var mealTime = new MealTime();
        ApplyMealTime(mealTime, dto);
        return mealTime;
    }

    private static void ApplyMealTime(MealTime mealTime, MealTimeFieldsDto dto)
    {
        mealTime.Time = dto.Time!.Value.UtcDateTime;
        mealTime.DescriptionDanish = dto.DescriptionDanish ?? string.Empty;
        mealTime.DescriptionEnglish = dto.DescriptionEnglish ?? string.Empty;
    }

    private async Task<FoodTypeDto?> LoadDtoAsync(int id)
    {
        var food = await db.FoodTypes.AsNoTracking().Include(f => f.MealTimes).FirstOrDefaultAsync(f => f.Id == id);
        if (food == null)
            return null;

        var mealTimeIds = food.MealTimes.Select(m => m.Id).ToList();
        var orderCounts = await db
            .ParticipantMeals.Where(m => mealTimeIds.Contains(m.MealTimeId))
            .GroupBy(m => m.MealTimeId)
            .Select(g => new { MealTimeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(c => c.MealTimeId, c => c.Count);

        return new FoodTypeDto(
            food.Id,
            food.NameDanish,
            food.NameEnglish,
            food.Price,
            food.IsHidden,
            food.MealTimes.OrderBy(m => m.Time)
                .ThenBy(m => m.Id)
                .Select(m => new MealTimeDto(
                    m.Id,
                    m.Time,
                    m.DescriptionDanish,
                    m.DescriptionEnglish,
                    orderCounts.GetValueOrDefault(m.Id)
                ))
                .ToList()
        );
    }

    private string? CurrentUserId() => User?.FindFirstValue(ClaimTypes.NameIdentifier);
}
