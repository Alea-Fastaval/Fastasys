using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Fastasys.ApiService.Controllers;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Services;

namespace Fastasys.Tests;

// Infosys: every activity admin page requires a logged-in user whose role grants the Activity controller method.
// Fastasys maps that onto the activities_view / activities_edit privileges.
public class ActivitiesAuthorizationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ActivitiesAuthorizationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static T NewActivity<T>(string title) where T : ActivityFieldsDto, new() => new T
    {
        Title = title,
        Type = ActivityType.Scenario,
        DurationHours = 4,
        MinPlayersPerTeam = 3,
        MaxPlayersPerTeam = 5,
        GameMastersPerTeam = 1,
        Price = 30,
        CanSignUp = true,
        IsRoomExclusive = true,
        IsTimeExclusive = true,
        PlayerLanguage = ActivityLanguage.Danish,
        GameMasterLanguage = ActivityLanguage.Danish,
        KarmaType = KarmaType.None,
        AutoSignupCategory = AutoSignupCategory.None
    };

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

    [Fact]
    public async Task Anonymous_CannotListActivities()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/activities", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserWithoutActivityPrivilege_IsForbidden()
    {
        var client = await CreateClientWithPrivilegeAsync("participants_view");

        var response = await client.GetAsync("/api/activities", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ViewPrivilege_CanRead_ButNotWrite()
    {
        var client = await CreateClientWithPrivilegeAsync("activities_view");
        var ct = TestContext.Current.CancellationToken;

        var list = await client.GetAsync("/api/activities", ct);
        var create = await client.PostAsJsonAsync("/api/activities", NewActivity<CreateActivityDto>("Kun læse"), ct);
        var update = await client.PutAsJsonAsync("/api/activities/1", NewActivity<UpdateActivityDto>("Kun læse"), ct);
        var delete = await client.DeleteAsync("/api/activities/1", ct);

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task EditPrivilege_CanCreateReadUpdateAndDelete()
    {
        var client = await CreateClientWithPrivilegeAsync("activities_edit");
        var ct = TestContext.Current.CancellationToken;

        var create = await client.PostAsJsonAsync("/api/activities", NewActivity<CreateActivityDto>("Redigerbar"), ct);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<ActivityDto>(ct);
        Assert.NotNull(created);

        var get = await client.GetAsync($"/api/activities/{created.Id}", ct);
        var update = await client.PutAsJsonAsync($"/api/activities/{created.Id}", NewActivity<UpdateActivityDto>("Redigeret"), ct);
        var delete = await client.DeleteAsync($"/api/activities/{created.Id}", ct);

        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
    }

    [Fact]
    public async Task Activity_EnumsAreSerializedAsNames()
    {
        var client = await CreateClientWithPrivilegeAsync("activities_edit");
        var ct = TestContext.Current.CancellationToken;

        var create = await client.PostAsJsonAsync("/api/activities", NewActivity<CreateActivityDto>("Enum-navne") with { KarmaType = KarmaType.Big }, ct);
        using var json = JsonDocument.Parse(await create.Content.ReadAsStringAsync(ct));

        Assert.Equal("Scenario", json.RootElement.GetProperty("type").GetString());
        Assert.Equal("Big", json.RootElement.GetProperty("karmaType").GetString());
        Assert.Equal("Danish", json.RootElement.GetProperty("playerLanguage").GetString());
        Assert.Equal("None", json.RootElement.GetProperty("autoSignupCategory").GetString());
    }

    [Fact]
    public async Task Activity_HasNoMaximumAge()
    {
        // Agreed deviation: Infosys had a maximum age (activityageranges 'max'); Fastasys only keeps the minimum.
        var client = await CreateClientWithPrivilegeAsync("activities_edit");
        var ct = TestContext.Current.CancellationToken;
        var body = JsonSerializer.SerializeToNode(NewActivity<CreateActivityDto>("Uden maksimumalder") with { MinAge = 12 },
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
        body["maxAge"] = 17;

        var create = await client.PostAsJsonAsync("/api/activities", body, ct);
        using var json = JsonDocument.Parse(await create.Content.ReadAsStringAsync(ct));

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal(12, json.RootElement.GetProperty("minAge").GetInt32());
        Assert.False(json.RootElement.TryGetProperty("maxAge", out _));
    }

    // Infosys shows the activity page's signup tables to everyone who may see the activity page
    [Fact]
    public async Task SignupTables_AreOpenToTheActivityViewRight()
    {
        var ct = TestContext.Current.CancellationToken;
        var viewer = await CreateClientWithPrivilegeAsync("activities_view");
        var other = await CreateClientWithPrivilegeAsync("participants_view");
        var editor = await CreateClientWithPrivilegeAsync("activities_edit");
        var created = await (await editor.PostAsJsonAsync("/api/activities", NewActivity<CreateActivityDto>("Tilmeldinger"), ct)).Content.ReadFromJsonAsync<ActivityDto>(ct);
        Assert.NotNull(created);

        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync($"/api/activities/{created.Id}/signups", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync($"/api/activities/{created.Id}/signups", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync($"/api/activities/{created.Id}/signups", ct)).StatusCode);
    }
}
