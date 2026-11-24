using System.Data.Common;
using Fastasys.ApiService.Controllers;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Fastasys.Tests;

public class ScheduleCancellationPostgresTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    // The migration before the karma settings and schedule cancellation
    private const string PreviousMigration = "20261121115314_WidenNickname";

    [PostgresFact]
    public async Task MigrationPreservesRecords_AndCancellationSerializesWithNewAllocations()
    {
        await using var database = await PostgresTestDatabase.CreateAsync(Ct);
        await VerifyAsync(database.Options);
    }

    [PostgresFact]
    public async Task SeatCreationValidatesSeatsCommittedWhileWaitingForTheScheduleLock()
    {
        await using var database = await PostgresTestDatabase.CreateAsync(Ct);
        await using var db = new InfosysDbContext(database.Options);
        await db.Database.MigrateAsync(Ct);
        var participant = new Participant { FirstName = "Last seat" };
        var waiting = new Participant { FirstName = "Waiting" };
        var run = new Run
        {
            Schedule = new Schedule
            {
                Activity = new Activity { Title = "One player", MaxPlayersPerTeam = 1 },
                StartTime = new DateTime(2027, 3, 24, 9, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2027, 3, 24, 13, 0, 0, DateTimeKind.Utc),
            },
            Room = new Room { Name = "Test room" },
            Number = 1,
        };
        db.AddRange(run, participant, waiting);
        await db.SaveChangesAsync(Ct);

        await using var reservation = await ScheduleWriteLock.AcquireAsync(db, run.ScheduleId);
        db.RunSeats.Add(
            new RunSeat
            {
                RunId = run.Id,
                SeatNumber = 1,
                ParticipantId = participant.Id,
            }
        );
        await db.SaveChangesAsync(Ct);
        var signal = new LockAttempt();
        await using var allocating = new InfosysDbContext(
            new DbContextOptionsBuilder<InfosysDbContext>(database.Options).AddInterceptors(signal).Options
        );
        var request = new RunsController(
            allocating,
            NullLogger<RunsController>.Instance,
            new KarmaService(allocating)
        ).CreateSeat(run.Id, new(waiting.Id, RunSeatRole.Player));
        await signal.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        Assert.False(request.IsCompleted);
        await reservation!.CommitAsync(Ct);

        var conflict = Assert.IsType<ConflictObjectResult>(
            (await request.WaitAsync(TimeSpan.FromSeconds(10), Ct)).Result
        );
        Assert.Contains("cannot use more players", conflict.Value!.ToString());
        Assert.Equal(participant.Id, (await db.RunSeats.SingleAsync(Ct)).ParticipantId);
    }

    [PostgresFact]
    public async Task RoomUpdateReturnsNotFoundIfRunIsDeletedWhileWaitingForTheScheduleLock()
    {
        await using var database = await PostgresTestDatabase.CreateAsync(Ct);
        await using var db = new InfosysDbContext(database.Options);
        await db.Database.MigrateAsync(Ct);
        var run = new Run
        {
            Schedule = new Schedule
            {
                Activity = new Activity { Title = "Deleted run" },
                StartTime = new DateTime(2027, 3, 24, 9, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2027, 3, 24, 13, 0, 0, DateTimeKind.Utc),
            },
            Room = new Room { Name = "Test room" },
            Number = 1,
        };
        db.Add(run);
        await db.SaveChangesAsync(Ct);

        await using var deletion = await ScheduleWriteLock.AcquireAsync(db, run.ScheduleId);
        var signal = new LockAttempt();
        await using var updating = new InfosysDbContext(
            new DbContextOptionsBuilder<InfosysDbContext>(database.Options).AddInterceptors(signal).Options
        );
        var request = new RunsController(
            updating,
            NullLogger<RunsController>.Instance,
            new KarmaService(updating)
        ).Update(run.Id, new(run.RoomId));
        await signal.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        Assert.False(request.IsCompleted);
        db.Remove(run);
        await db.SaveChangesAsync(Ct);
        await deletion!.CommitAsync(Ct);
        Assert.IsType<NotFoundObjectResult>((await request.WaitAsync(TimeSpan.FromSeconds(10), Ct)).Result);
    }

    private static async Task VerifyAsync(DbContextOptions<InfosysDbContext> options)
    {
        await using var db = new InfosysDbContext(options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration, Ct);
        var participant = new Participant { FirstName = "Original" };
        var other = new Participant { FirstName = "New signup" };
        var room = new Room { Name = "Cancellation test room" };
        var activity = new Activity
        {
            Title = "Cancellation test",
            KarmaType = KarmaType.Big,
            MaxPlayersPerTeam = 4,
        };
        db.AddRange(participant, other, room, activity);
        await db.SaveChangesAsync(Ct);
        var start = new DateTime(2027, 3, 24, 9, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(4);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO schedule ("ActivityId", "StartTime", "EndTime") VALUES ({activity.Id}, {start}, {end})
            """,
            Ct
        );
        var scheduleId = await db
            .Database.SqlQuery<int>(
                $"""
                SELECT "Id" AS "Value" FROM schedule WHERE "ActivityId" = {activity.Id}
                """
            )
            .SingleAsync(Ct);
        // The signup and run tables are unchanged by the migration, so the current model can write them
        db.ScheduleSignups.Add(
            new ScheduleSignup
            {
                ParticipantId = participant.Id,
                ScheduleId = scheduleId,
                Role = RunSeatRole.Player,
                Priority = 1,
            }
        );
        var run = new Run
        {
            ScheduleId = scheduleId,
            RoomId = room.Id,
            Number = 1,
        };
        run.Seats.Add(new RunSeat { ParticipantId = participant.Id, SeatNumber = 1 });
        db.Add(run);
        await db.SaveChangesAsync(Ct);

        await migrator.MigrateAsync(cancellationToken: Ct);
        var schedule = await db.Schedules.AsNoTracking().SingleAsync(Ct);
        Assert.False(schedule.IsCancelled);
        Assert.Null(schedule.CancelledAt);
        Assert.False(db.Database.HasPendingModelChanges());
        var defaults = await db.ActivitySettings.AsNoTracking().SingleAsync(Ct);
        Assert.Equal(ActivitySettings.SingletonId, defaults.Id);
        Assert.Equal(7, defaults.SmallKarma);
        Assert.Equal(0, defaults.MediumKarma);
        Assert.Equal(10, defaults.BigKarma);
        Assert.Equal(1, (await db.ScheduleSignups.AsNoTracking().SingleAsync(Ct)).Priority);

        async Task<ScheduleDto> Change(bool cancelled)
        {
            await using var context = new InfosysDbContext(options);
            var action = await new ActivitiesController(
                context,
                NullLogger<ActivitiesController>.Instance,
                new KarmaService(context)
            ).SetCancellation(activity.Id, scheduleId, new(cancelled));
            return Assert.IsType<ScheduleDto>(Assert.IsType<OkObjectResult>(action.Result).Value);
        }

        Assert.True((await Change(true)).IsCancelled);
        Assert.Equal(0, (await new KarmaService(db).CalculateAsync([participant.Id], Ct))[participant.Id]);
        var preserved = await db.ScheduleSignups.AsNoTracking().SingleAsync(Ct);
        Assert.Equal(participant.Id, preserved.ParticipantId);
        Assert.Equal(1, preserved.Priority);
        Assert.Single(await db.RunSeats.AsNoTracking().ToListAsync(Ct));
        Assert.False((await Change(false)).IsCancelled);
        Assert.Equal(-2, (await new KarmaService(db).CalculateAsync([participant.Id], Ct))[participant.Id]);

        // Hold cancellation uncommitted. Each API write must wait on the same row before checking state.
        for (var operation = 1; operation < 3; operation++)
        {
            await Change(false);
            await using var cancelling = new InfosysDbContext(options);
            await using var transaction = await cancelling.Database.BeginTransactionAsync(Ct);
            var now = DateTimeOffset.UtcNow;
            await cancelling.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE schedule SET "IsCancelled" = true, "CancelledAt" = {now} WHERE "Id" = {scheduleId}
                """,
                Ct
            );
            var signal = new LockAttempt();
            await using var allocating = new InfosysDbContext(
                new DbContextOptionsBuilder<InfosysDbContext>(options).AddInterceptors(signal).Options
            );
            async Task<IActionResult> Write()
            {
                var controller = new RunsController(
                    allocating,
                    NullLogger<RunsController>.Instance,
                    new KarmaService(allocating)
                );
                return operation == 1
                    ? (await controller.Create(new(scheduleId))).Result!
                    : (await controller.CreateSeat(run.Id, new(other.Id, RunSeatRole.Player))).Result!;
            }
            var write = Write();
            await signal.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
            Assert.False(write.IsCompleted);
            await transaction.CommitAsync(Ct);
            var result = await write.WaitAsync(TimeSpan.FromSeconds(10), Ct);
            Assert.IsType<ConflictObjectResult>(result);
            Assert.Equal(1, await db.ScheduleSignups.CountAsync(Ct));
            Assert.Equal(1, await db.Runs.CountAsync(Ct));
            Assert.Equal(1, await db.RunSeats.CountAsync(Ct));
        }

        // A concurrent room allocation must commit before restoration checks retained bookings.
        await using (var reserving = new InfosysDbContext(options))
        {
            var competing = new Schedule
            {
                Activity = new Activity { Title = "Room competitor" },
                StartTime = start,
                EndTime = end,
            };
            reserving.Add(competing);
            await reserving.SaveChangesAsync(Ct);
            await using var reservation = await reserving.Database.BeginTransactionAsync(Ct);
            await ScheduleWriteLock.AcquireRoomsAsync(reserving);
            var competingRun = new Run
            {
                ScheduleId = competing.Id,
                RoomId = room.Id,
                Number = 1,
            };
            reserving.Add(competingRun);
            await reserving.SaveChangesAsync(Ct);
            var signal = new LockAttempt("\"Rooms\"");
            await using var restoring = new InfosysDbContext(
                new DbContextOptionsBuilder<InfosysDbContext>(options).AddInterceptors(signal).Options
            );
            var restore = new ActivitiesController(
                restoring,
                NullLogger<ActivitiesController>.Instance,
                new KarmaService(restoring)
            ).SetCancellation(activity.Id, scheduleId, new(false));
            await signal.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
            Assert.False(restore.IsCompleted);
            await reservation.CommitAsync(Ct);
            Assert.IsType<ConflictObjectResult>((await restore.WaitAsync(TimeSpan.FromSeconds(10), Ct)).Result);
            reserving.Remove(competingRun);
            reserving.Remove(competing);
            await reserving.SaveChangesAsync(Ct);
        }

        var refusal = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(PreviousMigration, Ct));
        Assert.Contains("cancelled occurrences", refusal.MessageText);
        Assert.True((await db.Schedules.AsNoTracking().SingleAsync(Ct)).IsCancelled);
        await Change(false);
        await migrator.MigrateAsync(PreviousMigration, Ct);
        Assert.Equal(
            start,
            await db
                .Database.SqlQuery<DateTime>(
                    $"""
                    SELECT "StartTime" AS "Value" FROM schedule WHERE "Id" = {scheduleId}
                    """
                )
                .SingleAsync(Ct)
        );
        await migrator.MigrateAsync(cancellationToken: Ct);
        Assert.False((await db.Schedules.AsNoTracking().SingleAsync(Ct)).IsCancelled);
        Assert.Equal(1, await db.RunSeats.CountAsync(Ct));
    }

    private sealed class LockAttempt(string target = "schedule") : DbCommandInterceptor
    {
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default
        )
        {
            if (
                command.CommandText.Contains("FOR UPDATE", StringComparison.Ordinal)
                && command.CommandText.Contains(target, StringComparison.Ordinal)
            )
                Started.TrySetResult(true);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
