using Fastasys.ApiService.Controllers;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fastasys.Tests;

// Pins Infosys deltager/updateaktivitet (ParticipantController::updateAktiviteter, ParticipantModel::updateAktiviteter,
// editaktiviteter.phtml, imwedit.js setupAktivitet and ActivityController's getafviklinger/gethold/getholdneeds).
public class ParticipantSeatsControllerTests
{
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

    private static ParticipantSeatsController CreateController(InfosysDbContext db) =>
        new(db, NullLogger<ParticipantSeatsController>.Instance);

    private static T Ok<T>(ActionResult<T> result) =>
        Assert.IsAssignableFrom<T>(Assert.IsAssignableFrom<ObjectResult>(result.Result).Value);

    // An activity with one schedule and one run (room 1), for 2-3 players and 1 game master
    private static (Activity Activity, Schedule Schedule, Run Run) AddGame(
        InfosysDbContext db, int id, string title, DateTime start, bool timeExclusive = true, bool canSignUp = true)
    {
        var activity = new Activity
        {
            Id = id,
            Title = title,
            TitleEnglish = $"{title} (en)",
            MinPlayersPerTeam = 2,
            MaxPlayersPerTeam = 3,
            GameMastersPerTeam = 1,
            IsTimeExclusive = timeExclusive,
            CanSignUp = canSignUp,
        };
        var schedule = new Schedule { Id = id * 10, ActivityId = id, StartTime = start, EndTime = start.AddHours(4) };
        var run = new Run { Id = id * 100, ScheduleId = schedule.Id, Number = 1, RoomId = 1 };
        db.AddRange(activity, schedule, run);
        return (activity, schedule, run);
    }

    private static async Task<InfosysDbContext> SeedAsync(Action<InfosysDbContext> setup, Action<Participant>? participant = null)
    {
        var db = CreateDbContext();
        db.Rooms.Add(new Room { Id = 1, Name = "Lokale A1", Location = "Skolen" });
        var p = new Participant { Id = 1, FirstName = "Testa", LastName = "Testesen" };
        participant?.Invoke(p);
        db.Participants.Add(p);
        db.Participants.AddRange(
            new Participant { Id = 2, FirstName = "Anden", LastName = "Deltager" },
            new Participant { Id = 3, FirstName = "Tredje", LastName = "Deltager" },
            new Participant { Id = 4, FirstName = "Fjerde", LastName = "Deltager" });
        setup(db);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();
        return db;
    }

    private static RunSeat Seat(int runId, int seatNumber, int participantId, RunSeatRole role = RunSeatRole.Player) =>
        new() { RunId = runId, SeatNumber = seatNumber, ParticipantId = participantId, Role = role };

    [Fact]
    public async Task Get_IsNotFoundForAnUnknownParticipant()
    {
        using var db = await SeedAsync(_ => { });

        Assert.IsType<NotFoundObjectResult>((await CreateController(db).Get(99)).Result);
    }

    [Fact]
    public async Task Get_RefusesAnActivityLockedParticipant()
    {
        // Infosys: "Deltagerens aktiviteter kan ikke ændres." and back to the participant page
        using var db = await SeedAsync(_ => { }, p => p.IsActivityLocked = true);

        Assert.IsType<ConflictObjectResult>((await CreateController(db).Get(1)).Result);
    }

    [Fact]
    public async Task Get_ListsTheSeatsAndTheActivitiesThatCanBeSignedUpFor()
    {
        using var db = await SeedAsync(d =>
        {
            AddGame(d, 1, "Zombier", Wednesday(8));
            AddGame(d, 2, "abekatte", Wednesday(18));
            AddGame(d, 3, "Æblekrig", Wednesday(18));
            AddGame(d, 4, "Lukket", Wednesday(18), canSignUp: false);
            d.RunSeats.Add(Seat(100, 1, 1, RunSeatRole.GameMaster));
        });

        var page = Ok(await CreateController(db).Get(1));

        Assert.Equal(1, page.ParticipantId);
        Assert.Equal(
            new[] { new ParticipantSeatDto(100, 1, RunSeatRole.GameMaster, "Zombier", "Zombier (en)", Wednesday(8), Wednesday(12)) },
            page.Seats);
        // kan_tilmeldes = nej is left out. strcmp order: upper case, then lower case, then æøå
        Assert.Equal(["Zombier", "abekatte", "Æblekrig"], page.Activities.Select(a => a.Title));
        var zombier = page.Activities[0];
        Assert.Equal("Zombier (en)", zombier.TitleEnglish);
        var schedule = Assert.Single(zombier.Schedules);
        Assert.Equal(10, schedule.Id);
        // The participant is the run's only game master, so it needs no more; it can use 3 players
        Assert.Equal(new SeatRunOptionDto(100, 1, true, false), Assert.Single(schedule.Runs));
    }

    [Fact]
    public async Task Get_MarksTheRunsThatCanUseNoMorePlayers()
    {
        using var db = await SeedAsync(d =>
        {
            AddGame(d, 1, "Fyldt", Wednesday(8));
            d.RunSeats.AddRange(Seat(100, 1, 2), Seat(100, 2, 3), Seat(100, 3, 4));
        });

        var run = Ok(await CreateController(db).Get(1)).Activities.Single().Schedules.Single().Runs.Single();

        Assert.False(run.CanUsePlayers);
        Assert.True(run.NeedsGameMasters);
    }

    [Fact]
    public async Task Get_MarksTimeExclusiveSchedulesThatClashWithTheParticipantsSeats()
    {
        // Infosys ajaxGetAfviklinger: a schedule is disabled when its activity is time-exclusive and the participant
        // is busy then. Busy means a seat on a time-exclusive activity.
        using var db = await SeedAsync(d =>
        {
            AddGame(d, 1, "Optaget", Wednesday(10));
            AddGame(d, 2, "Samtidig", Wednesday(12));
            AddGame(d, 3, "Samtidig, ikke eksklusiv", Wednesday(12), timeExclusive: false);
            AddGame(d, 4, "Lige efter", Wednesday(14));
            AddGame(d, 5, "Hyggeligt", Wednesday(18), timeExclusive: false);
            AddGame(d, 6, "Under hyggen", Wednesday(19));
            d.RunSeats.AddRange(Seat(100, 1, 1), Seat(500, 1, 1));
        });

        var busy = Ok(await CreateController(db).Get(1)).Activities
            .ToDictionary(a => a.Title, a => a.Schedules.Single().IsBusy);

        Assert.True(busy["Optaget"]);
        Assert.True(busy["Samtidig"]);
        Assert.False(busy["Samtidig, ikke eksklusiv"]);
        // 14:00 is when the seat at 10:00-14:00 ends; periods that only touch don't clash
        Assert.False(busy["Lige efter"]);
        // A seat on an activity that isn't time-exclusive doesn't make the participant busy
        Assert.False(busy["Under hyggen"]);
    }

    [Fact]
    public async Task Get_CountsADesignersOwnGameAsBusyButDoesNotListIt()
    {
        // Infosys isBusyBetween goes through getPladser, so a designer's rows count; they are not seats, so they can't
        // be edited
        using var db = await SeedAsync(
            d =>
            {
                AddGame(d, 1, "Mit spil", Wednesday(10));
                AddGame(d, 2, "Samtidig", Wednesday(12));
            },
            p => { p.OrganizerAreaId = OrganizerArea.BoardGameDesignerId; p.GameActivityId = 1; });

        var page = Ok(await CreateController(db).Get(1));

        Assert.Empty(page.Seats);
        Assert.True(page.Activities.Single(a => a.Title == "Samtidig").Schedules.Single().IsBusy);
    }

    [Fact]
    public async Task Update_ReplacesTheSeatsWithTheList()
    {
        using var db = await SeedAsync(d =>
        {
            AddGame(d, 1, "Beholdes", Wednesday(8));
            AddGame(d, 2, "Fjernes", Wednesday(12));
            AddGame(d, 3, "Tilføjes", Wednesday(18));
            d.RunSeats.AddRange(Seat(100, 1, 1), Seat(200, 1, 1), Seat(300, 1, 2), Seat(300, 3, 3));
        });

        var result = await CreateController(db).Update(1, new UpdateParticipantSeatsDto(
        [
            new ParticipantSeatChoiceDto(100, RunSeatRole.Player),
            new ParticipantSeatChoiceDto(300, RunSeatRole.Player),
        ]));

        Assert.IsType<NoContentResult>(result);
        var seats = await db.RunSeats.AsNoTracking().Where(s => s.ParticipantId == 1).OrderBy(s => s.RunId).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal([100, 300], seats.Select(s => s.RunId));
        // The first free seat number on run 300, where 1 and 3 are taken
        Assert.Equal(2, seats[1].SeatNumber);
        Assert.Equal(RunSeatRole.Player, seats[1].Role);
    }

    [Fact]
    public async Task Update_KeepsTheRoleOfASeatThatIsKept()
    {
        // Legacy: updateAktiviteter matches on hold_id only, so a kept seat keeps its role
        using var db = await SeedAsync(d =>
        {
            AddGame(d, 1, "Spil", Wednesday(8));
            d.RunSeats.Add(Seat(100, 1, 1, RunSeatRole.Player));
        });

        await CreateController(db).Update(1, new UpdateParticipantSeatsDto([new ParticipantSeatChoiceDto(100, RunSeatRole.GameMaster)]));

        var seat = await db.RunSeats.AsNoTracking().SingleAsync(s => s.ParticipantId == 1, TestContext.Current.CancellationToken);
        Assert.Equal(RunSeatRole.Player, seat.Role);
    }

    [Fact]
    public async Task Update_SkipsARunWithoutRoomForTheRole()
    {
        // Legacy: Pladser::setDeltagerPlads refuses, and Infosys still says "Deltageren blev opdateret."
        using var db = await SeedAsync(d =>
        {
            AddGame(d, 1, "Fyldt", Wednesday(8));
            AddGame(d, 2, "Ledig", Wednesday(12));
            d.RunSeats.AddRange(Seat(100, 1, 2), Seat(100, 2, 3), Seat(100, 3, 4, RunSeatRole.GameMaster));
        });

        var result = await CreateController(db).Update(1, new UpdateParticipantSeatsDto(
        [
            new ParticipantSeatChoiceDto(100, RunSeatRole.GameMaster),
            new ParticipantSeatChoiceDto(200, RunSeatRole.GameMaster),
        ]));

        Assert.IsType<NoContentResult>(result);
        var seat = await db.RunSeats.AsNoTracking().SingleAsync(s => s.ParticipantId == 1, TestContext.Current.CancellationToken);
        Assert.Equal(200, seat.RunId);
    }

    [Fact]
    public async Task Update_WithAnEmptyListRemovesEverySeat()
    {
        using var db = await SeedAsync(d =>
        {
            AddGame(d, 1, "Spil", Wednesday(8));
            d.RunSeats.AddRange(Seat(100, 1, 1), Seat(100, 2, 2));
        });

        await CreateController(db).Update(1, new UpdateParticipantSeatsDto([]));

        Assert.Equal([2], await db.RunSeats.AsNoTracking().Select(s => s.ParticipantId).ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Update_RefusesAnActivityLockedParticipant()
    {
        using var db = await SeedAsync(
            d =>
            {
                AddGame(d, 1, "Spil", Wednesday(8));
                d.RunSeats.Add(Seat(100, 1, 1));
            },
            p => p.IsActivityLocked = true);

        var result = await CreateController(db).Update(1, new UpdateParticipantSeatsDto([]));

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(1, await db.RunSeats.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Update_RefusesUnknownRunsRolesAndRunsListedTwice()
    {
        using var db = await SeedAsync(d => AddGame(d, 1, "Spil", Wednesday(8)));
        var controller = CreateController(db);

        var unknownRun = await controller.Update(1, new UpdateParticipantSeatsDto([new ParticipantSeatChoiceDto(999, RunSeatRole.Player)]));
        var unknownRole = await controller.Update(1, new UpdateParticipantSeatsDto([new ParticipantSeatChoiceDto(100, (RunSeatRole)7)]));
        var twice = await controller.Update(1, new UpdateParticipantSeatsDto(
            [new ParticipantSeatChoiceDto(100, RunSeatRole.Player), new ParticipantSeatChoiceDto(100, RunSeatRole.GameMaster)]));
        var unknownParticipant = await controller.Update(99, new UpdateParticipantSeatsDto([]));

        Assert.IsType<BadRequestObjectResult>(unknownRun);
        Assert.IsType<BadRequestObjectResult>(unknownRole);
        Assert.IsType<BadRequestObjectResult>(twice);
        Assert.IsType<NotFoundObjectResult>(unknownParticipant);
        Assert.Equal(0, await db.RunSeats.CountAsync(TestContext.Current.CancellationToken));
    }
}
