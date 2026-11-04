using Fastasys.ApiService.Controllers;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fastasys.Tests;

// Pins the Infosys behaviour of hold/visalle, hold/vis, hold/ajaxcreategroup, hold/edit, hold/slet,
// hold/ajaxdeletegroup and groups/scheduleparticipant.
public class RunsControllerTests
{
    // Wednesday 24 March 2027, 10:00-14:00 in Copenhagen
    private static readonly DateTime Start = new(2027, 3, 24, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = Start.AddHours(4);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static InfosysDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<InfosysDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var context = new InfosysDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static RunsController CreateController(InfosysDbContext db) =>
        new(db, NullLogger<RunsController>.Instance);

    private static Activity NewActivity(string title = "Testscenarie", bool fillsRoom = true) => new()
    {
        Title = title,
        MinPlayersPerTeam = 3,
        MaxPlayersPerTeam = 5,
        GameMastersPerTeam = 1,
        IsRoomExclusive = fillsRoom
    };

    private static async Task<Schedule> AddScheduleAsync(InfosysDbContext db, Activity? activity = null, DateTime? start = null, DateTime? end = null)
    {
        var schedule = new Schedule { Activity = activity ?? NewActivity(), StartTime = start ?? Start, EndTime = end ?? End };
        db.Schedules.Add(schedule);
        await db.SaveChangesAsync(Ct);
        return schedule;
    }

    private static async Task<List<Room>> AddRoomsAsync(InfosysDbContext db, int count)
    {
        var rooms = Enumerable.Range(1, count).Select(i => new Room { Name = $"Lokale {i}", Location = $"Bygning {i}" }).ToList();
        db.Rooms.AddRange(rooms);
        await db.SaveChangesAsync(Ct);
        return rooms;
    }

    private static async Task<Run> AddRunAsync(InfosysDbContext db, Schedule schedule, Room room, int number = 1)
    {
        var run = new Run { ScheduleId = schedule.Id, RoomId = room.Id, Number = number };
        db.Runs.Add(run);
        await db.SaveChangesAsync(Ct);
        return run;
    }

    private static async Task<Participant> AddParticipantAsync(InfosysDbContext db, string firstName = "Test", string lastName = "Deltager", string nickname = "")
    {
        var participant = new Participant { FirstName = firstName, LastName = lastName, Nickname = nickname };
        db.Participants.Add(participant);
        await db.SaveChangesAsync(Ct);
        return participant;
    }

    private static async Task AddSeatAsync(InfosysDbContext db, Run run, Participant participant, RunSeatRole role, int seatNumber)
    {
        db.RunSeats.Add(new RunSeat { RunId = run.Id, ParticipantId = participant.Id, Role = role, SeatNumber = seatNumber });
        await db.SaveChangesAsync(Ct);
    }

    private static async Task<Run> AddRunWithSeatsAsync(InfosysDbContext db, int players, int gameMasters, Activity? activity = null)
    {
        var schedule = await AddScheduleAsync(db, activity);
        var room = (await AddRoomsAsync(db, 1))[0];
        var run = await AddRunAsync(db, schedule, room);
        var seat = 1;
        for (var i = 0; i < gameMasters; i++)
        {
            await AddSeatAsync(db, run, await AddParticipantAsync(db, "Spil", $"Leder {i}"), RunSeatRole.GameMaster, seat++);
        }
        for (var i = 0; i < players; i++)
        {
            await AddSeatAsync(db, run, await AddParticipantAsync(db, "Spil", $"Ler {i}"), RunSeatRole.Player, seat++);
        }
        return run;
    }

    private static T Value<T>(ActionResult<T> result) where T : class
    {
        if (result.Value != null) return result.Value;
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        return Assert.IsType<T>(objectResult.Value);
    }

    // ---- Create (hold/ajaxcreategroup) ----

    [Fact]
    public async Task Create_FirstRun_GetsNumberOne_AndFirstFreeRoom()
    {
        using var db = CreateDbContext();
        var schedule = await AddScheduleAsync(db);
        var rooms = await AddRoomsAsync(db, 3);

        var result = await CreateController(db).Create(new CreateRunDto(schedule.Id));

        Assert.IsType<CreatedAtActionResult>(result.Result);
        var run = Value(result);
        Assert.Equal(1, run.Number);
        Assert.Equal(rooms[0].Id, run.RoomId);
        Assert.Equal(schedule.Id, run.ScheduleId);
        Assert.Empty(run.Seats);
    }

    [Fact]
    public async Task Create_NumberIsHighestPlusOne_GapsAreNotReused()
    {
        // Infosys Hold::getNextHoldnummer uses MAX(holdnummer) + 1
        using var db = CreateDbContext();
        var activity = NewActivity(fillsRoom: false);
        var schedule = await AddScheduleAsync(db, activity);
        var room = (await AddRoomsAsync(db, 1))[0];
        await AddRunAsync(db, schedule, room, number: 1);
        await AddRunAsync(db, schedule, room, number: 3);

        var run = Value(await CreateController(db).Create(new CreateRunDto(schedule.Id)));

        Assert.Equal(4, run.Number);
    }

    [Fact]
    public async Task Create_NumbersAreCountedPerSchedule()
    {
        using var db = CreateDbContext();
        var activity = NewActivity();
        var first = await AddScheduleAsync(db, activity);
        var second = await AddScheduleAsync(db, activity, Start.AddDays(1), End.AddDays(1));
        var rooms = await AddRoomsAsync(db, 2);
        await AddRunAsync(db, first, rooms[0], number: 1);
        await AddRunAsync(db, first, rooms[1], number: 2);

        var run = Value(await CreateController(db).Create(new CreateRunDto(second.Id)));

        Assert.Equal(1, run.Number);
    }

    [Fact]
    public async Task Create_SkipsRoomsBookedByOverlappingRuns()
    {
        using var db = CreateDbContext();
        var rooms = await AddRoomsAsync(db, 2);
        var other = await AddScheduleAsync(db, NewActivity("Andet scenarie"), Start.AddHours(2), End.AddHours(2));
        await AddRunAsync(db, other, rooms[0]);
        var schedule = await AddScheduleAsync(db);

        var run = Value(await CreateController(db).Create(new CreateRunDto(schedule.Id)));

        Assert.Equal(rooms[1].Id, run.RoomId);
    }

    [Fact]
    public async Task Create_UsesRoomBookedOutsideTheSchedule()
    {
        using var db = CreateDbContext();
        var rooms = await AddRoomsAsync(db, 2);
        var earlier = await AddScheduleAsync(db, NewActivity("Formiddag"), Start.AddHours(-4), Start);
        await AddRunAsync(db, earlier, rooms[0]);
        var schedule = await AddScheduleAsync(db);

        var run = Value(await CreateController(db).Create(new CreateRunDto(schedule.Id)));

        Assert.Equal(rooms[0].Id, run.RoomId);
    }

    [Fact]
    public async Task Create_SharesRoomWhenNeitherActivityFillsIt()
    {
        // Infosys: lokale_eksklusiv = nej on both activities lets them use the same room at the same time
        using var db = CreateDbContext();
        var rooms = await AddRoomsAsync(db, 2);
        var other = await AddScheduleAsync(db, NewActivity("Brætspil", fillsRoom: false));
        await AddRunAsync(db, other, rooms[0]);
        var schedule = await AddScheduleAsync(db, NewActivity("Mere brætspil", fillsRoom: false));

        var run = Value(await CreateController(db).Create(new CreateRunDto(schedule.Id)));

        Assert.Equal(rooms[0].Id, run.RoomId);
    }

    [Fact]
    public async Task Create_NoFreeRoom_ReturnsConflict_AndCreatesNothing()
    {
        using var db = CreateDbContext();
        var room = (await AddRoomsAsync(db, 1))[0];
        var schedule = await AddScheduleAsync(db);
        await AddRunAsync(db, schedule, room);

        var result = await CreateController(db).Create(new CreateRunDto(schedule.Id));

        Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Single(db.Runs);
    }

    [Fact]
    public async Task Create_WithoutRooms_ReturnsConflict()
    {
        using var db = CreateDbContext();
        var schedule = await AddScheduleAsync(db);

        var result = await CreateController(db).Create(new CreateRunDto(schedule.Id));

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task Create_UnknownSchedule_ReturnsNotFound()
    {
        using var db = CreateDbContext();
        await AddRoomsAsync(db, 1);

        var result = await CreateController(db).Create(new CreateRunDto(999));

        Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Empty(db.Runs);
    }

    // ---- Get (hold/vis) and GetAll (hold/visalle) ----

    [Fact]
    public async Task Get_ShowsActivityTimeRoomAndSeatsBySeatNumber()
    {
        using var db = CreateDbContext();
        var activity = NewActivity("Visningstest");
        var schedule = await AddScheduleAsync(db, activity);
        var room = (await AddRoomsAsync(db, 1))[0];
        var run = await AddRunAsync(db, schedule, room, number: 2);
        var player = await AddParticipantAsync(db, "Anna", "Spiller", nickname: "Kaldenavn");
        var gameMaster = await AddParticipantAsync(db, "Bo", "Leder");
        await AddSeatAsync(db, run, player, RunSeatRole.Player, 2);
        await AddSeatAsync(db, run, gameMaster, RunSeatRole.GameMaster, 1);

        var dto = Value(await CreateController(db).Get(run.Id));

        Assert.Equal(run.Id, dto.Id);
        Assert.Equal(2, dto.Number);
        Assert.Equal(activity.Id, dto.ActivityId);
        Assert.Equal("Visningstest", dto.ActivityTitle);
        Assert.Equal(Start, dto.StartTime);
        Assert.Equal(End, dto.EndTime);
        Assert.Equal(room.Id, dto.RoomId);
        Assert.Equal("Lokale 1", dto.RoomName);
        Assert.Collection(dto.Seats,
            seat =>
            {
                Assert.Equal(gameMaster.Id, seat.ParticipantId);
                Assert.Equal("Bo Leder", seat.ParticipantName);
                Assert.Equal(RunSeatRole.GameMaster, seat.Role);
            },
            seat =>
            {
                Assert.Equal(player.Id, seat.ParticipantId);
                // Infosys shows fornavn and efternavn, not the nickname
                Assert.Equal("Anna Spiller", seat.ParticipantName);
                Assert.Equal(RunSeatRole.Player, seat.Role);
            });
    }

    [Fact]
    public async Task Get_KarmaIsBlankUntilKarmaIsPorted()
    {
        using var db = CreateDbContext();
        var run = await AddRunWithSeatsAsync(db, players: 1, gameMasters: 0);

        var dto = Value(await CreateController(db).Get(run.Id));

        Assert.Null(Assert.Single(dto.Seats).Karma);
    }

    [Theory]
    // Activity: min 3, max 5 players, 1 game master
    [InlineData(0, 0, true, true, true)]
    [InlineData(2, 1, true, false, true)]
    [InlineData(3, 0, false, true, true)]
    [InlineData(3, 1, false, false, true)]
    [InlineData(5, 1, false, false, false)]
    public async Task Get_MissingFlagsAreComputedFromSeats(int players, int gameMasters, bool needsPlayers, bool needsGameMasters, bool canUsePlayers)
    {
        using var db = CreateDbContext();
        var run = await AddRunWithSeatsAsync(db, players, gameMasters);

        var dto = Value(await CreateController(db).Get(run.Id));

        Assert.Equal(needsPlayers, dto.NeedsPlayers);
        Assert.Equal(needsGameMasters, dto.NeedsGameMasters);
        Assert.Equal(canUsePlayers, dto.CanUsePlayers);
    }

    [Fact]
    public async Task Get_ActivityWithoutGameMasters_NeverMissesGameMasters()
    {
        using var db = CreateDbContext();
        var activity = NewActivity();
        activity.GameMastersPerTeam = 0;
        var run = await AddRunWithSeatsAsync(db, players: 0, gameMasters: 0, activity);

        var dto = Value(await CreateController(db).Get(run.Id));

        Assert.False(dto.NeedsGameMasters);
    }

    [Fact]
    public async Task Get_UnknownId_ReturnsNotFound()
    {
        using var db = CreateDbContext();

        var result = await CreateController(db).Get(999);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetAll_ListsEveryRunInIdOrder_WithMissingFlags()
    {
        using var db = CreateDbContext();
        var full = await AddRunWithSeatsAsync(db, players: 3, gameMasters: 1, NewActivity("Fyldt"));
        var empty = await AddRunWithSeatsAsync(db, players: 0, gameMasters: 0, NewActivity("Tomt"));

        var result = await CreateController(db).GetAll();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var runs = Assert.IsAssignableFrom<IEnumerable<RunSummaryDto>>(ok.Value).ToList();
        Assert.Equal(new[] { full.Id, empty.Id }, runs.Select(r => r.Id));
        Assert.Equal("Fyldt", runs[0].ActivityTitle);
        Assert.False(runs[0].NeedsPlayers);
        Assert.False(runs[0].NeedsGameMasters);
        Assert.Equal("Tomt", runs[1].ActivityTitle);
        Assert.True(runs[1].NeedsPlayers);
        Assert.True(runs[1].NeedsGameMasters);
        Assert.Equal(Start, runs[1].StartTime);
        Assert.Equal("Lokale 1", runs[1].RoomName);
    }

    // ---- Seats (groups/scheduleparticipant) ----

    [Fact]
    public async Task CreateSeat_AddsParticipant_InFirstFreeSeatNumber()
    {
        using var db = CreateDbContext();
        var run = await AddRunWithSeatsAsync(db, players: 0, gameMasters: 0);
        var first = await AddParticipantAsync(db, "Første", "Deltager");
        var third = await AddParticipantAsync(db, "Tredje", "Deltager");
        await AddSeatAsync(db, run, first, RunSeatRole.Player, 1);
        await AddSeatAsync(db, run, third, RunSeatRole.Player, 3);
        var newcomer = await AddParticipantAsync(db, "Ny", "Deltager");

        var dto = Value(await CreateController(db).CreateSeat(run.Id, new CreateRunSeatDto(newcomer.Id, RunSeatRole.Player)));

        var seat = dto.Seats.Single(s => s.ParticipantId == newcomer.Id);
        Assert.Equal(2, seat.SeatNumber);
        Assert.Equal(RunSeatRole.Player, seat.Role);
        Assert.Equal(3, db.RunSeats.Count());
    }

    [Fact]
    public async Task CreateSeat_UpdatesMissingFlags()
    {
        using var db = CreateDbContext();
        var run = await AddRunWithSeatsAsync(db, players: 3, gameMasters: 0);
        var gameMaster = await AddParticipantAsync(db);

        var dto = Value(await CreateController(db).CreateSeat(run.Id, new CreateRunSeatDto(gameMaster.Id, RunSeatRole.GameMaster)));

        Assert.False(dto.NeedsGameMasters);
        Assert.False(dto.NeedsPlayers);
    }

    [Fact]
    public async Task CreateSeat_PlayerAboveMinimum_IsAllowedUpToMaximum()
    {
        using var db = CreateDbContext();
        var run = await AddRunWithSeatsAsync(db, players: 4, gameMasters: 1);
        var player = await AddParticipantAsync(db);

        var result = await CreateController(db).CreateSeat(run.Id, new CreateRunSeatDto(player.Id, RunSeatRole.Player));

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(6, db.RunSeats.Count());
    }

    [Fact]
    public async Task CreateSeat_PlayerOnFullRun_IsRefused()
    {
        using var db = CreateDbContext();
        var run = await AddRunWithSeatsAsync(db, players: 5, gameMasters: 0);
        var player = await AddParticipantAsync(db);

        var result = await CreateController(db).CreateSeat(run.Id, new CreateRunSeatDto(player.Id, RunSeatRole.Player));

        Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal(5, db.RunSeats.Count());
    }

    [Fact]
    public async Task CreateSeat_GameMasterWhenEnoughGameMasters_IsRefused()
    {
        using var db = CreateDbContext();
        var run = await AddRunWithSeatsAsync(db, players: 0, gameMasters: 1);
        var gameMaster = await AddParticipantAsync(db);

        var result = await CreateController(db).CreateSeat(run.Id, new CreateRunSeatDto(gameMaster.Id, RunSeatRole.GameMaster));

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateSeat_GameMasterOnActivityWithoutGameMasters_IsRefused()
    {
        using var db = CreateDbContext();
        var activity = NewActivity();
        activity.GameMastersPerTeam = 0;
        var run = await AddRunWithSeatsAsync(db, players: 0, gameMasters: 0, activity);
        var gameMaster = await AddParticipantAsync(db);

        var result = await CreateController(db).CreateSeat(run.Id, new CreateRunSeatDto(gameMaster.Id, RunSeatRole.GameMaster));

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateSeat_ParticipantAlreadyOnRun_IsRefused()
    {
        // Infosys pladser has a unique key on (hold_id, deltager_id), also across roles
        using var db = CreateDbContext();
        var run = await AddRunWithSeatsAsync(db, players: 0, gameMasters: 0);
        var participant = await AddParticipantAsync(db);
        await AddSeatAsync(db, run, participant, RunSeatRole.Player, 1);

        var result = await CreateController(db).CreateSeat(run.Id, new CreateRunSeatDto(participant.Id, RunSeatRole.GameMaster));

        Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Single(db.RunSeats);
    }

    [Fact]
    public async Task CreateSeat_ParticipantOnAnotherRunOfTheSameSchedule_IsAllowed()
    {
        // Infosys doesn't check this on the server; the drag-and-drop page moves participants between teams
        using var db = CreateDbContext();
        var schedule = await AddScheduleAsync(db, NewActivity(fillsRoom: false));
        var room = (await AddRoomsAsync(db, 1))[0];
        var firstRun = await AddRunAsync(db, schedule, room, number: 1);
        var secondRun = await AddRunAsync(db, schedule, room, number: 2);
        var participant = await AddParticipantAsync(db);
        await AddSeatAsync(db, firstRun, participant, RunSeatRole.Player, 1);

        var result = await CreateController(db).CreateSeat(secondRun.Id, new CreateRunSeatDto(participant.Id, RunSeatRole.Player));

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(2, db.RunSeats.Count(s => s.ParticipantId == participant.Id));
    }

    [Fact]
    public async Task CreateSeat_UnknownParticipant_ReturnsNotFound()
    {
        using var db = CreateDbContext();
        var run = await AddRunWithSeatsAsync(db, players: 0, gameMasters: 0);

        var result = await CreateController(db).CreateSeat(run.Id, new CreateRunSeatDto(999, RunSeatRole.Player));

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateSeat_UnknownRun_ReturnsNotFound()
    {
        using var db = CreateDbContext();
        var participant = await AddParticipantAsync(db);

        var result = await CreateController(db).CreateSeat(999, new CreateRunSeatDto(participant.Id, RunSeatRole.Player));

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateSeat_UndefinedRole_ReturnsBadRequest()
    {
        using var db = CreateDbContext();
        var run = await AddRunWithSeatsAsync(db, players: 0, gameMasters: 0);
        var participant = await AddParticipantAsync(db);

        var result = await CreateController(db).CreateSeat(run.Id, new CreateRunSeatDto(participant.Id, (RunSeatRole)7));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(db.RunSeats);
    }

    [Fact]
    public async Task DeleteSeat_RemovesParticipant_AndUpdatesMissingFlags()
    {
        using var db = CreateDbContext();
        var run = await AddRunWithSeatsAsync(db, players: 3, gameMasters: 0);
        var leaving = db.RunSeats.First(s => s.RunId == run.Id).ParticipantId;

        var dto = Value(await CreateController(db).DeleteSeat(run.Id, leaving));

        Assert.DoesNotContain(dto.Seats, s => s.ParticipantId == leaving);
        Assert.True(dto.NeedsPlayers);
        Assert.Equal(2, db.RunSeats.Count());
    }

    [Fact]
    public async Task DeleteSeat_ParticipantNotOnRun_IsNotAnError()
    {
        using var db = CreateDbContext();
        var run = await AddRunWithSeatsAsync(db, players: 1, gameMasters: 0);
        var outsider = await AddParticipantAsync(db);

        var result = await CreateController(db).DeleteSeat(run.Id, outsider.Id);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Single(db.RunSeats);
    }

    [Fact]
    public async Task DeleteSeat_UnknownParticipant_ReturnsNotFound()
    {
        using var db = CreateDbContext();
        var run = await AddRunWithSeatsAsync(db, players: 1, gameMasters: 0);

        var result = await CreateController(db).DeleteSeat(run.Id, 999);

        Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Single(db.RunSeats);
    }

    // ---- Update (hold/edit) ----

    [Fact]
    public async Task GetAvailableRooms_ListsCurrentRoomFirst_ThenFreeRoomsInIdOrder()
    {
        using var db = CreateDbContext();
        var rooms = await AddRoomsAsync(db, 4);
        var other = await AddScheduleAsync(db, NewActivity("Optager lokale 2"));
        await AddRunAsync(db, other, rooms[1]);
        var schedule = await AddScheduleAsync(db);
        var run = await AddRunAsync(db, schedule, rooms[2]);

        var result = await CreateController(db).GetAvailableRooms(run.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var ids = Assert.IsAssignableFrom<IEnumerable<RunRoomDto>>(ok.Value).Select(r => r.Id);
        Assert.Equal(new[] { rooms[2].Id, rooms[0].Id, rooms[3].Id }, ids);
    }

    [Fact]
    public async Task GetAvailableRooms_ListsCurrentRoomOnce_WhenActivityDoesNotFillIt()
    {
        using var db = CreateDbContext();
        var rooms = await AddRoomsAsync(db, 2);
        var schedule = await AddScheduleAsync(db, NewActivity(fillsRoom: false));
        var run = await AddRunAsync(db, schedule, rooms[0]);

        var result = await CreateController(db).GetAvailableRooms(run.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var ids = Assert.IsAssignableFrom<IEnumerable<RunRoomDto>>(ok.Value).Select(r => r.Id);
        Assert.Equal(new[] { rooms[0].Id, rooms[1].Id }, ids);
    }

    [Fact]
    public async Task Update_MovesRunToFreeRoom()
    {
        using var db = CreateDbContext();
        var rooms = await AddRoomsAsync(db, 2);
        var schedule = await AddScheduleAsync(db);
        var run = await AddRunAsync(db, schedule, rooms[0]);

        var dto = Value(await CreateController(db).Update(run.Id, new UpdateRunDto(rooms[1].Id)));

        Assert.Equal(rooms[1].Id, dto.RoomId);
        Assert.Equal(rooms[1].Id, (await db.Runs.AsNoTracking().SingleAsync(Ct)).RoomId);
    }

    [Fact]
    public async Task Update_KeepingTheCurrentRoom_IsAllowed()
    {
        using var db = CreateDbContext();
        var room = (await AddRoomsAsync(db, 1))[0];
        var schedule = await AddScheduleAsync(db);
        var run = await AddRunAsync(db, schedule, room);

        var result = await CreateController(db).Update(run.Id, new UpdateRunDto(room.Id));

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task Update_OccupiedRoom_IsRefused()
    {
        // Infosys only offers free rooms in the form; Fastasys also checks it on the server
        using var db = CreateDbContext();
        var rooms = await AddRoomsAsync(db, 2);
        var other = await AddScheduleAsync(db, NewActivity("Optager lokale 2"));
        await AddRunAsync(db, other, rooms[1]);
        var schedule = await AddScheduleAsync(db);
        var run = await AddRunAsync(db, schedule, rooms[0]);

        var result = await CreateController(db).Update(run.Id, new UpdateRunDto(rooms[1].Id));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(rooms[0].Id, (await db.Runs.AsNoTracking().SingleAsync(r => r.Id == run.Id, Ct)).RoomId);
    }

    [Fact]
    public async Task Update_UnknownRoom_IsRefused()
    {
        using var db = CreateDbContext();
        var room = (await AddRoomsAsync(db, 1))[0];
        var schedule = await AddScheduleAsync(db);
        var run = await AddRunAsync(db, schedule, room);

        var result = await CreateController(db).Update(run.Id, new UpdateRunDto(999));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Update_UnknownRun_ReturnsNotFound()
    {
        using var db = CreateDbContext();
        var room = (await AddRoomsAsync(db, 1))[0];

        var result = await CreateController(db).Update(999, new UpdateRunDto(room.Id));

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    // ---- Delete (hold/slet, hold/ajaxdeletegroup) ----

    [Fact]
    public async Task Delete_EmptyRun_IsRemoved()
    {
        using var db = CreateDbContext();
        var run = await AddRunWithSeatsAsync(db, players: 0, gameMasters: 0);

        var result = await CreateController(db).Delete(run.Id);

        Assert.IsType<OkObjectResult>(result);
        Assert.Empty(db.Runs);
    }

    [Fact]
    public async Task Delete_RunWithParticipants_WithoutRemovingThem_IsRefused()
    {
        // Infosys: without "override" the seats' foreign key makes the delete fail
        using var db = CreateDbContext();
        var run = await AddRunWithSeatsAsync(db, players: 2, gameMasters: 1);

        var result = await CreateController(db).Delete(run.Id);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Single(db.Runs);
        Assert.Equal(3, db.RunSeats.Count());
    }

    [Fact]
    public async Task Delete_RunWithParticipants_RemovingThem_DeletesSeatsAndRun()
    {
        using var db = CreateDbContext();
        var run = await AddRunWithSeatsAsync(db, players: 2, gameMasters: 1);
        var participants = db.Participants.Count();

        var result = await CreateController(db).Delete(run.Id, removeParticipants: true);

        Assert.IsType<OkObjectResult>(result);
        Assert.Empty(db.Runs);
        Assert.Empty(db.RunSeats);
        Assert.Equal(participants, db.Participants.Count());
    }

    [Fact]
    public async Task Delete_UnknownId_ReturnsNotFound()
    {
        using var db = CreateDbContext();

        var result = await CreateController(db).Delete(999);

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
