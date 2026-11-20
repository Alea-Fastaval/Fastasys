using Fastasys.ApiService.Services;
using Fastasys.ApiService.Controllers;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fastasys.Tests;

public class ActivitySignupsControllerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static InfosysDbContext CreateDb() => new(new DbContextOptionsBuilder<InfosysDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(Participant Participant, Schedule Schedule)> SeedAsync(InfosysDbContext db)
    {
        var participant = new Participant { FirstName = "Test" };
        var schedule = new Schedule
        {
            Activity = new Activity { Title = "Scenario", GameMastersPerTeam = 1 },
            StartTime = new DateTime(2027, 3, 24, 10, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2027, 3, 24, 14, 0, 0, DateTimeKind.Utc)
        };
        db.AddRange(participant, schedule);
        await db.SaveChangesAsync(Ct);
        return (participant, schedule);
    }

    private static ActivitySignupDto Created(ActionResult<ActivitySignupDto> result) =>
        Assert.IsType<ActivitySignupDto>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);

    [Fact]
    public async Task CreateAndGet_PreserveRolePriorityAndScheduleDetails()
    {
        using var db = CreateDb();
        var (participant, schedule) = await SeedAsync(db);
        var before = DateTime.UtcNow;
        var controller = new ActivitySignupsController(db);

        var created = Created(await controller.Create(participant.Id, new(schedule.Id, RunSeatRole.Player, 2)));
        var read = Assert.IsType<OkObjectResult>((await controller.Get(participant.Id, created.Id)).Result);

        Assert.Equal(created, read.Value);
        Assert.Equal(participant.Id, created.ParticipantId);
        Assert.Equal(schedule.Id, created.ScheduleId);
        Assert.Equal(schedule.ActivityId, created.ActivityId);
        Assert.Equal("Scenario", created.ActivityTitle);
        Assert.Equal(schedule.StartTime, created.StartTime);
        Assert.Equal(schedule.EndTime, created.EndTime);
        Assert.Equal("Player", created.Role);
        Assert.Equal(2, created.Priority);
        Assert.InRange(created.SignedUpAt, before, DateTime.UtcNow);
        Assert.Empty(db.RunSeats);
    }

    [Fact]
    public async Task Create_SupportsCombinedGameMasterAndFirstPriorityPlayer_ForOneSchedule()
    {
        using var db = CreateDb();
        var (participant, schedule) = await SeedAsync(db);
        var controller = new ActivitySignupsController(db);

        var gm = Created(await controller.Create(participant.Id, new(schedule.Id, RunSeatRole.GameMaster, 0)));
        var player = Created(await controller.Create(participant.Id, new(schedule.Id, RunSeatRole.Player, 1)));

        Assert.NotEqual(gm.Id, player.Id);
        var list = Assert.IsType<List<ActivitySignupDto>>(Assert.IsType<OkObjectResult>((await controller.GetAll(participant.Id)).Result).Value);
        Assert.Equal(new[] { gm.Id, player.Id }, list.Select(s => s.Id));
    }

    [Theory]
    [InlineData(RunSeatRole.Player, null)]
    [InlineData(RunSeatRole.Player, 0)]
    [InlineData(RunSeatRole.Player, -1)]
    [InlineData(RunSeatRole.Player, 5)]
    [InlineData(RunSeatRole.GameMaster, null)]
    [InlineData(RunSeatRole.GameMaster, -1)]
    [InlineData(RunSeatRole.GameMaster, 5)]
    [InlineData(null, 1)]
    [InlineData((RunSeatRole)99, 1)]
    public async Task Create_RejectsMissingOrInvalidRoleAndPriority(RunSeatRole? role, int? priority)
    {
        using var db = CreateDb();
        var (participant, schedule) = await SeedAsync(db);

        var result = await new ActivitySignupsController(db).Create(participant.Id, new(schedule.Id, role, priority));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(db.ParticipantSchedules);
    }

    [Theory]
    [InlineData(RunSeatRole.Player, 1)]
    [InlineData(RunSeatRole.Player, 4)]
    [InlineData(RunSeatRole.GameMaster, 0)]
    [InlineData(RunSeatRole.GameMaster, 1)]
    [InlineData(RunSeatRole.GameMaster, 4)]
    public async Task Create_AcceptsSupportedPrioritiesIncludingLegacyGameMasterValues(RunSeatRole role, int priority)
    {
        using var db = CreateDb();
        var (participant, schedule) = await SeedAsync(db);

        var created = Created(await new ActivitySignupsController(db).Create(participant.Id, new(schedule.Id, role, priority)));

        Assert.Equal(priority, created.Priority);
        Assert.Equal(role.ToString(), created.Role);
    }

    [Fact]
    public async Task Create_RejectsUnknownReferencesAndGameMasterForActivityWithoutGameMasters()
    {
        using var db = CreateDb();
        var (participant, schedule) = await SeedAsync(db);
        schedule.Activity.GameMastersPerTeam = 0;
        await db.SaveChangesAsync(Ct);
        var controller = new ActivitySignupsController(db);

        Assert.IsType<NotFoundObjectResult>((await controller.Create(999, new(schedule.Id, RunSeatRole.Player, 1))).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.Create(participant.Id, new(999, RunSeatRole.Player, 1))).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.Create(participant.Id, new(schedule.Id, RunSeatRole.GameMaster, 0))).Result);
        Assert.Empty(db.ParticipantSchedules);
    }

    [Fact]
    public async Task DuplicateScheduleAndPriority_IsConflictEvenWithDifferentRole()
    {
        using var db = CreateDb();
        var (participant, schedule) = await SeedAsync(db);
        var controller = new ActivitySignupsController(db);
        var original = Created(await controller.Create(participant.Id, new(schedule.Id, RunSeatRole.Player, 1)));

        Assert.IsType<ConflictObjectResult>((await controller.Create(participant.Id, new(schedule.Id, RunSeatRole.Player, 1))).Result);
        Assert.IsType<ConflictObjectResult>((await controller.Create(participant.Id, new(schedule.Id, RunSeatRole.GameMaster, 1))).Result);
        Assert.Equal(original.Id, Assert.Single(db.ParticipantSchedules).Id);
    }

    [Fact]
    public async Task Update_CanRankExistingDataAndMoveSchedule_WithoutChangingIdentityOrTimestamp()
    {
        using var db = CreateDb();
        var (participant, schedule) = await SeedAsync(db);
        var (_, otherSchedule) = await SeedAsync(db);
        var time = DateTime.UtcNow.AddDays(-10);
        var existing = new ParticipantSchedule { ParticipantId = participant.Id, ScheduleId = schedule.Id, SignedUpAt = time };
        db.ParticipantSchedules.Add(existing);
        await db.SaveChangesAsync(Ct);

        var result = await new ActivitySignupsController(db).Update(participant.Id, existing.Id, new(otherSchedule.Id, RunSeatRole.Player, 3));

        var updated = Assert.IsType<ActivitySignupDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(existing.Id, updated.Id);
        Assert.Equal(time, updated.SignedUpAt);
        Assert.Equal(otherSchedule.Id, updated.ScheduleId);
        Assert.Equal(3, updated.Priority);
    }

    [Fact]
    public async Task InvalidOrDuplicateUpdate_LeavesTheOriginalUnchanged()
    {
        using var db = CreateDb();
        var (participant, schedule) = await SeedAsync(db);
        var controller = new ActivitySignupsController(db);
        Created(await controller.Create(participant.Id, new(schedule.Id, RunSeatRole.Player, 1)));
        var other = Created(await controller.Create(participant.Id, new(schedule.Id, RunSeatRole.Player, 2)));

        Assert.IsType<ConflictObjectResult>((await controller.Update(participant.Id, other.Id, new(schedule.Id, RunSeatRole.GameMaster, 1))).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.Update(participant.Id, other.Id, new(schedule.Id, RunSeatRole.Player, 0))).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.Update(participant.Id, other.Id, new(999, RunSeatRole.Player, 3))).Result);
        var unchanged = Assert.IsType<ActivitySignupDto>(Assert.IsType<OkObjectResult>((await controller.Get(participant.Id, other.Id)).Result).Value);
        Assert.Equal(other, unchanged);
    }

    [Fact]
    public async Task Read_ExposesUnrankedAndUnknownLegacyRolesWithoutChangingThem()
    {
        using var db = CreateDb();
        var (participant, schedule) = await SeedAsync(db);
        db.ParticipantSchedules.Add(new ParticipantSchedule { ParticipantId = participant.Id, ScheduleId = schedule.Id, Role = "LegacyObserver" });
        await db.SaveChangesAsync(Ct);

        var result = await new ActivitySignupsController(db).GetAll(participant.Id);

        var signup = Assert.Single(Assert.IsType<List<ActivitySignupDto>>(Assert.IsType<OkObjectResult>(result.Result).Value));
        Assert.Null(signup.Priority);
        Assert.Equal("LegacyObserver", signup.Role);
    }

    [Fact]
    public async Task GetUpdateAndDelete_AreScopedToTheParticipant()
    {
        using var db = CreateDb();
        var (participant, schedule) = await SeedAsync(db);
        var (other, _) = await SeedAsync(db);
        var controller = new ActivitySignupsController(db);
        var signup = Created(await controller.Create(participant.Id, new(schedule.Id, RunSeatRole.Player, 1)));

        Assert.IsType<NotFoundObjectResult>((await controller.Get(other.Id, signup.Id)).Result);
        Assert.IsType<NotFoundObjectResult>((await controller.Update(other.Id, signup.Id, new(schedule.Id, RunSeatRole.Player, 2))).Result);
        Assert.IsType<NotFoundObjectResult>(await controller.Delete(other.Id, signup.Id));
        Assert.IsType<NotFoundObjectResult>((await controller.Get(participant.Id, 999)).Result);
        Assert.IsType<NotFoundObjectResult>((await controller.Update(participant.Id, 999, new(schedule.Id, RunSeatRole.Player, 2))).Result);
        Assert.IsType<NotFoundObjectResult>(await controller.Delete(participant.Id, 999));
        Assert.Single(db.ParticipantSchedules);
    }

    [Fact]
    public async Task List_DistinguishesUnknownParticipantFromParticipantWithoutPreferences()
    {
        using var db = CreateDb();
        var (participant, _) = await SeedAsync(db);
        var controller = new ActivitySignupsController(db);

        Assert.IsType<NotFoundObjectResult>((await controller.GetAll(999)).Result);
        var list = Assert.IsType<OkObjectResult>((await controller.GetAll(participant.Id)).Result);
        Assert.Empty(Assert.IsType<List<ActivitySignupDto>>(list.Value));
    }

    [Fact]
    public async Task Delete_RemovesOnlyThePreference_AndLeavesTheAssignedSeat()
    {
        using var db = CreateDb();
        var (participant, schedule) = await SeedAsync(db);
        var controller = new ActivitySignupsController(db);
        var gm = Created(await controller.Create(participant.Id, new(schedule.Id, RunSeatRole.GameMaster, 0)));
        var player = Created(await controller.Create(participant.Id, new(schedule.Id, RunSeatRole.Player, 1)));
        db.RunSeats.Add(new RunSeat
        {
            Run = new Run { ScheduleId = schedule.Id, Number = 1, Room = new Room { Name = "A" } },
            SeatNumber = 1,
            ParticipantId = participant.Id,
            Role = RunSeatRole.Player
        });
        await db.SaveChangesAsync(Ct);

        Assert.IsType<NoContentResult>(await controller.Delete(participant.Id, gm.Id));
        Assert.Equal(player.Id, Assert.Single(db.ParticipantSchedules).Id);
        Assert.Single(db.RunSeats);
    }

    [Fact]
    public async Task DeleteSchedule_WithSignupsIsRefused()
    {
        using var db = CreateDb();
        var (participant, schedule) = await SeedAsync(db);
        var controller = new ActivitySignupsController(db);
        var signup = Created(await controller.Create(participant.Id, new(schedule.Id, RunSeatRole.Player, 1)));
        var activities = new ActivitiesController(db, NullLogger<ActivitiesController>.Instance, new KarmaService(db));

        Assert.IsType<ConflictObjectResult>(await activities.DeleteSchedule(schedule.ActivityId, schedule.Id));
        Assert.Single(db.ParticipantSchedules);
        await controller.Delete(participant.Id, signup.Id);
        Assert.IsType<OkObjectResult>(await activities.DeleteSchedule(schedule.ActivityId, schedule.Id));
    }
}
