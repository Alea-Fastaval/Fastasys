using Fastasys.ApiService.Controllers;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fastasys.Tests;

// Pins the Infosys behaviour of aktivitet/opretafvikling and the schedules (afviklinger) on aktivitet/vis.
public class ActivitySchedulesTests
{
    // Wednesday 24 March 2027, 10:00-14:00 in Copenhagen (UTC+1)
    private static readonly DateTimeOffset Start = new(2027, 3, 24, 10, 0, 0, TimeSpan.FromHours(1));
    private static readonly DateTimeOffset End = Start.AddHours(4);

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

    private static ActivitiesController CreateController(InfosysDbContext db) =>
        new(db, NullLogger<ActivitiesController>.Instance);

    private static async Task<Activity> AddActivityAsync(InfosysDbContext db, string title = "Testscenarie")
    {
        var activity = new Activity { Title = title, MinPlayersPerTeam = 3, MaxPlayersPerTeam = 5, GameMastersPerTeam = 1 };
        db.Activities.Add(activity);
        await db.SaveChangesAsync(Ct);
        return activity;
    }

    private static async Task<Room> AddRoomAsync(InfosysDbContext db, string name = "Mødelokale")
    {
        var room = new Room { Name = name };
        db.Rooms.Add(room);
        await db.SaveChangesAsync(Ct);
        return room;
    }

    [Fact]
    public async Task CreateSchedule_StoresTimesInUtc_AndMeetingRoom()
    {
        using var db = CreateDbContext();
        var activity = await AddActivityAsync(db);
        var room = await AddRoomAsync(db);

        var result = await CreateController(db).CreateSchedule(activity.Id, new CreateScheduleDto(Start, End, room.Id));

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var dto = Assert.IsType<ScheduleDto>(created.Value);
        var stored = await db.Schedules.SingleAsync(Ct);
        Assert.Equal(new DateTime(2027, 3, 24, 9, 0, 0, DateTimeKind.Utc), stored.StartTime);
        Assert.Equal(DateTimeKind.Utc, stored.StartTime.Kind);
        Assert.Equal(new DateTime(2027, 3, 24, 13, 0, 0, DateTimeKind.Utc), stored.EndTime);
        Assert.Equal(room.Id, stored.RoomId);
        Assert.Equal(stored.Id, dto.Id);
        Assert.Equal("Mødelokale", dto.RoomName);
        Assert.Empty(dto.Runs);
    }

    [Fact]
    public async Task CreateSchedule_RefreshesActivityUpdatedAt()
    {
        // Infosys sets aktiviteter.updated when a schedule is created
        using var db = CreateDbContext();
        var activity = await AddActivityAsync(db);
        var old = DateTimeOffset.UtcNow.AddDays(-3);
        activity.UpdatedAt = old;
        await db.SaveChangesAsync(Ct);

        await CreateController(db).CreateSchedule(activity.Id, new CreateScheduleDto(Start, End, null));

        var stored = await db.Activities.AsNoTracking().SingleAsync(Ct);
        Assert.True(stored.UpdatedAt > old);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public async Task CreateSchedule_NoRoomOrZero_MeansNoMeetingRoom(int? roomId)
    {
        // Infosys stores intval(lokale_id); 0 shows "Mødested: spørg venligst i informationen."
        using var db = CreateDbContext();
        var activity = await AddActivityAsync(db);

        var result = await CreateController(db).CreateSchedule(activity.Id, new CreateScheduleDto(Start, End, roomId));

        Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Null((await db.Schedules.SingleAsync(Ct)).RoomId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateSchedule_StartMustBeBeforeEnd(int hoursAfterStart)
    {
        using var db = CreateDbContext();
        var activity = await AddActivityAsync(db);

        var result = await CreateController(db).CreateSchedule(activity.Id, new CreateScheduleDto(Start, Start.AddHours(hoursAfterStart), null));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(db.Schedules);
    }

    [Fact]
    public async Task CreateSchedule_RequiresStartAndEnd()
    {
        using var db = CreateDbContext();
        var activity = await AddActivityAsync(db);
        var controller = CreateController(db);

        Assert.IsType<BadRequestObjectResult>((await controller.CreateSchedule(activity.Id, new CreateScheduleDto(null, End, null))).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.CreateSchedule(activity.Id, new CreateScheduleDto(Start, null, null))).Result);
        Assert.Empty(db.Schedules);
    }

    [Fact]
    public async Task CreateSchedule_SameStartAndEndTwice_IsRefused()
    {
        // Infosys checks for an existing afvikling with the same aktivitet_id, start and slut
        using var db = CreateDbContext();
        var activity = await AddActivityAsync(db);
        var controller = CreateController(db);
        await controller.CreateSchedule(activity.Id, new CreateScheduleDto(Start, End, null));

        var result = await controller.CreateSchedule(activity.Id, new CreateScheduleDto(Start.ToUniversalTime(), End.ToUniversalTime(), null));

        Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Single(db.Schedules);
    }

    [Fact]
    public async Task CreateSchedule_OverlappingButDifferentTimes_IsAllowed()
    {
        using var db = CreateDbContext();
        var activity = await AddActivityAsync(db);
        var controller = CreateController(db);
        await controller.CreateSchedule(activity.Id, new CreateScheduleDto(Start, End, null));

        var result = await controller.CreateSchedule(activity.Id, new CreateScheduleDto(Start.AddHours(1), End, null));

        Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(2, db.Schedules.Count());
    }

    [Fact]
    public async Task CreateSchedule_SameTimesForAnotherActivity_IsAllowed()
    {
        using var db = CreateDbContext();
        var first = await AddActivityAsync(db, "Første");
        var second = await AddActivityAsync(db, "Anden");
        var controller = CreateController(db);
        await controller.CreateSchedule(first.Id, new CreateScheduleDto(Start, End, null));

        var result = await controller.CreateSchedule(second.Id, new CreateScheduleDto(Start, End, null));

        Assert.IsType<CreatedAtActionResult>(result.Result);
    }

    [Fact]
    public async Task CreateSchedule_UnknownRoom_IsRefused()
    {
        using var db = CreateDbContext();
        var activity = await AddActivityAsync(db);

        var result = await CreateController(db).CreateSchedule(activity.Id, new CreateScheduleDto(Start, End, 999));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(db.Schedules);
    }

    [Fact]
    public async Task CreateSchedule_UnknownActivity_ReturnsNotFound()
    {
        using var db = CreateDbContext();

        var result = await CreateController(db).CreateSchedule(999, new CreateScheduleDto(Start, End, null));

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetSchedules_OrdersByStart_WithRunsByNumberAndTheirSeats()
    {
        using var db = CreateDbContext();
        var activity = await AddActivityAsync(db);
        var room = await AddRoomAsync(db, "Spillelokale");
        var late = new Schedule { ActivityId = activity.Id, StartTime = Start.UtcDateTime.AddDays(1), EndTime = End.UtcDateTime.AddDays(1) };
        var early = new Schedule { ActivityId = activity.Id, StartTime = Start.UtcDateTime, EndTime = End.UtcDateTime, RoomId = room.Id };
        db.Schedules.AddRange(late, early);
        await db.SaveChangesAsync(Ct);
        var second = new Run { ScheduleId = early.Id, Number = 2, RoomId = room.Id };
        var first = new Run { ScheduleId = early.Id, Number = 1, RoomId = room.Id };
        db.Runs.AddRange(second, first);
        var participant = new Participant { FirstName = "Test", LastName = "Spilleder" };
        db.Participants.Add(participant);
        await db.SaveChangesAsync(Ct);
        db.RunSeats.Add(new RunSeat { RunId = first.Id, SeatNumber = 1, ParticipantId = participant.Id, Role = RunSeatRole.GameMaster });
        await db.SaveChangesAsync(Ct);

        var result = await CreateController(db).GetSchedules(activity.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var schedules = Assert.IsAssignableFrom<IEnumerable<ScheduleDto>>(ok.Value).ToList();
        Assert.Equal(new[] { early.Id, late.Id }, schedules.Select(s => s.Id));
        Assert.Equal("Spillelokale", schedules[0].RoomName);
        Assert.Null(schedules[1].RoomName);
        Assert.Equal(new[] { 1, 2 }, schedules[0].Runs.Select(r => r.Number));
        var seat = Assert.Single(schedules[0].Runs[0].Seats);
        Assert.Equal("Test Spilleder", seat.ParticipantName);
        Assert.False(schedules[0].Runs[0].NeedsGameMasters);
        Assert.True(schedules[0].Runs[1].NeedsGameMasters);
        Assert.Empty(schedules[1].Runs);
    }

    [Fact]
    public async Task GetSchedules_UnknownActivity_ReturnsNotFound()
    {
        using var db = CreateDbContext();

        var result = await CreateController(db).GetSchedules(999);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }
}
