using Fastasys.ApiService.Controllers;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fastasys.Tests;

// Pins the Infosys behaviour of aktivitet/opretafvikling, aktivitet/editafvikling, aktivitet/sletafvikling and the
// schedules (afviklinger) on aktivitet/vis.
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
    public async Task CreateSchedule_AcrossTheSwitchToSummerTime_StoresTheRealInstants()
    {
        // Fastaval 2027: Saturday 27 March 23:00 (UTC+1) to Sunday 28 March 04:00 (UTC+2). The clocks go from 02:00
        // to 03:00 in between, so the schedule is four hours long, not five.
        using var db = CreateDbContext();
        var activity = await AddActivityAsync(db);
        var start = new DateTimeOffset(2027, 3, 27, 23, 0, 0, TimeSpan.FromHours(1));
        var end = new DateTimeOffset(2027, 3, 28, 4, 0, 0, TimeSpan.FromHours(2));

        var result = await CreateController(db).CreateSchedule(activity.Id, new CreateScheduleDto(start, end, null));

        Assert.IsType<CreatedAtActionResult>(result.Result);
        var stored = await db.Schedules.SingleAsync(Ct);
        Assert.Equal(new DateTime(2027, 3, 27, 22, 0, 0, DateTimeKind.Utc), stored.StartTime);
        Assert.Equal(new DateTime(2027, 3, 28, 2, 0, 0, DateTimeKind.Utc), stored.EndTime);
        Assert.Equal(TimeSpan.FromHours(4), stored.EndTime - stored.StartTime);
    }

    [Fact]
    public async Task CreateSchedule_SameInstantsWithOtherOffsets_IsADuplicate()
    {
        // 01:00 UTC on 28 March 2027 is 03:00 summer time; the check compares instants, not wall-clock times
        using var db = CreateDbContext();
        var activity = await AddActivityAsync(db);
        var controller = CreateController(db);
        await controller.CreateSchedule(activity.Id, new CreateScheduleDto(
            new DateTimeOffset(2027, 3, 28, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2027, 3, 28, 1, 0, 0, TimeSpan.Zero),
            null));

        var result = await controller.CreateSchedule(activity.Id, new CreateScheduleDto(
            new DateTimeOffset(2027, 3, 28, 1, 0, 0, TimeSpan.FromHours(1)),
            new DateTimeOffset(2027, 3, 28, 3, 0, 0, TimeSpan.FromHours(2)),
            null));

        Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Single(db.Schedules);
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

    // ---- aktivitet/editafvikling and aktivitet/sletafvikling ----

    // Thursday 25 March 2027, 12:00-15:00 in Copenhagen (still UTC+1)
    private static readonly DateTimeOffset NewStart = new(2027, 3, 25, 12, 0, 0, TimeSpan.FromHours(1));
    private static readonly DateTimeOffset NewEnd = NewStart.AddHours(3);

    private static async Task<Schedule> AddScheduleAsync(InfosysDbContext db, Activity activity, DateTimeOffset start, DateTimeOffset end, int? roomId = null)
    {
        var schedule = new Schedule { ActivityId = activity.Id, StartTime = start.UtcDateTime, EndTime = end.UtcDateTime, RoomId = roomId };
        db.Schedules.Add(schedule);
        await db.SaveChangesAsync(Ct);
        return schedule;
    }

    private static async Task<Run> AddRunAsync(InfosysDbContext db, Schedule schedule, Room room, int number = 1)
    {
        var run = new Run { ScheduleId = schedule.Id, Number = number, RoomId = room.Id };
        db.Runs.Add(run);
        await db.SaveChangesAsync(Ct);
        return run;
    }

    private static async Task<Schedule> ReloadAsync(InfosysDbContext db, int scheduleId) =>
        await db.Schedules.AsNoTracking().SingleAsync(s => s.Id == scheduleId, Ct);

    [Fact]
    public async Task GetSchedule_ReturnsTheSchedule_WithItsRuns()
    {
        using var db = CreateDbContext();
        var activity = await AddActivityAsync(db);
        var room = await AddRoomAsync(db);
        var schedule = await AddScheduleAsync(db, activity, Start, End, room.Id);
        await AddRunAsync(db, schedule, room);

        var result = await CreateController(db).GetSchedule(activity.Id, schedule.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ScheduleDto>(ok.Value);
        Assert.Equal(schedule.Id, dto.Id);
        Assert.Equal(activity.Id, dto.ActivityId);
        Assert.Equal(Start.UtcDateTime, dto.StartTime);
        Assert.Equal(End.UtcDateTime, dto.EndTime);
        Assert.Equal(room.Id, dto.RoomId);
        Assert.Equal("Mødelokale", dto.RoomName);
        Assert.Equal(1, Assert.Single(dto.Runs).Number);
    }

    [Fact]
    public async Task GetSchedule_UnknownOrOfAnotherActivity_ReturnsNotFound()
    {
        using var db = CreateDbContext();
        var activity = await AddActivityAsync(db);
        var other = await AddActivityAsync(db, "Anden");
        var schedule = await AddScheduleAsync(db, activity, Start, End);
        var controller = CreateController(db);

        Assert.IsType<NotFoundObjectResult>((await controller.GetSchedule(activity.Id, 999)).Result);
        Assert.IsType<NotFoundObjectResult>((await controller.GetSchedule(other.Id, schedule.Id)).Result);
    }

    [Fact]
    public async Task UpdateSchedule_ChangesTimesAndMeetingRoom_StoredInUtc()
    {
        using var db = CreateDbContext();
        var activity = await AddActivityAsync(db);
        var oldRoom = await AddRoomAsync(db, "Gammelt lokale");
        var newRoom = await AddRoomAsync(db, "Nyt lokale");
        var schedule = await AddScheduleAsync(db, activity, Start, End, oldRoom.Id);

        var result = await CreateController(db).UpdateSchedule(activity.Id, schedule.Id, new UpdateScheduleDto(NewStart, NewEnd, newRoom.Id));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ScheduleDto>(ok.Value);
        var stored = await ReloadAsync(db, schedule.Id);
        Assert.Equal(new DateTime(2027, 3, 25, 11, 0, 0, DateTimeKind.Utc), stored.StartTime);
        Assert.Equal(new DateTime(2027, 3, 25, 14, 0, 0, DateTimeKind.Utc), stored.EndTime);
        Assert.Equal(newRoom.Id, stored.RoomId);
        Assert.Equal("Nyt lokale", dto.RoomName);
        Assert.Equal(stored.StartTime, dto.StartTime);
    }

    [Fact]
    public async Task UpdateSchedule_RefreshesActivityUpdatedAt()
    {
        // Infosys sets aktiviteter.updated when a schedule is updated
        using var db = CreateDbContext();
        var activity = await AddActivityAsync(db);
        var schedule = await AddScheduleAsync(db, activity, Start, End);
        var old = DateTimeOffset.UtcNow.AddDays(-3);
        activity.UpdatedAt = old;
        await db.SaveChangesAsync(Ct);

        await CreateController(db).UpdateSchedule(activity.Id, schedule.Id, new UpdateScheduleDto(NewStart, NewEnd, null));

        var stored = await db.Activities.AsNoTracking().SingleAsync(Ct);
        Assert.True(stored.UpdatedAt > old);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public async Task UpdateSchedule_NoRoomOrZero_RemovesTheMeetingRoom(int? roomId)
    {
        // Infosys stores intval(lokale_id)
        using var db = CreateDbContext();
        var activity = await AddActivityAsync(db);
        var room = await AddRoomAsync(db);
        var schedule = await AddScheduleAsync(db, activity, Start, End, room.Id);

        var result = await CreateController(db).UpdateSchedule(activity.Id, schedule.Id, new UpdateScheduleDto(Start, End, roomId));

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Null((await ReloadAsync(db, schedule.Id)).RoomId);
    }

    [Fact]
    public async Task UpdateSchedule_KeepingItsOwnTimes_IsAllowed()
    {
        // Infosys looks for another afvikling (id != this one) with the same aktivitet_id, start and slut
        using var db = CreateDbContext();
        var activity = await AddActivityAsync(db);
        var room = await AddRoomAsync(db);
        var schedule = await AddScheduleAsync(db, activity, Start, End);

        var result = await CreateController(db).UpdateSchedule(activity.Id, schedule.Id, new UpdateScheduleDto(Start, End, room.Id));

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(room.Id, (await ReloadAsync(db, schedule.Id)).RoomId);
    }

    [Fact]
    public async Task UpdateSchedule_ToTheTimesOfAnotherScheduleOfTheActivity_IsRefused()
    {
        using var db = CreateDbContext();
        var activity = await AddActivityAsync(db);
        await AddScheduleAsync(db, activity, NewStart, NewEnd);
        var schedule = await AddScheduleAsync(db, activity, Start, End);

        var result = await CreateController(db).UpdateSchedule(activity.Id, schedule.Id, new UpdateScheduleDto(NewStart.ToUniversalTime(), NewEnd.ToUniversalTime(), null));

        Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal(Start.UtcDateTime, (await ReloadAsync(db, schedule.Id)).StartTime);
    }

    [Fact]
    public async Task UpdateSchedule_ToTheTimesOfAnotherActivitysSchedule_IsAllowed()
    {
        using var db = CreateDbContext();
        var activity = await AddActivityAsync(db);
        var other = await AddActivityAsync(db, "Anden");
        await AddScheduleAsync(db, other, NewStart, NewEnd);
        var schedule = await AddScheduleAsync(db, activity, Start, End);

        var result = await CreateController(db).UpdateSchedule(activity.Id, schedule.Id, new UpdateScheduleDto(NewStart, NewEnd, null));

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task UpdateSchedule_StartMustBeBeforeEnd(int hoursAfterStart)
    {
        using var db = CreateDbContext();
        var activity = await AddActivityAsync(db);
        var schedule = await AddScheduleAsync(db, activity, Start, End);

        var result = await CreateController(db).UpdateSchedule(activity.Id, schedule.Id, new UpdateScheduleDto(NewStart, NewStart.AddHours(hoursAfterStart), null));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(Start.UtcDateTime, (await ReloadAsync(db, schedule.Id)).StartTime);
    }

    [Fact]
    public async Task UpdateSchedule_RequiresStartAndEnd()
    {
        using var db = CreateDbContext();
        var activity = await AddActivityAsync(db);
        var schedule = await AddScheduleAsync(db, activity, Start, End);
        var controller = CreateController(db);

        Assert.IsType<BadRequestObjectResult>((await controller.UpdateSchedule(activity.Id, schedule.Id, new UpdateScheduleDto(null, NewEnd, null))).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.UpdateSchedule(activity.Id, schedule.Id, new UpdateScheduleDto(NewStart, null, null))).Result);
        Assert.Equal(End.UtcDateTime, (await ReloadAsync(db, schedule.Id)).EndTime);
    }

    [Fact]
    public async Task UpdateSchedule_UnknownRoom_IsRefused()
    {
        using var db = CreateDbContext();
        var activity = await AddActivityAsync(db);
        var room = await AddRoomAsync(db);
        var schedule = await AddScheduleAsync(db, activity, Start, End, room.Id);

        var result = await CreateController(db).UpdateSchedule(activity.Id, schedule.Id, new UpdateScheduleDto(NewStart, NewEnd, 999));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(room.Id, (await ReloadAsync(db, schedule.Id)).RoomId);
    }

    [Fact]
    public async Task UpdateSchedule_UnknownOrOfAnotherActivity_ReturnsNotFound()
    {
        using var db = CreateDbContext();
        var activity = await AddActivityAsync(db);
        var other = await AddActivityAsync(db, "Anden");
        var schedule = await AddScheduleAsync(db, activity, Start, End);
        var controller = CreateController(db);

        Assert.IsType<NotFoundObjectResult>((await controller.UpdateSchedule(activity.Id, 999, new UpdateScheduleDto(NewStart, NewEnd, null))).Result);
        Assert.IsType<NotFoundObjectResult>((await controller.UpdateSchedule(other.Id, schedule.Id, new UpdateScheduleDto(NewStart, NewEnd, null))).Result);
        Assert.Equal(Start.UtcDateTime, (await ReloadAsync(db, schedule.Id)).StartTime);
    }

    [Fact]
    public async Task UpdateSchedule_KeepsTheRunsAndTheirRooms()
    {
        // Infosys only updates the afvikling row; its hold keep their numbers and rooms
        using var db = CreateDbContext();
        var activity = await AddActivityAsync(db);
        var room = await AddRoomAsync(db);
        var schedule = await AddScheduleAsync(db, activity, Start, End, room.Id);
        var run = await AddRunAsync(db, schedule, room, number: 2);

        var result = await CreateController(db).UpdateSchedule(activity.Id, schedule.Id, new UpdateScheduleDto(NewStart, NewEnd, room.Id));

        var dto = Assert.IsType<ScheduleDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
        var runDto = Assert.Single(dto.Runs);
        Assert.Equal(run.Id, runDto.Id);
        Assert.Equal(2, runDto.Number);
        Assert.Equal(room.Id, runDto.RoomId);
        Assert.Equal(NewStart.UtcDateTime, runDto.StartTime);
    }

    [Fact]
    public async Task DeleteSchedule_WithoutRuns_DeletesIt()
    {
        using var db = CreateDbContext();
        var activity = await AddActivityAsync(db);
        var schedule = await AddScheduleAsync(db, activity, Start, End);
        var other = await AddScheduleAsync(db, activity, NewStart, NewEnd);

        var result = await CreateController(db).DeleteSchedule(activity.Id, schedule.Id);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(other.Id, (await db.Schedules.AsNoTracking().SingleAsync(Ct)).Id);
    }

    [Fact]
    public async Task DeleteSchedule_WithRuns_IsRefused_AndKeepsEverything()
    {
        // Infosys: the hold foreign key has no ON DELETE CASCADE, so the delete fails and nothing is removed
        using var db = CreateDbContext();
        var activity = await AddActivityAsync(db);
        var room = await AddRoomAsync(db);
        var schedule = await AddScheduleAsync(db, activity, Start, End);
        await AddRunAsync(db, schedule, room);

        var result = await CreateController(db).DeleteSchedule(activity.Id, schedule.Id);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Single(db.Schedules);
        Assert.Single(db.Runs);
    }

    [Fact]
    public async Task DeleteSchedule_DoesNotTouchActivityUpdatedAt()
    {
        // Infosys deleteAfvikling, unlike opretAfvikling and updateAfvikling, doesn't set aktiviteter.updated
        using var db = CreateDbContext();
        var activity = await AddActivityAsync(db);
        var schedule = await AddScheduleAsync(db, activity, Start, End);
        var old = DateTimeOffset.UtcNow.AddDays(-3);
        activity.UpdatedAt = old;
        await db.SaveChangesAsync(Ct);

        await CreateController(db).DeleteSchedule(activity.Id, schedule.Id);

        Assert.Equal(old, (await db.Activities.AsNoTracking().SingleAsync(Ct)).UpdatedAt);
    }

    [Fact]
    public async Task DeleteSchedule_UnknownOrOfAnotherActivity_ReturnsNotFound()
    {
        using var db = CreateDbContext();
        var activity = await AddActivityAsync(db);
        var other = await AddActivityAsync(db, "Anden");
        var schedule = await AddScheduleAsync(db, activity, Start, End);
        var controller = CreateController(db);

        Assert.IsType<NotFoundObjectResult>(await controller.DeleteSchedule(activity.Id, 999));
        Assert.IsType<NotFoundObjectResult>(await controller.DeleteSchedule(other.Id, schedule.Id));
        Assert.Single(db.Schedules);
    }
}
