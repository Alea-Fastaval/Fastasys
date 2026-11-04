using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Fastasys.ApiService.Controllers;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Services;

namespace Fastasys.Tests;

// Infosys: viewing a hold needs a role that grants the Groups controller; creating, editing, deleting and
// placing participants also needs the role Activity-admin, Infonaut or admin.
// Fastasys maps that onto the activities_view / activities_edit privileges.
public class RunsAuthorizationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public RunsAuthorizationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> CreateClientWithPrivilegeAsync(string privilegeKey)
    {
        var suffix = Guid.NewGuid().ToString("N");
        const string password = "test-password";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InfosysDbContext>();
            var role = new Role { Name = $"role-{suffix}" };
            role.RolePrivileges.Add(new RolePrivilege { Privilege = new Privilege { Name = privilegeKey, Key = privilegeKey } });
            var user = new User
            {
                Username = $"user-{suffix}",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Email = $"{suffix}@example.invalid",
                IsActive = true
            };
            user.UserRoles.Add(new UserRole { Role = role });
            db.Users.Add(user);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var loginScope = _factory.Services.CreateScope();
        var auth = await loginScope.ServiceProvider.GetRequiredService<IAuthService>()
            .LoginAsync(new LoginDto($"user-{suffix}", password));
        Assert.NotNull(auth);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }

    private async Task<T> AddAsync<T>(T entity) where T : class
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InfosysDbContext>();
        db.Add(entity);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return entity;
    }

    [Fact]
    public async Task Anonymous_CannotListRuns()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/runs", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserWithoutActivityPrivilege_IsForbidden()
    {
        var client = await CreateClientWithPrivilegeAsync("participants_view");
        var ct = TestContext.Current.CancellationToken;

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/runs", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/runs/1", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/activities/1/schedules", ct)).StatusCode);
    }

    [Fact]
    public async Task ViewPrivilege_CanRead_ButNotWrite()
    {
        var client = await CreateClientWithPrivilegeAsync("activities_view");
        var ct = TestContext.Current.CancellationToken;

        var list = await client.GetAsync("/api/runs", ct);
        var createSchedule = await client.PostAsJsonAsync("/api/activities/1/schedules", new CreateScheduleDto(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), null), ct);
        var create = await client.PostAsJsonAsync("/api/runs", new CreateRunDto(1), ct);
        var rooms = await client.GetAsync("/api/runs/1/available-rooms", ct);
        var update = await client.PutAsJsonAsync("/api/runs/1", new UpdateRunDto(1), ct);
        var delete = await client.DeleteAsync("/api/runs/1?removeParticipants=true", ct);
        var addSeat = await client.PostAsJsonAsync("/api/runs/1/seats", new CreateRunSeatDto(1, RunSeatRole.Player), ct);
        var removeSeat = await client.DeleteAsync("/api/runs/1/seats/1", ct);

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, createSchedule.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, rooms.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, addSeat.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, removeSeat.StatusCode);
    }

    [Fact]
    public async Task EditPrivilege_CanCreateScheduleAndRun_PlaceParticipants_ChangeRoom_AndDelete()
    {
        var client = await CreateClientWithPrivilegeAsync("activities_edit");
        var ct = TestContext.Current.CancellationToken;

        // The test database is shared, so use a time nobody else books
        var start = new DateTimeOffset(2100, 1, 1, 10, 0, 0, TimeSpan.Zero).AddDays(Random.Shared.Next(0, 300_000));
        var activity = await AddAsync(new Activity { Title = "Holdtest", MinPlayersPerTeam = 1, MaxPlayersPerTeam = 2, GameMastersPerTeam = 1 });
        await AddAsync(new Room { Name = "Holdtest lokale A" });
        var participant = await AddAsync(new Participant { FirstName = "Test", LastName = "Spilleder" });

        var scheduleResponse = await client.PostAsJsonAsync($"/api/activities/{activity.Id}/schedules", new CreateScheduleDto(start, start.AddHours(4), null), ct);
        Assert.Equal(HttpStatusCode.Created, scheduleResponse.StatusCode);
        var schedule = await scheduleResponse.Content.ReadFromJsonAsync<ScheduleDto>(ct);
        Assert.NotNull(schedule);

        var runResponse = await client.PostAsJsonAsync("/api/runs", new CreateRunDto(schedule.Id), ct);
        Assert.Equal(HttpStatusCode.Created, runResponse.StatusCode);
        var run = await runResponse.Content.ReadFromJsonAsync<RunDto>(ct);
        Assert.NotNull(run);

        var addSeat = await client.PostAsJsonAsync($"/api/runs/{run.Id}/seats", new CreateRunSeatDto(participant.Id, RunSeatRole.GameMaster), ct);
        Assert.Equal(HttpStatusCode.OK, addSeat.StatusCode);

        // The role is serialized by name and karma is blank until karma is ported
        var get = await client.GetAsync($"/api/runs/{run.Id}", ct);
        using (var json = JsonDocument.Parse(await get.Content.ReadAsStringAsync(ct)))
        {
            var seat = json.RootElement.GetProperty("seats")[0];
            Assert.Equal("GameMaster", seat.GetProperty("role").GetString());
            Assert.Equal(JsonValueKind.Null, seat.GetProperty("karma").ValueKind);
            Assert.False(json.RootElement.GetProperty("needsGameMasters").GetBoolean());
            Assert.True(json.RootElement.GetProperty("needsPlayers").GetBoolean());
        }

        var otherRoom = await AddAsync(new Room { Name = "Holdtest lokale B" });
        var rooms = await client.GetAsync($"/api/runs/{run.Id}/available-rooms", ct);
        var update = await client.PutAsJsonAsync($"/api/runs/{run.Id}", new UpdateRunDto(otherRoom.Id), ct);
        var deleteRefused = await client.DeleteAsync($"/api/runs/{run.Id}", ct);
        var removeSeat = await client.DeleteAsync($"/api/runs/{run.Id}/seats/{participant.Id}", ct);
        var delete = await client.DeleteAsync($"/api/runs/{run.Id}", ct);

        Assert.Equal(HttpStatusCode.OK, rooms.StatusCode);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, deleteRefused.StatusCode);
        Assert.Equal(HttpStatusCode.OK, removeSeat.StatusCode);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
    }
}
