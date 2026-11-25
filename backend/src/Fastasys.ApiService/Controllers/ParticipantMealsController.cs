using System.Security.Claims;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fastasys.ApiService.Controllers;

// A food type with its meal times, for the Mad selects of the participant's edit page and the participant search's Mad
// section. Every food type, hidden ones too, in id order; its meal times in id order (Mad::getMadtider has no order).
public record FoodTypeOptionDto(int Id, string NameDanish, IReadOnlyList<MealTimeOptionDto> MealTimes);

// A meal time in a select: the page shows its Danish day name ("Ons"), as Infosys's mad/ajaxgetmadtider/:id: did
public record MealTimeOptionDto(int Id, DateTime Time);

// A meal of the participant on the edit page: "{FoodName} {short day of Time}"
public record ParticipantMealRowDto(int MealTimeId, string FoodName, DateTime Time, bool IsReceived);

// The Mad part of Infosys's "Rediger indgang, mad og wear" page (deltager/updatemadwear/:id:): the participant's meals
// by time (DeltagereMadtider::getDeltagerMadtider orders by dato) and the food types the selects offer
public record ParticipantMealsDto(
    int ParticipantId,
    IReadOnlyList<ParticipantMealRowDto> Meals,
    IReadOnlyList<FoodTypeOptionDto> FoodTypes
);

// The page's "Opdater" (Infosys madtider[]): the meal times the participant should have
public record UpdateParticipantMealsDto(IReadOnlyList<int>? MealTimeIds);

// Infosys ParticipantController::updateMadWear and ParticipantModel::handleFoodUpdate, for the meals. Infosys lets only
// Infonaut and admin change a participant's meals ("Kun infonauter kan opdatere mad"); here it takes
// food_participant_edit.
[ApiController]
[Route("api/participants/{participantId:int}/meals")]
[Authorize(Policy = "ParticipantFoodManagement")]
public class ParticipantMealsController(InfosysDbContext db, ILogger<ParticipantMealsController> logger)
    : ControllerBase
{
    private const string NotFoundMessage = "Participant not found";

    [HttpGet]
    public async Task<ActionResult<ParticipantMealsDto>> Get(int participantId)
    {
        if (!await db.Participants.AnyAsync(p => p.Id == participantId))
            return NotFound(new { message = NotFoundMessage });

        var meals = await db
            .ParticipantMeals.AsNoTracking()
            .Where(m => m.ParticipantId == participantId)
            .OrderBy(m => m.MealTime.Time)
            .ThenBy(m => m.MealTimeId)
            .Select(m => new ParticipantMealRowDto(
                m.MealTimeId,
                m.MealTime.FoodType.NameDanish,
                m.MealTime.Time,
                m.IsReceived
            ))
            .ToListAsync();

        return Ok(new ParticipantMealsDto(participantId, meals, await LoadFoodTypeOptionsAsync(db)));
    }

    // "Opdater": the list replaces the participant's meals. Infosys deleted all of them and added the posted ones back,
    // so "Udleveret" was reset on every meal kept; a meal kept now keeps it (fixed 2026-11-25). Like Infosys, an id
    // that isn't a meal time is skipped. The same meal time listed twice is kept once; in Infosys that was an error
    // page (fixed 2026-11-25).
    [HttpPut]
    public async Task<IActionResult> Update(int participantId, [FromBody] UpdateParticipantMealsDto dto)
    {
        if (!await db.Participants.AnyAsync(p => p.Id == participantId))
            return NotFound(new { message = NotFoundMessage });

        var requested = (dto.MealTimeIds ?? []).Distinct().ToList();
        var wanted = await db.MealTimes.Where(m => requested.Contains(m.Id)).Select(m => m.Id).ToListAsync();
        var current = await db.ParticipantMeals.Where(m => m.ParticipantId == participantId).ToListAsync();

        db.ParticipantMeals.RemoveRange(current.Where(m => !wanted.Contains(m.MealTimeId)));
        db.ParticipantMeals.AddRange(
            wanted
                .Where(id => !current.Any(m => m.MealTimeId == id))
                .Select(id => new ParticipantMeal { ParticipantId = participantId, MealTimeId = id })
        );

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // The meals were changed at the same time, or a meal time was deleted meanwhile
            return Conflict(new { message = "The meals were changed at the same time; try again" });
        }

        logger.LogInformation(
            "Meals of participant {ParticipantId} updated by user {UserId}",
            participantId,
            CurrentUserId()
        );

        return NoContent();
    }

    // Every food type with its meal times (ParticipantModel::getAllMad, Mad::getMadtider)
    public static async Task<IReadOnlyList<FoodTypeOptionDto>> LoadFoodTypeOptionsAsync(InfosysDbContext db) =>
        await db
            .FoodTypes.AsNoTracking()
            .OrderBy(f => f.Id)
            .Select(f => new FoodTypeOptionDto(
                f.Id,
                f.NameDanish,
                f.MealTimes.OrderBy(m => m.Id).Select(m => new MealTimeOptionDto(m.Id, m.Time)).ToList()
            ))
            .ToListAsync();

    private string? CurrentUserId() => User?.FindFirstValue(ClaimTypes.NameIdentifier);
}
