using System.Security.Claims;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fastasys.Tests;

// Every saved change to a participant's amount paid is logged with the old and the new amount and the user (asked for in
// a PR review and agreed on 2026-11-21)
public class AmountPaidLogInterceptorTests
{
    private sealed class ListLogger : ILogger<AmountPaidLogInterceptor>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }

    private sealed class FakeHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }

    // A context whose saves are logged as made by user 3
    private static (InfosysDbContext Db, List<string> Log) Create(string? databaseName = null)
    {
        var logger = new ListLogger();
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "3")]));
        var accessor = new FakeHttpContextAccessor { HttpContext = new DefaultHttpContext { User = user } };
        var options = new DbContextOptionsBuilder<InfosysDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .AddInterceptors(new AmountPaidLogInterceptor(logger, accessor))
            .Options;
        return (new InfosysDbContext(options), logger.Messages);
    }

    [Fact]
    public async Task LogsAChangeWithTheOldAndTheNewAmountAndTheUser()
    {
        var (db, log) = Create();
        using var _ = db;
        db.Participants.Add(new Participant { Id = 1 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        (await db.Participants.SingleAsync(TestContext.Current.CancellationToken)).AmountPaid = 450;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["Participant 1's amount paid changed from 0 to 450 by user 3"], log);
    }

    [Fact]
    public void LogsASynchronousSaveToo()
    {
        var (db, log) = Create();
        using var _ = db;
        db.Participants.Add(new Participant { Id = 1, AmountPaid = 450 });
        db.SaveChanges();
        log.Clear();

        db.Participants.Single().AmountPaid = -50;
        db.SaveChanges();

        Assert.Equal(["Participant 1's amount paid changed from 450 to -50 by user 3"], log);
    }

    [Fact]
    public async Task LogsNothingWhenTheAmountIsUnchanged()
    {
        var (db, log) = Create();
        using var _ = db;
        db.Participants.Add(new Participant { Id = 1, AmountPaid = 450 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        log.Clear();

        var participant = await db.Participants.SingleAsync(TestContext.Current.CancellationToken);
        participant.FirstName = "Testa";
        participant.AmountPaid = 450;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Empty(log);
    }

    [Fact]
    public async Task LogsANewParticipantOnlyWhenTheyStartWithAnAmount()
    {
        var (db, log) = Create();
        using var _ = db;
        db.Participants.Add(new Participant { Id = 1 });
        db.Participants.Add(new Participant { Id = 2, AmountPaid = 450 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["Participant 2 created with amount paid 450 by user 3"], log);
    }

    [Fact]
    public async Task LogsNothingWhenTheSaveFails()
    {
        var databaseName = Guid.NewGuid().ToString();
        var (first, _) = Create(databaseName);
        using var firstDb = first;
        first.Participants.Add(new Participant { Id = 1 });
        await first.SaveChangesAsync(TestContext.Current.CancellationToken);

        var (second, log) = Create(databaseName);
        using var secondDb = second;
        second.Participants.Add(new Participant { Id = 1, AmountPaid = 450 });

        await Assert.ThrowsAnyAsync<Exception>(() => second.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Empty(log);
    }

    [Fact]
    public void TheApiLogsItsSaves()
    {
        using var factory = new TestWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InfosysDbContext>();

        var interceptors = db.GetService<IDbContextOptions>().FindExtension<CoreOptionsExtension>()?.Interceptors ?? [];

        Assert.Contains(interceptors, i => i is AmountPaidLogInterceptor);
    }
}
