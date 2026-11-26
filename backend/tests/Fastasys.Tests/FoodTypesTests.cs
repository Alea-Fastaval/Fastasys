using System.Net.Http.Headers;
using System.Net.Http.Json;
using Fastasys.ApiService.Controllers;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fastasys.Tests;

// Pins Infosys's Mad pages (FoodController: Mad-typer, one type, create, edit, delete, Statistik) and the meals on the
// participant (deltagere_madtider), ported on 2026-11-25 with the five agreed bug fixes.
public class FoodTypesTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Fastaval 2027: Wednesday 24 to Monday 29 March. Copenhagen is UTC+1 until the clocks go forward on Sunday 28.
    private static readonly ConventionOptions Convention = new()
    {
        Start = new DateOnly(2027, 3, 24),
        End = new DateOnly(2027, 3, 29),
    };

    private static DateTime Utc(int day, int hour) => new(2027, 3, day, hour, 0, 0, DateTimeKind.Utc);

    private static InfosysDbContext CreateDbContext()
    {
        var context = new InfosysDbContext(
            new DbContextOptionsBuilder<InfosysDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options
        );
        context.Database.EnsureCreated();
        return context;
    }

    private static FoodTypesController Foods(InfosysDbContext db) =>
        new(db, Options.Create(Convention), NullLogger<FoodTypesController>.Instance);

    private static ParticipantMealsController Meals(InfosysDbContext db) =>
        new(db, NullLogger<ParticipantMealsController>.Instance);

    private static CreateFoodTypeDto ValidCreate() =>
        new()
        {
            NameDanish = "Aftensmad - Pizza",
            NameEnglish = "Dinner - Pizza",
            Price = 68,
            MealTimes =
            [
                new MealTimeFieldsDto(null, Utc(24, 16), "Med salat", "With salad"),
                new MealTimeFieldsDto(null, Utc(25, 16), null, null),
            ],
        };

    private static T Value<T>(ActionResult<T> result) =>
        Assert.IsAssignableFrom<T>(Assert.IsAssignableFrom<ObjectResult>(result.Result).Value);

    // Food type 1 "Aftensmad" with meal times 10 (Wednesday) and 11 (Thursday), food type 2 "Morgenmad" with 20, and
    // participants 1-3
    private static async Task SeedAsync(
        InfosysDbContext db,
        params (int Participant, int MealTime, bool Received)[] meals
    )
    {
        var dinner = new FoodType
        {
            Id = 1,
            NameDanish = "Aftensmad",
            Price = 68,
        };
        dinner.MealTimes.Add(new MealTime { Id = 10, Time = Utc(24, 16) });
        dinner.MealTimes.Add(new MealTime { Id = 11, Time = Utc(25, 16) });
        var breakfast = new FoodType
        {
            Id = 2,
            NameDanish = "Morgenmad",
            Price = 45,
        };
        breakfast.MealTimes.Add(new MealTime { Id = 20, Time = Utc(25, 7) });
        db.FoodTypes.AddRange(dinner, breakfast);
        foreach (var id in new[] { 1, 2, 3 })
            db.Participants.Add(
                new Participant
                {
                    Id = id,
                    FirstName = "Deltager",
                    LastName = id.ToString(),
                }
            );
        db.ParticipantMeals.AddRange(
            meals.Select(m => new ParticipantMeal
            {
                ParticipantId = m.Participant,
                MealTimeId = m.MealTime,
                IsReceived = m.Received,
            })
        );
        await db.SaveChangesAsync(Ct);
        db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task GetAll_ListsEveryTypeInIdOrder()
    {
        // Infosys FoodModel::getAllFood has no order; hidden types are listed too
        using var db = CreateDbContext();
        await SeedAsync(db);
        (await db.FoodTypes.SingleAsync(f => f.Id == 2, Ct)).IsHidden = true;
        await db.SaveChangesAsync(Ct);

        var types = Value(await Foods(db).GetAll()).ToList();

        Assert.Equal(
            [
                new FoodTypeSummaryDto(1, "Aftensmad", "", 68, false),
                new FoodTypeSummaryDto(2, "Morgenmad", "", 45, true),
            ],
            types
        );
    }

    [Fact]
    public async Task Get_ShowsTheMealTimesWithHowManyOrderedThem()
    {
        using var db = CreateDbContext();
        await SeedAsync(db, (1, 10, false), (2, 10, true));

        var food = Value(await Foods(db).Get(1));

        Assert.Equal("Aftensmad", food.NameDanish);
        Assert.Equal([10, 11], food.MealTimes.Select(m => m.Id));
        Assert.Equal([2, 0], food.MealTimes.Select(m => m.OrderCount));
        Assert.IsType<NotFoundObjectResult>((await Foods(db).Get(9)).Result);
    }

    [Fact]
    public async Task MealTimesAreListedByDayAndTime_NotInTheOrderTheyWereAdded()
    {
        // Infosys listed them in the order they were added (Mad::getMadtider has no order); changed 2026-11-26
        using var db = CreateDbContext();
        var lunch = new FoodType { Id = 5, NameDanish = "Frokost" };
        lunch.MealTimes.Add(new MealTime { Id = 30, Time = Utc(26, 11) });
        lunch.MealTimes.Add(new MealTime { Id = 31, Time = Utc(24, 11) });
        lunch.MealTimes.Add(new MealTime { Id = 32, Time = Utc(25, 11) });
        db.FoodTypes.Add(lunch);
        db.Participants.Add(
            new Participant
            {
                Id = 1,
                FirstName = "Deltager",
                LastName = "1",
            }
        );
        await db.SaveChangesAsync(Ct);
        db.ChangeTracker.Clear();

        Assert.Equal([31, 32, 30], Value(await Foods(db).Get(5)).MealTimes.Select(m => m.Id));
        Assert.Equal([31, 32, 30], Value(await Foods(db).GetStats()).Single().MealTimes.Select(m => m.Id));
        Assert.Equal([31, 32, 30], Value(await Meals(db).Get(1)).FoodTypes.Single().MealTimes.Select(m => m.Id));
    }

    [Fact]
    public async Task Create_StoresTheTypeAndItsMealTimes()
    {
        using var db = CreateDbContext();

        var result = await Foods(db).Create(ValidCreate() with { IsHidden = true });

        var created = Assert.IsType<FoodTypeDto>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        Assert.Equal("Aftensmad - Pizza", created.NameDanish);
        Assert.Equal("Dinner - Pizza", created.NameEnglish);
        Assert.Equal(68, created.Price);
        Assert.True(created.IsHidden);
        Assert.Equal([Utc(24, 16), Utc(25, 16)], created.MealTimes.Select(m => m.Time));
        Assert.Equal(["Med salat", ""], created.MealTimes.Select(m => m.DescriptionDanish));
        Assert.Equal(["With salad", ""], created.MealTimes.Select(m => m.DescriptionEnglish));
    }

    [Fact]
    public async Task Create_RefusesANameThatIsTaken()
    {
        // Infosys mad.kategori is unique
        using var db = CreateDbContext();
        await SeedAsync(db);

        var result = await Foods(db).Create(ValidCreate() with { NameDanish = "Aftensmad" });

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    public static TheoryData<CreateFoodTypeDto, bool> Inputs =>
        new()
        {
            { ValidCreate(), true },
            { ValidCreate() with { MealTimes = null }, true },
            { ValidCreate() with { NameEnglish = null }, true },
            // Infosys: empty($post->kategori), which is also true for "0"
            { ValidCreate() with { NameDanish = null }, false },
            { ValidCreate() with { NameDanish = "" }, false },
            { ValidCreate() with { NameDanish = "0" }, false },
            // mad.kategori and title_en are varchar(100)
            { ValidCreate() with { NameDanish = new string('x', 100) }, true },
            { ValidCreate() with { NameDanish = new string('x', 101) }, false },
            { ValidCreate() with { NameEnglish = new string('x', 101) }, false },
            // Infosys took any number; whole kroner, 0 or more (fixed 2026-11-25). 0 is a free meal.
            { ValidCreate() with { Price = 0 }, true },
            { ValidCreate() with { Price = -1 }, false },
            { ValidCreate() with { Price = null }, false },
            // A meal time needs a time; the day is one of the convention's days (agreed 2026-11-25)
            { ValidCreate() with { MealTimes = [new MealTimeFieldsDto(null, null, null, null)] }, false },
            // The day counts in Copenhagen: 23:00 UTC on Wednesday 24 is 00:00 on Thursday 25 there
            { ValidCreate() with { MealTimes = [new MealTimeFieldsDto(null, Utc(24, 23), null, null)] }, true },
            // 22:00 UTC on Tuesday 23 is 23:00 on Tuesday in Copenhagen, the day before the convention
            { ValidCreate() with { MealTimes = [new MealTimeFieldsDto(null, Utc(23, 22), null, null)] }, false },
            // Monday 29 is the last day; Tuesday 30 isn't
            { ValidCreate() with { MealTimes = [new MealTimeFieldsDto(null, Utc(29, 10), null, null)] }, true },
            { ValidCreate() with { MealTimes = [new MealTimeFieldsDto(null, Utc(30, 10), null, null)] }, false },
            // madtider.description_da / _en are varchar(128)
            {
                ValidCreate() with
                {
                    MealTimes = [new MealTimeFieldsDto(null, Utc(24, 16), new string('x', 129), null)],
                },
                false
            },
            // Infosys madtider: unique (mad_id, dato)
            {
                ValidCreate() with
                {
                    MealTimes =
                    [
                        new MealTimeFieldsDto(null, Utc(24, 16), null, null),
                        new MealTimeFieldsDto(null, Utc(24, 16), null, null),
                    ],
                },
                false
            },
        };

    [Theory]
    [MemberData(nameof(Inputs))]
    public void Validate_PinsTheFormRules(CreateFoodTypeDto dto, bool isValid)
    {
        var (valid, error) = FoodTypesController.Validate(dto, Convention, new Dictionary<int, DateTime>());

        Assert.Equal(isValid, valid);
        Assert.Equal(isValid, error.Length == 0);
    }

    [Fact]
    public void Validate_LetsAMealTimeKeepATimeOutsideTheConvention_AndChecksNoDayWithoutTheDates()
    {
        var february = ValidCreate() with
        {
            MealTimes = [new MealTimeFieldsDto(5, new DateTime(2027, 2, 3, 12, 0, 0, DateTimeKind.Utc), null, null)],
        };
        var current = new Dictionary<int, DateTime> { [5] = new DateTime(2027, 2, 3, 12, 0, 0, DateTimeKind.Utc) };

        Assert.True(FoodTypesController.Validate(february, Convention, current).IsValid);
        Assert.False(FoodTypesController.Validate(february, Convention, new Dictionary<int, DateTime>()).IsValid);
        Assert.True(
            FoodTypesController.Validate(february, new ConventionOptions(), new Dictionary<int, DateTime>()).IsValid
        );
    }

    [Fact]
    public async Task Update_RemovesOnlyTheRemovedMealTime()
    {
        // Infosys compared only the first meal time, so removing the first one deleted every other one too (fixed
        // 2026-11-25). Here meal time 10 is removed, 11 changes its time and a new one is added.
        using var db = CreateDbContext();
        await SeedAsync(db);
        var update = new UpdateFoodTypeDto
        {
            NameDanish = "Aftensmad",
            NameEnglish = "Dinner",
            Price = 70,
            MealTimes =
            [
                new MealTimeFieldsDto(11, Utc(25, 17), "Senere", "Later"),
                new MealTimeFieldsDto(null, Utc(26, 16), null, null),
            ],
        };

        var updated = Value(await Foods(db).Update(1, update));

        Assert.Equal(70, updated.Price);
        Assert.Equal("Dinner", updated.NameEnglish);
        Assert.Equal(2, updated.MealTimes.Count);
        Assert.Equal(11, updated.MealTimes[0].Id);
        Assert.Equal(Utc(25, 17), updated.MealTimes[0].Time);
        Assert.Equal("Senere", updated.MealTimes[0].DescriptionDanish);
        Assert.Equal(Utc(26, 16), updated.MealTimes[1].Time);
        Assert.False(await db.MealTimes.AnyAsync(m => m.Id == 10, Ct));
        // The other type's meal time is untouched
        Assert.True(await db.MealTimes.AnyAsync(m => m.Id == 20, Ct));
    }

    [Fact]
    public async Task Update_SkipsAMealTimeOfAnotherType_AndRefusesToRemoveAnOrderedOne()
    {
        using var db = CreateDbContext();
        await SeedAsync(db, (1, 10, false));
        var update = new UpdateFoodTypeDto
        {
            NameDanish = "Aftensmad",
            Price = 68,
            MealTimes =
            [
                new MealTimeFieldsDto(10, Utc(24, 16), null, null),
                new MealTimeFieldsDto(11, Utc(25, 16), null, null),
                // Morgenmad's meal time: skipped, like Infosys
                new MealTimeFieldsDto(20, Utc(27, 9), null, null),
            ],
        };

        Value(await Foods(db).Update(1, update));
        Assert.Equal(Utc(25, 7), (await db.MealTimes.SingleAsync(m => m.Id == 20, Ct)).Time);

        // Meal time 10 is ordered, so it can't be removed (Infosys's foreign key gave an error page)
        db.ChangeTracker.Clear();
        var refused = await Foods(db)
            .Update(1, update with { MealTimes = [new MealTimeFieldsDto(11, Utc(25, 16), null, null)] });
        Assert.Contains("mealTimeOrdered", Assert.IsType<ConflictObjectResult>(refused.Result).Value!.ToString());
        Assert.Equal(2, await db.MealTimes.CountAsync(m => m.FoodTypeId == 1, Ct));
    }

    [Fact]
    public async Task Delete_DeletesTheMealTimesWithTheType_UnlessOneIsOrdered()
    {
        // Infosys's foreign key made deleting a food type with meal times an error page (fixed 2026-11-25)
        using var db = CreateDbContext();
        await SeedAsync(db, (1, 10, false));

        Assert.Contains("inUse", Assert.IsType<ConflictObjectResult>(await Foods(db).Delete(1)).Value!.ToString());
        Assert.IsType<OkObjectResult>(await Foods(db).Delete(2));
        Assert.IsType<NotFoundObjectResult>(await Foods(db).Delete(2));

        Assert.Equal([1], db.FoodTypes.Select(f => f.Id));
        Assert.Equal([10, 11], db.MealTimes.OrderBy(m => m.Id).Select(m => m.Id));
    }

    [Fact]
    public async Task GetStats_CountsOrderedAndHandedOutMeals_WithoutAnnulledParticipants()
    {
        // Infosys FoodModel::getFoodStats counts annulled participants too; they are left out (agreed 2026-11-25)
        using var db = CreateDbContext();
        await SeedAsync(db, (1, 10, true), (2, 10, false), (3, 10, true), (1, 20, false));
        (await db.Participants.SingleAsync(p => p.Id == 3, Ct)).IsAnnulled = true;
        await db.SaveChangesAsync(Ct);
        db.ChangeTracker.Clear();

        var stats = Value(await Foods(db).GetStats()).ToList();

        Assert.Equal(["Aftensmad", "Morgenmad"], stats.Select(s => s.NameDanish));
        Assert.Equal(2, stats[0].Total);
        Assert.Equal(
            [new FoodStatsMealTimeDto(10, Utc(24, 16), 2, 1), new FoodStatsMealTimeDto(11, Utc(25, 16), 0, 0)],
            stats[0].MealTimes
        );
        Assert.Equal(1, stats[1].Total);
    }

    [Fact]
    public async Task ParticipantMeals_ListsTheMealsByTime_AndEveryFoodType()
    {
        using var db = CreateDbContext();
        await SeedAsync(db, (1, 11, false), (1, 20, true));

        var page = Value(await Meals(db).Get(1));

        Assert.Equal(
            [
                new ParticipantMealRowDto(20, "Morgenmad", Utc(25, 7), true),
                new ParticipantMealRowDto(11, "Aftensmad", Utc(25, 16), false),
            ],
            page.Meals
        );
        Assert.Equal([1, 2], page.FoodTypes.Select(f => f.Id));
        Assert.Equal([10, 11], page.FoodTypes[0].MealTimes.Select(m => m.Id));
        Assert.IsType<NotFoundObjectResult>((await Meals(db).Get(9)).Result);
    }

    [Fact]
    public async Task ParticipantMeals_UpdateKeepsHandedOutMeals()
    {
        // Infosys deleted all the participant's meals and added the posted ones back, which reset "Udleveret" (fixed
        // 2026-11-25). An unknown id is skipped like in Infosys; a meal listed twice is kept once (Infosys crashed).
        using var db = CreateDbContext();
        await SeedAsync(db, (1, 10, true), (1, 11, false), (2, 10, true));

        var result = await Meals(db).Update(1, new UpdateParticipantMealsDto([10, 20, 20, 999]));

        Assert.IsType<NoContentResult>(result);
        var meals = await db
            .ParticipantMeals.Where(m => m.ParticipantId == 1)
            .OrderBy(m => m.MealTimeId)
            .ToListAsync(Ct);
        Assert.Equal([10, 20], meals.Select(m => m.MealTimeId));
        Assert.Equal([true, false], meals.Select(m => m.IsReceived));
        Assert.True((await db.ParticipantMeals.SingleAsync(m => m.ParticipantId == 2, Ct)).IsReceived);

        db.ChangeTracker.Clear();
        Assert.IsType<NoContentResult>(await Meals(db).Update(1, new UpdateParticipantMealsDto(null)));
        Assert.Empty(db.ParticipantMeals.Where(m => m.ParticipantId == 1));
        Assert.IsType<NotFoundObjectResult>(await Meals(db).Update(9, new UpdateParticipantMealsDto([10])));
    }

    private async Task<HttpClient> ClientAsync(string? privilege)
    {
        if (privilege == null)
            return factory.CreateClient();
        var username = $"food-{Guid.NewGuid():N}";
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InfosysDbContext>();
        var role = new Role { Name = username };
        role.RolePrivileges.Add(
            new RolePrivilege
            {
                Privilege = new Privilege { Key = privilege, Name = privilege },
            }
        );
        var user = new User
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password"),
            IsActive = true,
        };
        user.UserRoles.Add(new UserRole { Role = role });
        db.Users.Add(user);
        await db.SaveChangesAsync(Ct);
        var auth = await scope.ServiceProvider.GetRequiredService<IAuthService>().LoginAsync(new(username, "password"));
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized)]
    [InlineData("participant_edit", HttpStatusCode.Forbidden, HttpStatusCode.Forbidden, HttpStatusCode.Forbidden)]
    [InlineData("food_participant_edit", HttpStatusCode.Forbidden, HttpStatusCode.Forbidden, HttpStatusCode.Forbidden)]
    [InlineData("food_stats_view", HttpStatusCode.Forbidden, HttpStatusCode.OK, HttpStatusCode.Forbidden)]
    [InlineData("food_view", HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.Forbidden)]
    [InlineData("food_edit", HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.Created)]
    public async Task FoodTypes_FoodViewMayLook_FoodEditMayChange_FoodStatsViewMaySeeTheStatistics(
        string? privilege,
        HttpStatusCode look,
        HttpStatusCode stats,
        HttpStatusCode create
    )
    {
        using var client = await ClientAsync(privilege);

        Assert.Equal(look, (await client.GetAsync("/api/food-types", Ct)).StatusCode);
        Assert.Equal(stats, (await client.GetAsync("/api/food-types/stats", Ct)).StatusCode);
        var dto = ValidCreate() with { NameDanish = $"Mad {Guid.NewGuid():N}" };
        Assert.Equal(create, (await client.PostAsJsonAsync("/api/food-types", dto, Ct)).StatusCode);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("participant_edit", HttpStatusCode.Forbidden)]
    [InlineData("food_edit", HttpStatusCode.Forbidden)]
    [InlineData("food_participant_edit", HttpStatusCode.NotFound)]
    public async Task ParticipantMeals_NeedFoodParticipantEdit(string? privilege, HttpStatusCode expected)
    {
        // An unknown participant: 404 once the user may change meals at all
        using var client = await ClientAsync(privilege);

        Assert.Equal(expected, (await client.GetAsync("/api/participants/987654/meals", Ct)).StatusCode);
        Assert.Equal(
            expected,
            (
                await client.PutAsJsonAsync("/api/participants/987654/meals", new UpdateParticipantMealsDto([]), Ct)
            ).StatusCode
        );
    }
}
