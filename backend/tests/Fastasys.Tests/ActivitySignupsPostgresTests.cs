using System.Data.Common;
using Fastasys.ApiService.Controllers;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Fastasys.Tests;

public class ActivitySignupsPostgresTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private const string PreviousMigration = "20261104205249_AddRuns";

    [PostgresFact]
    public async Task Migration_PreservesExistingData_EnforcesKeysAndReferences_AndGuardsRollback()
    {
        await using var database = await PostgresTestDatabase.CreateAsync(Ct);
        await VerifyMigrationAsync(database.Options);
    }

    private static async Task VerifyMigrationAsync(DbContextOptions<InfosysDbContext> options)
    {
        await using var db = new InfosysDbContext(options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration, Ct);

        var players = new[]
        {
            new Participant { FirstName = "Player" },
            new Participant { FirstName = "Game master" },
            new Participant { FirstName = "Unknown legacy role" }
        };
        var schedule = new Schedule
        {
            Activity = new Activity { Title = "Migration test", GameMastersPerTeam = 1 },
            StartTime = new DateTime(2027, 3, 24, 10, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2027, 3, 24, 14, 0, 0, DateTimeKind.Utc)
        };
        db.AddRange(players);
        db.Add(schedule.Activity);
        await db.SaveChangesAsync(Ct);
        // Seed the previous schema without asking the current EF model to insert future cancellation columns.
        schedule.ActivityId = schedule.Activity.Id;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO schedule ("ActivityId", "StartTime", "EndTime")
            VALUES ({schedule.ActivityId}, {schedule.StartTime}, {schedule.EndTime})
            """, Ct);
        schedule.Id = await db.Database.SqlQuery<int>($"""
            SELECT "Id" AS "Value" FROM schedule WHERE "ActivityId" = {schedule.ActivityId}
            """).SingleAsync(Ct);
        var roles = new[] { "Player", "GameMaster", "LegacyObserver" };
        // Whole seconds avoid rounding differences between .NET ticks and PostgreSQL microseconds.
        var signedUpAt = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < players.Length; i++)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO participant_schedules ("ParticipantId", "ScheduleId", "Role", "SignedUpAt")
                VALUES ({players[i].Id}, {schedule.Id}, {roles[i]}, {signedUpAt})
                """, Ct);
        }

        await migrator.MigrateAsync(cancellationToken: Ct);
        var migratedSchedule = await db.Schedules.AsNoTracking().SingleAsync(s => s.Id == schedule.Id, Ct);
        Assert.False(migratedSchedule.IsCancelled);
        Assert.Null(migratedSchedule.CancelledAt);
        var migrated = await db.ParticipantSchedules.AsNoTracking().OrderBy(s => s.ParticipantId).ToListAsync(Ct);
        Assert.Equal(3, migrated.Count);
        Assert.Equal(3, migrated.Select(s => s.Id).Distinct().Count());
        for (var i = 0; i < players.Length; i++)
        {
            Assert.True(migrated[i].Id > 0);
            Assert.Equal(players[i].Id, migrated[i].ParticipantId);
            Assert.Equal(schedule.Id, migrated[i].ScheduleId);
            Assert.Equal(roles[i], migrated[i].Role);
            Assert.Equal(signedUpAt, migrated[i].SignedUpAt);
            Assert.Null(migrated[i].Priority);
        }
        Assert.False(db.Database.HasPendingModelChanges());

        // The real database must support combined GM/player signups and generate fresh IDs after the backfill.
        var controller = new ActivitySignupsController(db);
        var gm = Assert.IsType<ActivitySignupDto>(Assert.IsType<CreatedAtActionResult>(
            (await controller.Create(players[0].Id, new(schedule.Id, RunSeatRole.GameMaster, 0))).Result).Value);
        var player = Assert.IsType<ActivitySignupDto>(Assert.IsType<CreatedAtActionResult>(
            (await controller.Create(players[0].Id, new(schedule.Id, RunSeatRole.Player, 1))).Result).Value);
        Assert.DoesNotContain(migrated, s => s.Id == gm.Id || s.Id == player.Id);
        var list = Assert.IsType<List<ActivitySignupDto>>(Assert.IsType<OkObjectResult>(
            (await controller.GetAll(players[0].Id)).Result).Value);
        Assert.Equal(3, list.Count);
        Assert.Equal(player, Assert.IsType<OkObjectResult>((await controller.Get(players[0].Id, player.Id)).Result).Value);
        var update = Assert.IsType<ActivitySignupDto>(Assert.IsType<OkObjectResult>(
            (await controller.Update(players[0].Id, player.Id, new(schedule.Id, RunSeatRole.Player, 2))).Result).Value);
        Assert.Equal(2, update.Priority);
        Assert.Equal(player.SignedUpAt, update.SignedUpAt);
        Assert.IsType<OkObjectResult>((await controller.Update(players[0].Id, player.Id, new(schedule.Id, RunSeatRole.Player, 1))).Result);

        var commands = new QueryCounter();
        await using (var batched = new InfosysDbContext(new DbContextOptionsBuilder<InfosysDbContext>(options)
            .AddInterceptors(commands).Options))
        {
            var service = new KarmaService(batched);
            Assert.Empty(await service.CalculateAsync([], Ct));
            Assert.Equal(0, commands.Count);
            var totals = await service.CalculateAsync([players[0].Id, players[1].Id, players[2].Id, players[0].Id], Ct);
            Assert.Equal(2, commands.Count);
            Assert.Equal(-19, totals[players[0].Id].Karma.Total);
            Assert.Equal(-7, totals[players[1].Id].Karma.Total);
            Assert.Equal(0, totals[players[2].Id].Karma.Total);
            Assert.Equal(1, totals[players[2].Id].Karma.Exclusions.UnknownSignupRoles);
            var individual = await service.GetParticipantAsync(players[0].Id, Ct);
            Assert.Equal(-19, individual!.Karma.Total);
        }

        // Bypass controller prechecks to prove storage also protects against concurrent duplicate requests.
        await AssertRejectedAsync(options, new ParticipantSchedule
        {
            ParticipantId = players[0].Id,
            ScheduleId = schedule.Id,
            Priority = 1,
            Role = "GameMaster"
        }, PostgresErrorCodes.UniqueViolation);
        await AssertRejectedAsync(options, new ParticipantSchedule
        {
            ParticipantId = players[0].Id,
            ScheduleId = schedule.Id,
            Priority = null
        }, PostgresErrorCodes.UniqueViolation);
        await AssertRejectedAsync(options, new ParticipantSchedule
        {
            ParticipantId = int.MaxValue,
            ScheduleId = schedule.Id,
            Priority = 1
        }, PostgresErrorCodes.ForeignKeyViolation);
        await AssertRejectedAsync(options, new ParticipantSchedule
        {
            ParticipantId = players[0].Id,
            ScheduleId = int.MaxValue,
            Priority = 1
        }, PostgresErrorCodes.ForeignKeyViolation);

        await using (var deletion = new InfosysDbContext(options))
        {
            deletion.Remove(new Schedule { Id = schedule.Id });
            var failure = await Assert.ThrowsAsync<DbUpdateException>(() => deletion.SaveChangesAsync(Ct));
            Assert.Equal(PostgresErrorCodes.RestrictViolation, Assert.IsType<PostgresException>(failure.InnerException).SqlState);
        }

        // Rollback must fail before dropping columns if the old composite key would lose preferences.
        var rollback = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(PreviousMigration, Ct));
        Assert.Contains("multiple preferences", rollback.MessageText);
        Assert.Equal(5, await db.ParticipantSchedules.CountAsync(Ct));
        Assert.Equal(0, (await db.ParticipantSchedules.AsNoTracking().SingleAsync(s => s.Id == gm.Id, Ct)).Priority);

        await controller.Delete(players[0].Id, gm.Id);
        await controller.Delete(players[0].Id, player.Id);
        await migrator.MigrateAsync(PreviousMigration, Ct);
        // The older schema no longer has Id/Priority, so inspect the preserved legacy columns directly.
        var oldRows = await db.Database.SqlQuery<LegacySignupRow>($"""
            SELECT "ParticipantId", "ScheduleId", "Role", "SignedUpAt" FROM participant_schedules
            ORDER BY "ParticipantId"
            """).ToListAsync(Ct);
        Assert.Equal(3, oldRows.Count);
        for (var i = 0; i < oldRows.Count; i++)
        {
            Assert.Equal(players[i].Id, oldRows[i].ParticipantId);
            Assert.Equal(schedule.Id, oldRows[i].ScheduleId);
            Assert.Equal(roles[i], oldRows[i].Role);
            Assert.Equal(signedUpAt, oldRows[i].SignedUpAt);
        }
        await migrator.MigrateAsync(cancellationToken: Ct);
        Assert.Equal(3, await db.ParticipantSchedules.CountAsync(Ct));
    }

    private static async Task AssertRejectedAsync(DbContextOptions<InfosysDbContext> options, ParticipantSchedule signup, string code)
    {
        await using var db = new InfosysDbContext(options);
        db.ParticipantSchedules.Add(signup);
        var failure = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
        Assert.Equal(code, Assert.IsType<PostgresException>(failure.InnerException).SqlState);
    }

    private sealed record LegacySignupRow(int ParticipantId, int ScheduleId, string Role, DateTime SignedUpAt);

    private sealed class QueryCounter : DbCommandInterceptor
    {
        public int Count { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Count++;
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
