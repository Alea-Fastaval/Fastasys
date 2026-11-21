using System.Data.Common;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Fastasys.Tests;

// The karma reads on PostgreSQL: a batch of participants costs the same three queries as one participant, and a
// participant may wish to both run and play the same schedule (schedule_signups' key is participant + schedule + role).
public class KarmaPostgresTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [PostgresFact]
    public async Task Batch_UsesThreeQueries_AndCountsGameMasterAndPlayerWishesOnTheSameSchedule()
    {
        await using var database = await PostgresTestDatabase.CreateAsync(Ct);
        var both = new Participant { FirstName = "Both" };
        var gameMaster = new Participant { FirstName = "Game master" };
        var none = new Participant { FirstName = "None" };
        await using (var db = new InfosysDbContext(database.Options))
        {
            await db.Database.MigrateAsync(Ct);
            var schedule = new Schedule
            {
                Activity = new Activity
                {
                    Title = "Karma test",
                    GameMastersPerTeam = 1,
                    IsTimeExclusive = true,
                },
                StartTime = new DateTime(2027, 3, 24, 10, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2027, 3, 24, 14, 0, 0, DateTimeKind.Utc),
            };
            db.AddRange(both, gameMaster, none, schedule);
            db.ScheduleSignups.AddRange(
                new ScheduleSignup
                {
                    Participant = both,
                    Schedule = schedule,
                    Role = RunSeatRole.GameMaster,
                    Priority = 0,
                },
                new ScheduleSignup
                {
                    Participant = both,
                    Schedule = schedule,
                    Role = RunSeatRole.Player,
                    Priority = 1,
                },
                new ScheduleSignup
                {
                    Participant = gameMaster,
                    Schedule = schedule,
                    Role = RunSeatRole.GameMaster,
                    Priority = 0,
                }
            );
            await db.SaveChangesAsync(Ct);
        }

        var commands = new QueryCounter();
        await using var counted = new InfosysDbContext(
            new DbContextOptionsBuilder<InfosysDbContext>(database.Options).AddInterceptors(commands).Options
        );
        var service = new KarmaService(counted);

        Assert.Empty(await service.CalculateAsync([], Ct));
        Assert.Equal(0, commands.Count);
        var totals = await service.CalculateAsync([both.Id, gameMaster.Id, none.Id, both.Id], Ct);
        Assert.Equal(3, commands.Count);
        Assert.Equal(-19, totals[both.Id]); // -7 for the game master wish, -12 for the single first priority
        Assert.Equal(-7, totals[gameMaster.Id]);
        Assert.Equal(0, totals[none.Id]);
        Assert.Equal(-19, (await service.CalculateAsync([both.Id], Ct))[both.Id]);
    }

    private sealed class QueryCounter : DbCommandInterceptor
    {
        public int Count { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default
        )
        {
            Count++;
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
