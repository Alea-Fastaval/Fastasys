using System.Net.Http.Headers;
using System.Net.Http.Json;
using Fastasys.ApiService.Controllers;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Services;

namespace Fastasys.Tests;

public class ActivitySettingsTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private const string Url = "/api/management/activity-settings";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<HttpClient> ClientAsync(string? privilege, bool admin = false)
    {
        if (privilege == null)
            return factory.CreateClient();
        var username = $"settings-{Guid.NewGuid():N}";
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
    [InlineData("activity_view", false, HttpStatusCode.Forbidden)]
    [InlineData("participant_view", false, HttpStatusCode.Forbidden)]
    [InlineData("activity_edit", false, HttpStatusCode.OK)]
    [InlineData("unrelated", true, HttpStatusCode.OK)]
    public async Task SettingsRequireActivityEditAccess(string? privilege, bool admin, HttpStatusCode expected)
    {
        using var client = await ClientAsync(privilege, admin);
        Assert.Equal(expected, (await client.GetAsync(Url, Ct)).StatusCode);
        if (expected != HttpStatusCode.OK)
        {
            var values = new ActivitySettingsDto
            {
                SmallKarma = 7,
                MediumKarma = 0,
                BigKarma = 10,
            };
            Assert.Equal(expected, (await client.PutAsJsonAsync(Url, values, Ct)).StatusCode);
        }
    }

    [Fact]
    public async Task SettingsPersist_RejectInvalidValues_AndImmediatelyAffectPlanningScores()
    {
        using var client = await ClientAsync("activity_edit");
        var defaults = await client.GetFromJsonAsync<ActivitySettingsDto>(Url, Ct);
        Assert.Equal(
            new ActivitySettingsDto
            {
                SmallKarma = 7,
                MediumKarma = 0,
                BigKarma = 10,
            },
            defaults
        );

        int runId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InfosysDbContext>();
            var run = new Run
            {
                Number = 1,
                Room = new Room { Name = "Settings room" },
                Schedule = new Schedule
                {
                    Activity = new Activity { Title = "Settings game", KarmaType = KarmaType.Medium },
                    StartTime = new DateTime(2027, 3, 24, 10, 0, 0, DateTimeKind.Utc),
                    EndTime = new DateTime(2027, 3, 24, 14, 0, 0, DateTimeKind.Utc),
                },
            };
            run.Seats.Add(
                new RunSeat
                {
                    Participant = new Participant { FirstName = "Settings player" },
                    SeatNumber = 1,
                }
            );
            db.Runs.Add(run);
            await db.SaveChangesAsync(Ct);
            runId = run.Id;
        }
        async Task<int> CurrentKarma() =>
            Assert.Single((await client.GetFromJsonAsync<RunDto>($"/api/runs/{runId}", Ct))!.Seats).Karma;
        Assert.Equal(0, await CurrentKarma());
        var values = new ActivitySettingsDto
        {
            SmallKarma = 2,
            MediumKarma = 4,
            BigKarma = 9,
        };
        var response = await client.PutAsJsonAsync(Url, values, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(values, await response.Content.ReadFromJsonAsync<ActivitySettingsDto>(Ct));
        Assert.Equal(values, await client.GetFromJsonAsync<ActivitySettingsDto>(Url, Ct));
        Assert.Equal(4, await CurrentKarma());

        object[] invalid =
        [
            new { },
            new
            {
                smallKarma = -1,
                mediumKarma = 4,
                bigKarma = 9,
            },
            new
            {
                smallKarma = 2,
                mediumKarma = 1001,
                bigKarma = 9,
            },
            new
            {
                smallKarma = 2,
                mediumKarma = 4,
                bigKarma = 1.5,
            },
        ];
        foreach (var input in invalid)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(Url, input, Ct)).StatusCode);
            Assert.Equal(values, await client.GetFromJsonAsync<ActivitySettingsDto>(Url, Ct));
        }
        Assert.Equal(4, await CurrentKarma());
    }
}
