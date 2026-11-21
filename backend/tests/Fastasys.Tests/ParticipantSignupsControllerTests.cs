using Fastasys.ApiService.Controllers;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fastasys.Tests;

// Pins Infosys deltager/updatetilmelding (ParticipantModel::updateTilmeldinger) and the Aktivitetstilmeldinger row
// dialog (updateSchedule, updateSignupPriorities, removeParticipantSchedule, findConflictingSchedules), with the fixes
// agreed on 2026-11-13.
public class ParticipantSignupsControllerTests
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    // Wednesday 24 March 2027 at the given UTC hour
    private static DateTime Wednesday(int hour) => new(2027, 3, 24, hour, 0, 0, DateTimeKind.Utc);

    private static InfosysDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<InfosysDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var context = new InfosysDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static ParticipantSignupsController CreateController(InfosysDbContext db) =>
        new(db, NullLogger<ParticipantSignupsController>.Instance);

    private static T Ok<T>(ActionResult<T> result) =>
        Assert.IsAssignableFrom<T>(Assert.IsAssignableFrom<ObjectResult>(result.Result).Value);

    private static string? Reason(object? result) =>
        Assert.IsAssignableFrom<ObjectResult>(result).Value?.GetType().GetProperty("reason")?.GetValue(Assert.IsAssignableFrom<ObjectResult>(result).Value) as string;

    // An activity (id) with one schedule (id * 10) of 4 hours from the given start
    private static void AddGame(InfosysDbContext db, int id, string title, DateTime start, int gameMasters = 1, bool canSignUp = true) =>
        db.AddRange(
            new Activity { Id = id, Title = title, TitleEnglish = $"{title} (en)", GameMastersPerTeam = gameMasters, MaxPlayersPerTeam = 5, CanSignUp = canSignUp },
            new Schedule { Id = id * 10, ActivityId = id, StartTime = start, EndTime = start.AddHours(4) });

    private static ScheduleSignup Wish(int scheduleId, RunSeatRole role, int priority, int participantId = 1) =>
        new() { ParticipantId = participantId, ScheduleId = scheduleId, Role = role, Priority = priority };

    private static async Task<InfosysDbContext> SeedAsync(Action<InfosysDbContext> setup, bool locked = false)
    {
        var db = CreateDbContext();
        db.Participants.Add(new Participant { Id = 1, FirstName = "Testa", LastName = "Testesen", IsActivityLocked = locked });
        db.Participants.Add(new Participant { Id = 2, FirstName = "Anden", LastName = "Deltager" });
        setup(db);
        await db.SaveChangesAsync(Ct);
        db.ChangeTracker.Clear();
        return db;
    }

    private static async Task<List<(int ScheduleId, RunSeatRole Role, int Priority)>> WishesAsync(InfosysDbContext db, int participantId = 1) =>
        (await db.ScheduleSignups.AsNoTracking().Where(s => s.ParticipantId == participantId).ToListAsync(Ct))
        .Select(s => (s.ScheduleId, s.Role, s.Priority))
        .OrderBy(s => s.ScheduleId).ThenBy(s => s.Role)
        .ToList();

    // --- The edit page's data ---

    [Fact]
    public async Task Get_ListsTheWishesByStartAndTheActivitiesThatCanBeSignedUpFor()
    {
        using var db = await SeedAsync(d =>
        {
            AddGame(d, 2, "Sent", Wednesday(18));
            AddGame(d, 1, "Tidligt", Wednesday(8));
            AddGame(d, 3, "Lukket", Wednesday(12), canSignUp: false);
            AddGame(d, 4, "Uden spilledere", Wednesday(12), gameMasters: 0);
            AddGame(d, 5, "Lukket, ikke ønsket", Wednesday(12), canSignUp: false);
            // The Infosys signup's "SL + 1. prioritet": a game master wish with priority 0 and a player wish with 1
            d.ScheduleSignups.AddRange(Wish(20, RunSeatRole.Player, 2), Wish(10, RunSeatRole.Player, 1), Wish(10, RunSeatRole.GameMaster, 0), Wish(30, RunSeatRole.Player, 3));
        });

        var page = Ok(await CreateController(db).Get(1));

        Assert.Equal(
            new[]
            {
                new ParticipantActivitySignupDto(1, "Tidligt", "Tidligt (en)", 10, Wednesday(8), Wednesday(12), 0, RunSeatRole.GameMaster),
                new ParticipantActivitySignupDto(1, "Tidligt", "Tidligt (en)", 10, Wednesday(8), Wednesday(12), 1, RunSeatRole.Player),
                new ParticipantActivitySignupDto(3, "Lukket", "Lukket (en)", 30, Wednesday(12), Wednesday(16), 3, RunSeatRole.Player),
                new ParticipantActivitySignupDto(2, "Sent", "Sent (en)", 20, Wednesday(18), Wednesday(22), 2, RunSeatRole.Player),
            },
            page.Signups);
        // In id order. "Lukket" can't be signed up for, but a wish is on it, so the dialog can offer its schedules.
        Assert.Equal([1, 2, 3, 4], page.Activities.Select(a => a.Id));
        Assert.False(page.Activities.Single(a => a.Id == 3).CanSignUp);
        Assert.False(page.Activities.Single(a => a.Id == 4).HasGameMasters);
        Assert.Equal(new SignupScheduleOptionDto(10, Wednesday(8), Wednesday(12)), Assert.Single(page.Activities[0].Schedules));
    }

    [Fact]
    public async Task Get_RefusesAnUnknownOrActivityLockedParticipant()
    {
        using var locked = await SeedAsync(_ => { }, locked: true);

        var unknown = (await CreateController(locked).Get(99)).Result;
        var refused = (await CreateController(locked).Get(1)).Result;

        Assert.IsType<NotFoundObjectResult>(unknown);
        Assert.IsType<ConflictObjectResult>(refused);
        Assert.Equal("locked", Reason(refused));
    }

    // --- The edit page's "Opdater" ---

    [Fact]
    public async Task Update_ReplacesTheWishesAndSavesNewPrioritiesForKeptOnes()
    {
        // Fixed: Infosys kept a kept wish's old priority
        using var db = await SeedAsync(d =>
        {
            AddGame(d, 1, "Beholdes", Wednesday(8));
            AddGame(d, 2, "Fjernes", Wednesday(12));
            AddGame(d, 3, "Tilføjes", Wednesday(18));
            d.ScheduleSignups.AddRange(Wish(10, RunSeatRole.Player, 1), Wish(20, RunSeatRole.Player, 2), Wish(30, RunSeatRole.Player, 1, participantId: 2));
        });

        var result = await CreateController(db).Update(1, new UpdateParticipantSignupsDto(
        [
            new ParticipantSignupChoiceDto(10, RunSeatRole.Player, 3),
            new ParticipantSignupChoiceDto(30, RunSeatRole.GameMaster, 1),
            new ParticipantSignupChoiceDto(30, RunSeatRole.Player, 2),
        ]));

        Assert.IsType<NoContentResult>(result);
        Assert.Equal([(10, RunSeatRole.Player, 3), (30, RunSeatRole.Player, 2), (30, RunSeatRole.GameMaster, 1)], await WishesAsync(db));
        Assert.Equal([(30, RunSeatRole.Player, 1)], await WishesAsync(db, participantId: 2));
    }

    [Fact]
    public async Task Update_RefusesTheSameScheduleAndRoleTwice()
    {
        // Fixed: Infosys's page could add the same schedule twice
        using var db = await SeedAsync(d => AddGame(d, 1, "Spil", Wednesday(8)));

        var result = await CreateController(db).Update(1, new UpdateParticipantSignupsDto(
            [new ParticipantSignupChoiceDto(10, RunSeatRole.Player, 1), new ParticipantSignupChoiceDto(10, RunSeatRole.Player, 2)]));

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(await WishesAsync(db));
    }

    [Fact]
    public async Task Update_RefusesANewGameMasterWishForAnActivityWithoutGameMasters()
    {
        // Fixed: Infosys's DeltagereTilmeldinger::setTilmelding dropped it silently
        using var db = await SeedAsync(d =>
        {
            AddGame(d, 1, "Uden spilledere", Wednesday(8), gameMasters: 0);
            AddGame(d, 2, "Ændret siden", Wednesday(12), gameMasters: 0);
            d.ScheduleSignups.Add(Wish(20, RunSeatRole.GameMaster, 0));
        });
        var controller = CreateController(db);

        var refused = await controller.Update(1, new UpdateParticipantSignupsDto([new ParticipantSignupChoiceDto(10, RunSeatRole.GameMaster, 1)]));
        // A game master wish the participant already has is kept, even if the activity no longer has game masters
        var kept = await controller.Update(1, new UpdateParticipantSignupsDto([new ParticipantSignupChoiceDto(20, RunSeatRole.GameMaster, 0)]));

        Assert.Equal("noGameMasters", Reason(refused));
        Assert.IsType<NoContentResult>(kept);
        Assert.Equal([(20, RunSeatRole.GameMaster, 0)], await WishesAsync(db));
    }

    [Theory]
    [InlineData(RunSeatRole.Player, 0)]
    [InlineData(RunSeatRole.Player, 5)]
    [InlineData((RunSeatRole)7, 1)]
    public async Task Update_RefusesInvalidPrioritiesAndRoles(RunSeatRole role, int priority)
    {
        using var db = await SeedAsync(d => AddGame(d, 1, "Spil", Wednesday(8)));

        var result = await CreateController(db).Update(1, new UpdateParticipantSignupsDto([new ParticipantSignupChoiceDto(10, role, priority)]));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Update_RefusesAnActivityLockedParticipantAndAnUnknownSchedule()
    {
        using var lockedDb = await SeedAsync(
            d =>
            {
                AddGame(d, 1, "Spil", Wednesday(8));
                d.ScheduleSignups.Add(Wish(10, RunSeatRole.Player, 1));
            },
            locked: true);
        using var openDb = await SeedAsync(d => AddGame(d, 1, "Spil", Wednesday(8)));

        // Fixed: Infosys only hid the link; the page itself didn't check the lock
        var locked = await CreateController(lockedDb).Update(1, new UpdateParticipantSignupsDto([]));
        var unknown = await CreateController(openDb).Update(1, new UpdateParticipantSignupsDto([new ParticipantSignupChoiceDto(99, RunSeatRole.Player, 1)]));

        Assert.Equal("locked", Reason(locked));
        Assert.Single(await WishesAsync(lockedDb));
        Assert.IsType<BadRequestObjectResult>(unknown);
    }

    [Fact]
    public async Task Update_WithAnEmptyListRemovesEveryWish()
    {
        using var db = await SeedAsync(d =>
        {
            AddGame(d, 1, "Spil", Wednesday(8));
            d.ScheduleSignups.AddRange(Wish(10, RunSeatRole.Player, 1), Wish(10, RunSeatRole.GameMaster, 0));
        });

        await CreateController(db).Update(1, new UpdateParticipantSignupsDto([]));

        Assert.Empty(await WishesAsync(db));
    }

    // --- The row dialog ---

    [Fact]
    public async Task UpdateOne_ChangesThePriorityAndReportsNoConflict()
    {
        using var db = await SeedAsync(d =>
        {
            AddGame(d, 1, "Spil", Wednesday(8));
            d.ScheduleSignups.Add(Wish(10, RunSeatRole.Player, 1));
        });

        var result = Ok(await CreateController(db).UpdateOne(1, 10, RunSeatRole.Player, new UpdateSignupDto(10, RunSeatRole.Player, 3)));

        Assert.Equal("fine", result.Status);
        Assert.Equal([(10, RunSeatRole.Player, 3)], await WishesAsync(db));
    }

    [Fact]
    public async Task UpdateOne_GivesAGameMasterWishPriorityOne()
    {
        using var db = await SeedAsync(d =>
        {
            AddGame(d, 1, "Spil", Wednesday(8));
            d.ScheduleSignups.Add(Wish(10, RunSeatRole.Player, 3));
        });

        await CreateController(db).UpdateOne(1, 10, RunSeatRole.Player, new UpdateSignupDto(10, RunSeatRole.GameMaster, 3));

        Assert.Equal([(10, RunSeatRole.GameMaster, 1)], await WishesAsync(db));
    }

    [Fact]
    public async Task UpdateOne_MovesTheWishToAnotherSchedule()
    {
        using var db = await SeedAsync(d =>
        {
            AddGame(d, 1, "Spil", Wednesday(8));
            d.Schedules.Add(new Schedule { Id = 11, ActivityId = 1, StartTime = Wednesday(18), EndTime = Wednesday(22) });
            d.ScheduleSignups.Add(Wish(10, RunSeatRole.Player, 2));
        });

        await CreateController(db).UpdateOne(1, 10, RunSeatRole.Player, new UpdateSignupDto(11, RunSeatRole.Player, 2));

        Assert.Equal([(11, RunSeatRole.Player, 2)], await WishesAsync(db));
    }

    [Fact]
    public async Task UpdateOne_RefusesToMoveOntoAWishTheParticipantAlreadyHas()
    {
        // Infosys: "Kan ikke flytte afvikling - deltageren er allerede tilmeldt afviklingen der flyttes til"
        using var db = await SeedAsync(d =>
        {
            AddGame(d, 1, "Spil", Wednesday(8));
            d.Schedules.Add(new Schedule { Id = 11, ActivityId = 1, StartTime = Wednesday(18), EndTime = Wednesday(22) });
            d.ScheduleSignups.AddRange(Wish(10, RunSeatRole.Player, 1), Wish(11, RunSeatRole.Player, 2));
        });

        var result = (await CreateController(db).UpdateOne(1, 10, RunSeatRole.Player, new UpdateSignupDto(11, RunSeatRole.Player, 1))).Result;

        Assert.Equal("alreadySignedUp", Reason(result));
        Assert.Equal([(10, RunSeatRole.Player, 1), (11, RunSeatRole.Player, 2)], await WishesAsync(db));
    }

    [Fact]
    public async Task UpdateOne_ChangesOnlyTheWishWithThatRole()
    {
        // Fixed: with a game master and a player wish on the same schedule, Infosys's UPDATE hit both
        using var db = await SeedAsync(d =>
        {
            AddGame(d, 1, "Spil", Wednesday(8));
            d.ScheduleSignups.AddRange(Wish(10, RunSeatRole.GameMaster, 0), Wish(10, RunSeatRole.Player, 1));
        });

        await CreateController(db).UpdateOne(1, 10, RunSeatRole.Player, new UpdateSignupDto(10, RunSeatRole.Player, 2));

        Assert.Equal([(10, RunSeatRole.Player, 2), (10, RunSeatRole.GameMaster, 0)], await WishesAsync(db));
    }

    [Fact]
    public async Task UpdateOne_RefusesAMissingWishAndAnActivityLockedParticipant()
    {
        using var db = await SeedAsync(d =>
        {
            AddGame(d, 1, "Spil", Wednesday(8));
            d.ScheduleSignups.Add(Wish(10, RunSeatRole.GameMaster, 1, participantId: 2));
        });
        var controller = CreateController(db);

        var missing = (await controller.UpdateOne(1, 10, RunSeatRole.Player, new UpdateSignupDto(10, RunSeatRole.Player, 1))).Result;

        Assert.IsType<NotFoundObjectResult>(missing);
        Assert.Equal("notSignedUp", Reason(missing));

        var participant = await db.Participants.SingleAsync(p => p.Id == 2, Ct);
        participant.IsActivityLocked = true;
        await db.SaveChangesAsync(Ct);
        db.ChangeTracker.Clear();
        // Fixed: Infosys's dialog ignored the lock
        var locked = (await controller.UpdateOne(2, 10, RunSeatRole.GameMaster, new UpdateSignupDto(10, RunSeatRole.GameMaster, 1))).Result;

        Assert.Equal("locked", Reason(locked));
    }

    [Fact]
    public async Task UpdateOne_ReportsAConflictWhenTwoPlayerWishesAtTheSameTimeShareAPriority()
    {
        using var db = await SeedAsync(d =>
        {
            AddGame(d, 1, "Ændres", Wednesday(8));
            AddGame(d, 2, "Samtidig", Wednesday(10));
            AddGame(d, 3, "Senere", Wednesday(18));
            d.ScheduleSignups.AddRange(Wish(10, RunSeatRole.Player, 2), Wish(20, RunSeatRole.Player, 1), Wish(30, RunSeatRole.Player, 1));
        });

        var result = Ok(await CreateController(db).UpdateOne(1, 10, RunSeatRole.Player, new UpdateSignupDto(10, RunSeatRole.Player, 1)));

        Assert.Equal("conflict", result.Status);
        // The wishes at the same time, the later one is not included
        Assert.Equal([10, 20], result.Conflicts.Select(c => c.ScheduleId).Order());
    }

    [Fact]
    public async Task UpdateOne_LeavesTouchingSchedulesGameMasterWishesAndPriorityFourOutOfTheConflictCheck()
    {
        // Fixed: Infosys used BETWEEN, so schedules that only touch counted, and it counted game master wishes too
        using var db = await SeedAsync(d =>
        {
            AddGame(d, 1, "Ændres", Wednesday(8));
            AddGame(d, 2, "Lige efter", Wednesday(12));
            AddGame(d, 3, "Samtidig som spilleder", Wednesday(9));
            AddGame(d, 4, "Samtidig, prioritet 4", Wednesday(10));
            d.ScheduleSignups.AddRange(
                Wish(10, RunSeatRole.Player, 2),
                Wish(20, RunSeatRole.Player, 1),
                Wish(30, RunSeatRole.GameMaster, 1),
                Wish(40, RunSeatRole.Player, 4));
        });
        var controller = CreateController(db);

        var first = Ok(await controller.UpdateOne(1, 10, RunSeatRole.Player, new UpdateSignupDto(10, RunSeatRole.Player, 1)));
        var fourth = Ok(await controller.UpdateOne(1, 10, RunSeatRole.Player, new UpdateSignupDto(10, RunSeatRole.Player, 4)));

        Assert.Equal("fine", first.Status);
        Assert.Equal("fine", fourth.Status);
    }

    [Fact]
    public async Task UpdatePriorities_GivesThePlayerWishesOneTwoThreeAndThenFour()
    {
        using var db = await SeedAsync(d =>
        {
            for (var id = 1; id <= 5; id++) AddGame(d, id, $"Spil {id}", Wednesday(8));
            d.ScheduleSignups.AddRange(
                Wish(10, RunSeatRole.Player, 1),
                Wish(20, RunSeatRole.Player, 1),
                Wish(30, RunSeatRole.Player, 1),
                Wish(40, RunSeatRole.Player, 1),
                Wish(50, RunSeatRole.Player, 1),
                Wish(50, RunSeatRole.GameMaster, 0));
        });

        var result = await CreateController(db).UpdatePriorities(1, new SignupPrioritiesDto([50, 30, 10, 20, 40]));

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(
            [(10, RunSeatRole.Player, 3), (20, RunSeatRole.Player, 4), (30, RunSeatRole.Player, 2), (40, RunSeatRole.Player, 4), (50, RunSeatRole.Player, 1), (50, RunSeatRole.GameMaster, 0)],
            await WishesAsync(db));
    }

    [Fact]
    public async Task UpdatePriorities_RefusesAnEmptyListAndSchedulesWithoutAPlayerWish()
    {
        using var db = await SeedAsync(d =>
        {
            AddGame(d, 1, "Spil", Wednesday(8));
            d.ScheduleSignups.Add(Wish(10, RunSeatRole.GameMaster, 0));
        });
        var controller = CreateController(db);

        Assert.IsType<BadRequestObjectResult>(await controller.UpdatePriorities(1, new SignupPrioritiesDto([])));
        Assert.IsType<NotFoundObjectResult>(await controller.UpdatePriorities(1, new SignupPrioritiesDto([10])));
    }

    [Fact]
    public async Task Delete_RemovesOnlyTheWishWithThatRole()
    {
        // Fixed: Infosys removed "the first" wish it found on the schedule
        using var db = await SeedAsync(d =>
        {
            AddGame(d, 1, "Spil", Wednesday(8));
            d.ScheduleSignups.AddRange(Wish(10, RunSeatRole.GameMaster, 0), Wish(10, RunSeatRole.Player, 1));
        });
        var controller = CreateController(db);

        var deleted = await controller.Delete(1, 10, RunSeatRole.GameMaster);
        var missing = await controller.Delete(1, 10, RunSeatRole.GameMaster);

        Assert.IsType<NoContentResult>(deleted);
        Assert.IsType<NotFoundObjectResult>(missing);
        Assert.Equal([(10, RunSeatRole.Player, 1)], await WishesAsync(db));
    }

    [Fact]
    public async Task Delete_RefusesAnActivityLockedParticipant()
    {
        using var db = await SeedAsync(d =>
        {
            AddGame(d, 1, "Spil", Wednesday(8));
            d.ScheduleSignups.Add(Wish(10, RunSeatRole.Player, 1));
        }, locked: true);

        var result = await CreateController(db).Delete(1, 10, RunSeatRole.Player);

        Assert.Equal("locked", Reason(result));
        Assert.Single(await WishesAsync(db));
    }
}
