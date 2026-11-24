using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Fastasys.ApiService.Controllers;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fastasys.Tests;

// The signup's manual switch and status (agreed 2026-11-24): signup_form_edit may switch it, anyone may read the status.
public class SignupSettingsTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private const string Url = "/api/management/signup-settings";
    private const string StatusUrl = "/api/signup/status";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class ListLogger : ILogger<SignupSettingsController>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) => Messages.Add(formatter(state, exception));
    }

    private async Task<HttpClient> ClientAsync(string? privilege, bool admin = false)
    {
        if (privilege == null)
            return factory.CreateClient();
        var username = $"signup-settings-{Guid.NewGuid():N}";
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InfosysDbContext>();
        var role = new Role { Name = admin ? "Admin" : username };
        role.RolePrivileges.Add(
            new RolePrivilege
            {
                Privilege = new Privilege { Key = privilege, Name = privilege },
            }
        );
        var user = new User
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password"),
            IsActive = true,
        };
        user.UserRoles.Add(new UserRole { Role = role });
        db.Users.Add(user);
        await db.SaveChangesAsync(Ct);
        var auth = await scope.ServiceProvider.GetRequiredService<IAuthService>().LoginAsync(new(username, "password"));
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }

    [Theory]
    [InlineData(null, false, HttpStatusCode.Unauthorized)]
    [InlineData("participant_view", false, HttpStatusCode.Forbidden)]
    [InlineData("activity_edit", false, HttpStatusCode.Forbidden)]
    [InlineData("signup_stats_view", false, HttpStatusCode.Forbidden)]
    [InlineData("signup_form_edit", false, HttpStatusCode.OK)]
    [InlineData("unrelated", true, HttpStatusCode.OK)]
    public async Task TheSwitchRequiresSignupFormEdit(string? privilege, bool admin, HttpStatusCode expected)
    {
        using var client = await ClientAsync(privilege, admin);
        Assert.Equal(expected, (await client.GetAsync(Url, Ct)).StatusCode);
        if (expected != HttpStatusCode.OK)
        {
            var values = new UpdateSignupSettingsDto { IsOpen = true };
            Assert.Equal(expected, (await client.PutAsJsonAsync(Url, values, Ct)).StatusCode);
        }
    }

    [Fact]
    public async Task AnyoneMayReadTheStatus()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync(StatusUrl, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(await response.Content.ReadFromJsonAsync<SignupStatusDto>(Ct));
    }

    [Fact]
    public async Task TheSwitchPersists_AndRejectsAMissingValue()
    {
        using var client = await ClientAsync("signup_form_edit");

        var response = await client.PutAsJsonAsync(Url, new UpdateSignupSettingsDto { IsOpen = true }, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<SignupSettingsDto>(Ct))!.IsOpen);
        Assert.True((await client.GetFromJsonAsync<SignupSettingsDto>(Url, Ct))!.IsOpen);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(Url, new { }, Ct)).StatusCode);
        Assert.True((await client.GetFromJsonAsync<SignupSettingsDto>(Url, Ct))!.IsOpen);

        await client.PutAsJsonAsync(Url, new UpdateSignupSettingsDto { IsOpen = false }, Ct);
        Assert.False((await client.GetFromJsonAsync<SignupSettingsDto>(Url, Ct))!.IsOpen);
    }

    [Fact]
    public async Task TheStatusFollowsTheSwitchAndTheDates()
    {
        using var db = new InfosysDbContext(
            new DbContextOptionsBuilder<InfosysDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options
        );
        var convention = Options.Create(
            new ConventionOptions
            {
                Start = new DateOnly(2027, 3, 24),
                End = new DateOnly(2027, 3, 29),
                SignupStart = new DateOnly(2027, 2, 1),
                SignupEnd = new DateOnly(2027, 2, 28),
                JuniorSignupStart = new DateTime(2027, 1, 15, 10, 0, 0),
                PayByDay = new DateOnly(2027, 3, 30),
            }
        );
        var clock = new TestClock(new DateTimeOffset(2027, 2, 10, 12, 0, 0, TimeSpan.Zero));
        var logger = new ListLogger();
        var settings = new SignupSettingsController(db, convention, clock, logger)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "42")])),
                },
            },
        };
        var status = new SignupStatusController(db, convention, clock);
        async Task<SignupStatusDto> Status() => Value(await status.Get(Ct));

        // Closed while nobody has switched it on (here the row doesn't even exist), although the opening day has passed
        var closed = await Status();
        Assert.False(closed.IsSignupOpen);
        Assert.False(closed.IsJuniorSignupOpen);
        Assert.False(closed.IsLateSignup);
        Assert.Equal(new DateOnly(2027, 2, 1), closed.SignupStart);
        Assert.Equal(new DateOnly(2027, 2, 28), closed.SignupEnd);
        Assert.Equal(new DateTime(2027, 1, 15, 9, 0, 0, DateTimeKind.Utc), closed.JuniorSignupStart);
        Assert.Equal(DateTimeKind.Utc, closed.JuniorSignupStart?.Kind);
        Assert.Equal(new DateOnly(2027, 3, 24), closed.ConventionStart);
        Assert.Equal(new DateOnly(2027, 3, 29), closed.ConventionEnd);
        Assert.Equal(new DateOnly(2027, 3, 30), closed.PayByDay);

        // Switched on: open, and the junior signup too, since its start has passed
        var updated = Value(await settings.Update(new UpdateSignupSettingsDto { IsOpen = true }, Ct));
        Assert.True(updated.IsOpen);
        Assert.True(updated.Status.IsSignupOpen);
        Assert.True((await Status()).IsJuniorSignupOpen);
        Assert.Equal("Signup switch set to True by user 42", Assert.Single(logger.Messages));

        // Late from 00:00 Danish time on 1 March, and still open
        clock.Now = new DateTimeOffset(2027, 2, 28, 23, 0, 0, TimeSpan.Zero);
        var late = await Status();
        Assert.True(late.IsSignupOpen);
        Assert.True(late.IsLateSignup);

        // Before the opening day it stays closed, even though it is switched on
        clock.Now = new DateTimeOffset(2027, 1, 31, 22, 59, 59, TimeSpan.Zero);
        Assert.False((await Status()).IsSignupOpen);
        Assert.True(Value(await settings.Get(Ct)).IsOpen);

        // Switched off again
        clock.Now = new DateTimeOffset(2027, 2, 10, 12, 0, 0, TimeSpan.Zero);
        Value(await settings.Update(new UpdateSignupSettingsDto { IsOpen = false }, Ct));
        Assert.False((await Status()).IsSignupOpen);
        Assert.Equal("Signup switch set to False by user 42", logger.Messages[^1]);
    }

    private static T Value<T>(ActionResult<T> result) =>
        Assert.IsType<T>(Assert.IsType<OkObjectResult>(result.Result).Value);
}
