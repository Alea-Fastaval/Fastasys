using System.Net.Http.Headers;
using System.Net.Http.Json;
using Fastasys.ApiService.Controllers;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Services;

namespace Fastasys.Tests;

public class ActivitySignupsAuthorizationTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<HttpClient> ClientAsync(string privilege, bool admin = false)
    {
        var name = $"signup-{Guid.NewGuid():N}";
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InfosysDbContext>();
            var role = new Role { Name = admin ? "Admin" : name };
            role.RolePrivileges.Add(
                new RolePrivilege
                {
                    Privilege = new Privilege { Name = privilege, Key = privilege },
                }
            );
            var user = new User
            {
                Username = name,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password"),
                IsActive = true,
            };
            user.UserRoles.Add(new UserRole { Role = role });
            db.Users.Add(user);
            await db.SaveChangesAsync(Ct);
        }
        using var loginScope = factory.Services.CreateScope();
        var auth = await loginScope
            .ServiceProvider.GetRequiredService<IAuthService>()
            .LoginAsync(new LoginDto(name, "password"));
        Assert.NotNull(auth);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("participant_management", HttpStatusCode.Forbidden)]
    public async Task MissingActivityAccess_CannotReadOrWrite(string? privilege, HttpStatusCode expected)
    {
        using var client = privilege == null ? factory.CreateClient() : await ClientAsync(privilege);
        const string url = "/api/participants/1/activity-signups";

        Assert.Equal(expected, (await client.GetAsync(url, Ct)).StatusCode);
        Assert.Equal(expected, (await client.GetAsync($"{url}/1", Ct)).StatusCode);
        Assert.Equal(
            expected,
            (await client.PostAsJsonAsync(url, new ActivitySignupFieldsDto(1, RunSeatRole.Player, 1), Ct)).StatusCode
        );
        Assert.Equal(
            expected,
            (
                await client.PutAsJsonAsync($"{url}/1", new ActivitySignupFieldsDto(1, RunSeatRole.Player, 2), Ct)
            ).StatusCode
        );
        Assert.Equal(expected, (await client.DeleteAsync($"{url}/1", Ct)).StatusCode);
    }

    [Fact]
    public async Task ViewPrivilege_CanReadButCannotChangePreferences()
    {
        using var client = await ClientAsync("activities_view");
        const string url = "/api/participants/999999/activity-signups";

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(url, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{url}/1", Ct)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync(url, new ActivitySignupFieldsDto(1, RunSeatRole.Player, 1), Ct)).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (
                await client.PutAsJsonAsync($"{url}/1", new ActivitySignupFieldsDto(1, RunSeatRole.Player, 2), Ct)
            ).StatusCode
        );
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"{url}/1", Ct)).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EditorOrAdmin_CanManagePreferences_AndInvalidJsonIsRejected(bool admin)
    {
        using var client = await ClientAsync(admin ? "unrelated" : "activities_edit", admin);
        int participantId;
        int scheduleId;
        int runId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InfosysDbContext>();
            var participant = new Participant { FirstName = "Signup test" };
            var schedule = new Schedule
            {
                Activity = new Activity { Title = "Signup test", GameMastersPerTeam = 1 },
                StartTime = new DateTime(2027, 3, 24, 10, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2027, 3, 24, 14, 0, 0, DateTimeKind.Utc),
            };
            var run = new Run
            {
                Schedule = schedule,
                Number = 1,
                Room = new Room { Name = "Signup test room" },
            };
            run.Seats.Add(new RunSeat { Participant = participant, SeatNumber = 1 });
            db.Add(run);
            await db.SaveChangesAsync(Ct);
            participantId = participant.Id;
            scheduleId = schedule.Id;
            runId = run.Id;
        }
        async Task<int> CurrentKarma()
        {
            var run = await client.GetFromJsonAsync<RunDto>($"/api/runs/{runId}", Ct);
            return Assert.Single(run!.Seats).Karma;
        }
        var url = $"/api/participants/{participantId}/activity-signups";
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url, Ct)).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(url, new { scheduleId }, Ct)).StatusCode);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (
                await client.PostAsJsonAsync(
                    url,
                    new
                    {
                        scheduleId,
                        role = "Observer",
                        priority = 1,
                    },
                    Ct
                )
            ).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (
                await client.PostAsJsonAsync(
                    url,
                    new
                    {
                        scheduleId,
                        role = 99,
                        priority = 1,
                    },
                    Ct
                )
            ).StatusCode
        );

        var create = await client.PostAsJsonAsync(
            url,
            new ActivitySignupFieldsDto(scheduleId, RunSeatRole.Player, 1),
            Ct
        );
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var signup = await create.Content.ReadFromJsonAsync<ActivitySignupDto>(Ct);
        Assert.NotNull(signup);
        Assert.Equal("Player", signup.Role);
        Assert.Equal(1, signup.Priority);
        Assert.Equal(-12, await CurrentKarma());
        var cancellationUrl = $"/api/activities/{signup.ActivityId}/schedules/{scheduleId}/cancellation";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(cancellationUrl, new { }, Ct)).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PutAsJsonAsync(cancellationUrl, new UpdateScheduleCancellationDto(true), Ct)).StatusCode
        );
        Assert.Equal(0, await CurrentKarma());
        var retained = await client.GetFromJsonAsync<ActivitySignupDto>($"{url}/{signup.Id}", Ct);
        Assert.True(retained!.IsCancelled);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (
                await client.PostAsJsonAsync(
                    url,
                    new ActivitySignupFieldsDto(scheduleId, RunSeatRole.GameMaster, 0),
                    Ct
                )
            ).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (
                await client.PutAsJsonAsync(
                    $"{url}/{signup.Id}",
                    new ActivitySignupFieldsDto(scheduleId, RunSeatRole.Player, 2),
                    Ct
                )
            ).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PutAsJsonAsync(cancellationUrl, new UpdateScheduleCancellationDto(false), Ct)).StatusCode
        );
        Assert.Equal(-12, await CurrentKarma());

        Assert.EndsWith($"{url}/{signup.Id}", create.Headers.Location?.ToString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(create.Headers.Location, Ct)).StatusCode);

        Assert.Equal(
            HttpStatusCode.Conflict,
            (
                await client.PostAsJsonAsync(url, new ActivitySignupFieldsDto(scheduleId, RunSeatRole.Player, 1), Ct)
            ).StatusCode
        );
        var update = await client.PutAsJsonAsync(
            $"{url}/{signup.Id}",
            new ActivitySignupFieldsDto(scheduleId, RunSeatRole.GameMaster, 0),
            Ct
        );
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var changed = await update.Content.ReadFromJsonAsync<ActivitySignupDto>(Ct);
        Assert.NotNull(changed);
        Assert.Equal("GameMaster", changed.Role);
        Assert.Equal(0, changed.Priority);
        Assert.Equal(-7, await CurrentKarma());

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{url}/{signup.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{url}/{signup.Id}", Ct)).StatusCode);
        Assert.Equal(0, await CurrentKarma());
    }
}
