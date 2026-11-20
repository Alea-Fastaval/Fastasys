using Fastasys.ApiService.Controllers;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fastasys.Tests;

public class ScheduleCancellationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly DateTime Start = new(2027, 3, 24, 9, 0, 0, DateTimeKind.Utc);

    private static InfosysDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<InfosysDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ActivitiesController Activities(InfosysDbContext db) =>
        new(db, NullLogger<ActivitiesController>.Instance, new KarmaService(db));

    private static RunsController Runs(InfosysDbContext db) =>
        new(db, NullLogger<RunsController>.Instance, new KarmaService(db));

    private static ScheduleDto ScheduleValue(ActionResult<ScheduleDto> result) =>
        Assert.IsType<ScheduleDto>(Assert.IsType<OkObjectResult>(result.Result).Value);

    private sealed record Setup(
        Participant Participant,
        Participant Other,
        Schedule Schedule,
        Run Run,
        ParticipantSchedule Signup,
        Room Room,
        Room Spare
    );

    private static async Task<Setup> SeedAsync(InfosysDbContext db)
    {
        var p = new Participant { FirstName = "Player" };
        var other = new Participant { FirstName = "Other" };
        var schedule = new Schedule
        {
            Activity = new Activity
            {
                Title = "Scenario",
                KarmaType = KarmaType.Big,
                IsTimeExclusive = true,
                MinPlayersPerTeam = 1,
                MaxPlayersPerTeam = 3,
                GameMastersPerTeam = 1,
            },
            StartTime = Start,
            EndTime = Start.AddHours(4),
        };
        var room = new Room { Name = "Room" };
        var spare = new Room { Name = "Spare" };
        var run = new Run
        {
            Schedule = schedule,
            Room = room,
            Number = 1,
        };
        run.Seats.Add(
            new RunSeat
            {
                Participant = p,
                SeatNumber = 1,
                Role = RunSeatRole.Player,
            }
        );
        var signup = new ParticipantSchedule
        {
            Participant = p,
            Schedule = schedule,
            Priority = 1,
        };
        db.AddRange(run, signup, other, spare);
        await db.SaveChangesAsync(Ct);
        return new(p, other, schedule, run, signup, room, spare);
    }

    [Fact]
    public async Task CancelAndRestore_PreserveRecordsAndOtherOccurrences_AndRepeatedReadsAreIdempotent()
    {
        using var db = CreateDb();
        var s = await SeedAsync(db);
        var otherOccurrence = new Schedule
        {
            Activity = s.Schedule.Activity,
            StartTime = Start.AddDays(1),
            EndTime = Start.AddDays(1).AddHours(4),
        };
        var otherRun = new Run
        {
            Schedule = otherOccurrence,
            Room = s.Room,
            Number = 1,
        };
        otherRun.Seats.Add(new RunSeat { Participant = s.Participant, SeatNumber = 1 });
        db.AddRange(
            otherRun,
            new ParticipantSchedule
            {
                Participant = s.Participant,
                Schedule = otherOccurrence,
                Priority = 2,
            },
            new ParticipantSchedule
            {
                Participant = s.Participant,
                Schedule = s.Schedule,
                Priority = 0,
                Role = "GameMaster",
            }
        );
        await db.SaveChangesAsync(Ct);
        var karma = new KarmaService(db);
        Assert.Equal(0, (await karma.CalculateAsync([s.Participant.Id], Ct))[s.Participant.Id]); // -12 -7 -1 +20
        var timestamp = s.Signup.SignedUpAt;
        var before = DateTimeOffset.UtcNow;

        var cancelled = ScheduleValue(
            await Activities(db).SetCancellation(s.Schedule.ActivityId, s.Schedule.Id, new(true))
        );

        Assert.True(cancelled.IsCancelled);
        Assert.NotNull(cancelled.CancelledAt);
        Assert.InRange(cancelled.CancelledAt.Value, before, DateTimeOffset.UtcNow);
        Assert.Equal(9, Assert.Single(Assert.Single(cancelled.Runs).Seats).Karma);
        Assert.Equal(3, db.ParticipantSchedules.Count());
        Assert.Equal(2, db.RunSeats.Count());
        Assert.Equal(timestamp, s.Signup.SignedUpAt);
        Assert.False(otherOccurrence.IsCancelled);
        var again = ScheduleValue(
            await Activities(db).SetCancellation(s.Schedule.ActivityId, s.Schedule.Id, new(true))
        );
        Assert.Equal(cancelled.CancelledAt, again.CancelledAt);
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(9, (await karma.CalculateAsync([s.Participant.Id], Ct))[s.Participant.Id]);
        }

        var restored = ScheduleValue(
            await Activities(db).SetCancellation(s.Schedule.ActivityId, s.Schedule.Id, new(false))
        );
        Assert.False(restored.IsCancelled);
        Assert.Null(restored.CancelledAt);
        Assert.Equal(0, (await karma.CalculateAsync([s.Participant.Id], Ct))[s.Participant.Id]);
        var updatedAt = s.Schedule.Activity.UpdatedAt;
        ScheduleValue(await Activities(db).SetCancellation(s.Schedule.ActivityId, s.Schedule.Id, new(false)));
        Assert.Equal(updatedAt, s.Schedule.Activity.UpdatedAt);
        Assert.Equal(3, db.ParticipantSchedules.Count());
        Assert.Equal(2, db.RunSeats.Count());
    }

    [Fact]
    public async Task CancelledOccurrence_RejectsNewPreferencesRunsAndSeats_ButAllowsExplicitRemoval()
    {
        using var db = CreateDb();
        var s = await SeedAsync(db);
        ScheduleValue(await Activities(db).SetCancellation(s.Schedule.ActivityId, s.Schedule.Id, new(true)));
        var signups = new ActivitySignupsController(db);
        Assert.IsType<BadRequestObjectResult>(
            (await signups.Create(s.Other.Id, new(s.Schedule.Id, RunSeatRole.Player, 1))).Result
        );
        Assert.IsType<BadRequestObjectResult>(
            (await signups.Update(s.Participant.Id, s.Signup.Id, new(s.Schedule.Id, RunSeatRole.Player, 2))).Result
        );
        Assert.IsType<ConflictObjectResult>((await Runs(db).Create(new(s.Schedule.Id))).Result);
        Assert.IsType<ConflictObjectResult>(
            (await Runs(db).CreateSeat(s.Run.Id, new(s.Other.Id, RunSeatRole.Player))).Result
        );
        Assert.Single(db.RunSeats);
        Assert.Single(db.ParticipantSchedules);
        var list = Assert.IsAssignableFrom<IEnumerable<RunSummaryDto>>(
            Assert.IsType<OkObjectResult>((await Runs(db).GetAll()).Result).Value
        );
        var run = Assert.Single(list);
        Assert.True(run.IsCancelled);
        Assert.False(run.NeedsPlayers);
        Assert.False(run.NeedsGameMasters);
        var signup = Assert.IsType<ActivitySignupDto>(
            Assert.IsType<OkObjectResult>((await signups.Get(s.Participant.Id, s.Signup.Id)).Result).Value
        );
        Assert.True(signup.IsCancelled);

        Assert.IsType<OkObjectResult>((await Runs(db).DeleteSeat(s.Run.Id, s.Participant.Id)).Result);
        Assert.Single(db.ParticipantSchedules);
        Assert.Empty(db.RunSeats);
    }

    [Fact]
    public async Task CancellingReleasesRooms_RestoringChecksRetainedBookingsBeforeChangingState()
    {
        using var db = CreateDb();
        var s = await SeedAsync(db);
        ScheduleValue(await Activities(db).SetCancellation(s.Schedule.ActivityId, s.Schedule.Id, new(true)));
        var cancelledAt = s.Schedule.CancelledAt;
        var competing = new Schedule
        {
            Activity = new Activity { Title = "Other activity", MaxPlayersPerTeam = 3 },
            StartTime = Start,
            EndTime = Start.AddHours(4),
        };
        db.Add(competing);
        await db.SaveChangesAsync(Ct);
        var created = Assert.IsType<RunDto>(
            Assert.IsType<CreatedAtActionResult>((await Runs(db).Create(new(competing.Id))).Result).Value
        );
        Assert.Equal(s.Room.Id, created.RoomId);
        Assert.IsType<ConflictObjectResult>(
            (await Activities(db).SetCancellation(s.Schedule.ActivityId, s.Schedule.Id, new(false))).Result
        );
        Assert.True(s.Schedule.IsCancelled);
        Assert.Equal(cancelledAt, s.Schedule.CancelledAt);
        Assert.Single(db.ParticipantSchedules);
        Assert.Single(db.RunSeats);
        Assert.IsType<OkObjectResult>((await Runs(db).Update(created.Id, new(s.Spare.Id))).Result);
        Assert.False(
            ScheduleValue(
                await Activities(db).SetCancellation(s.Schedule.ActivityId, s.Schedule.Id, new(false))
            ).IsCancelled
        );
    }

    [Fact]
    public async Task RestoreAlsoChecksConflictsBetweenItsOwnRetainedRuns()
    {
        using var db = CreateDb();
        var s = await SeedAsync(db);
        s.Schedule.Activity.IsRoomExclusive = false;
        var second = new Run
        {
            Schedule = s.Schedule,
            Room = s.Room,
            Number = 2,
        };
        db.Add(second);
        await db.SaveChangesAsync(Ct);
        ScheduleValue(await Activities(db).SetCancellation(s.Schedule.ActivityId, s.Schedule.Id, new(true)));
        s.Schedule.Activity.IsRoomExclusive = true;
        await db.SaveChangesAsync(Ct);
        Assert.IsType<ConflictObjectResult>(
            (await Activities(db).SetCancellation(s.Schedule.ActivityId, s.Schedule.Id, new(false))).Result
        );
        second.RoomId = s.Spare.Id;
        await db.SaveChangesAsync(Ct);
        Assert.False(
            ScheduleValue(
                await Activities(db).SetCancellation(s.Schedule.ActivityId, s.Schedule.Id, new(false))
            ).IsCancelled
        );
    }

    [Fact]
    public async Task InvalidBodyOrWrongActivity_DoesNotChangeTheOccurrence()
    {
        using var db = CreateDb();
        var s = await SeedAsync(db);
        var controller = Activities(db);
        Assert.IsType<BadRequestObjectResult>(
            (await controller.SetCancellation(s.Schedule.ActivityId, s.Schedule.Id, new(null))).Result
        );
        Assert.IsType<NotFoundObjectResult>((await controller.SetCancellation(999, s.Schedule.Id, new(true))).Result);
        Assert.IsType<NotFoundObjectResult>(
            (await controller.SetCancellation(s.Schedule.ActivityId, 999, new(true))).Result
        );
        Assert.False(s.Schedule.IsCancelled);
        Assert.Null(s.Schedule.CancelledAt);
    }

    [Fact]
    public async Task CancelledDropInWithUnknownSignupRole_HasZeroKarma()
    {
        using var db = CreateDb();
        var s = await SeedAsync(db);
        s.Schedule.Activity.IsTimeExclusive = false;
        s.Signup.Role = "LegacyObserver";
        await db.SaveChangesAsync(Ct);
        ScheduleValue(await Activities(db).SetCancellation(s.Schedule.ActivityId, s.Schedule.Id, new(true)));
        var totals = await new KarmaService(db).CalculateAsync([s.Participant.Id], Ct);
        Assert.Equal(0, totals[s.Participant.Id]);
    }
}
