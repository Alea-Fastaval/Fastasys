using System.Security.Claims;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Fastasys.ApiService.Controllers;

// A period the participant can help the hero force: the category (null is any category, "Alle"), the day and the part
// of the day
public record HeroAvailabilityDto(int? CategoryId, string? CategoryName, DateOnly Date, HeroPeriod Period);

// A category in the edit page's select (Infosys gdscategories.name_da)
public record HeroTaskCategoryDto(int Id, string NameDanish);

// Infosys deltager/updategdstilmeldinger/:id: ("Opdater Heltestyrke Tilmeldinger"): the participant's periods by day
// and time, and the categories in id order
public record ParticipantHeroAvailabilityDto(
    int ParticipantId,
    IReadOnlyList<HeroAvailabilityDto> Availability,
    IReadOnlyList<HeroTaskCategoryDto> Categories
);

// A period the participant should have
public record HeroAvailabilityFieldsDto(int? CategoryId, DateOnly? Date, HeroPeriod? Period);

// The page's "Opdater": the periods the participant should have
public record UpdateParticipantHeroAvailabilityDto(IReadOnlyList<HeroAvailabilityFieldsDto>? Availability);

// Infosys ParticipantController::updateGDSTilmeldinger and ParticipantModel::updateGDSTilmeldinger. Infosys let any
// role with ParticipantController/* change it (hiding the link from its Read-only roles); here it takes
// hero_force_participant_edit. Infosys offered only the periods of the chosen category's shifts; the hero force isn't
// ported, so any convention day and part of a day can be chosen, like in the signup (agreed 2026-11-26).
[ApiController]
[Route("api/participants/{participantId:int}/hero-availability")]
[Authorize(Policy = "ParticipantHeroForceManagement")]
public class ParticipantHeroAvailabilityController(
    InfosysDbContext db,
    IOptions<ConventionOptions> convention,
    ILogger<ParticipantHeroAvailabilityController> logger
) : ControllerBase
{
    private const string NotFoundMessage = "Participant not found";

    [HttpGet]
    public async Task<ActionResult<ParticipantHeroAvailabilityDto>> Get(int participantId)
    {
        if (!await db.Participants.AnyAsync(p => p.Id == participantId))
            return NotFound(new { message = NotFoundMessage });

        var categories = await db
            .HeroTaskCategories.AsNoTracking()
            .OrderBy(c => c.Id)
            .Select(c => new HeroTaskCategoryDto(c.Id, c.NameDanish))
            .ToListAsync();

        return Ok(new ParticipantHeroAvailabilityDto(participantId, await LoadAsync(db, participantId), categories));
    }

    // "Opdater": the list replaces the participant's periods. Infosys deleted them all and added the posted ones back,
    // skipping a period without a category (PHP's empty() on "0"), so saving dropped every period the signup had saved
    // for any category, and the same period twice failed on its primary key after the delete; now a period listed
    // twice is kept once and nothing changes unless all of it is saved (fixed 2026-11-26).
    [HttpPut]
    public async Task<IActionResult> Update(int participantId, [FromBody] UpdateParticipantHeroAvailabilityDto dto)
    {
        if (!await db.Participants.AnyAsync(p => p.Id == participantId))
            return NotFound(new { message = NotFoundMessage });

        var current = await db.ParticipantHeroAvailability.Where(a => a.ParticipantId == participantId).ToListAsync();
        var categoryIds = (await db.HeroTaskCategories.Select(c => c.Id).ToListAsync()).ToHashSet();
        var posted = dto.Availability ?? [];
        var (isValid, error) = Validate(posted, categoryIds, convention.Value, current.Select(a => a.Date).ToHashSet());
        if (!isValid)
            return BadRequest(new { message = error });

        var wanted = posted.Select(a => (a.Date!.Value, a.Period!.Value, a.CategoryId)).ToHashSet();
        db.ParticipantHeroAvailability.RemoveRange(
            current.Where(a => !wanted.Contains((a.Date, a.Period, a.CategoryId)))
        );
        foreach (var (date, period, categoryId) in wanted)
        {
            if (!current.Any(a => a.Date == date && a.Period == period && a.CategoryId == categoryId))
            {
                db.ParticipantHeroAvailability.Add(
                    new ParticipantHeroAvailability
                    {
                        ParticipantId = participantId,
                        Date = date,
                        Period = period,
                        CategoryId = categoryId,
                    }
                );
            }
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // The periods were changed at the same time
            return Conflict(new { message = "The hero force periods were changed at the same time; try again" });
        }

        logger.LogInformation(
            "Hero force availability of participant {ParticipantId} updated by user {UserId}",
            participantId,
            CurrentUserId()
        );

        return NoContent();
    }

    // Returns whether the posted periods are valid and, if not, why. Invalid input is expected, so it never throws.
    // currentDates are the days the participant has periods on already; they can stay outside the convention's days.
    public static (bool IsValid, string Error) Validate(
        IReadOnlyList<HeroAvailabilityFieldsDto> availability,
        IReadOnlySet<int> categoryIds,
        ConventionOptions convention,
        IReadOnlySet<DateOnly> currentDates
    )
    {
        foreach (var period in availability)
        {
            if (period.Date is not { } date)
                return (false, "A period needs a day");
            if (period.Period is not { } part || !Enum.IsDefined(part))
                return (false, "A period needs a part of the day");
            if (period.CategoryId is { } categoryId && !categoryIds.Contains(categoryId))
                return (false, "Unknown hero force category");

            // Without the dates in the configuration the day can't be checked
            if (
                !currentDates.Contains(date)
                && convention.Start is { } start
                && convention.End is { } end
                && (date < start || date > end)
            )
            {
                return (false, "A period must be on one of the convention's days");
            }
        }

        return (true, string.Empty);
    }

    // The participant's periods by day and time (see HeroAvailabilityRules.Order)
    public static async Task<IReadOnlyList<HeroAvailabilityDto>> LoadAsync(InfosysDbContext db, int participantId)
    {
        var rows = await db
            .ParticipantHeroAvailability.AsNoTracking()
            .Where(a => a.ParticipantId == participantId)
            .Select(a => new HeroAvailabilityDto(
                a.CategoryId,
                a.Category == null ? null : a.Category.NameDanish,
                a.Date,
                a.Period
            ))
            .ToListAsync();
        return HeroAvailabilityRules.Order(rows, r => r.Date, r => r.Period, r => r.CategoryId).ToList();
    }

    private string? CurrentUserId() => User?.FindFirstValue(ClaimTypes.NameIdentifier);
}
