using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Fastasys.ApiService.Controllers;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Data.Seed;
using Fastasys.ApiService.Services;

namespace Fastasys.Tests;

// Infosys: the Read-only role may list, search and view participants (and sees only the personal info on the
// participant page); the roles with ParticipantController/* may also create, change and delete them.
// Fastasys maps that onto the participants_view / participants_edit privileges.
public class ParticipantsAuthorizationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ParticipantsAuthorizationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;

        // The in-memory database is shared with the other test classes. EnsureCreated only adds the seed data
        // when nothing has used the database yet, so the lookups the participants need are added here if missing.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InfosysDbContext>();
        db.Database.EnsureCreated();
        if (!db.ParticipantCategories.Any())
        {
            db.ParticipantCategories.AddRange(ParticipantLookupSeed.Categories.Select(c => new ParticipantCategory { Id = c.Id, Name = c.Name, IsOrganizer = c.IsOrganizer }));
            db.Countries.Add(new Country { Code = "DK", NameDanish = "Danmark", NameEnglish = "Denmark" });
            db.SaveChanges();
        }
    }

    private Task<HttpClient> CreateClientWithPrivilegeAsync(string privilegeKey) => CreateClientWithPrivilegesAsync(privilegeKey);

    private async Task<HttpClient> CreateClientWithPrivilegesAsync(params string[] privilegeKeys)
    {
        var suffix = Guid.NewGuid().ToString("N");
        const string password = "test-password";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InfosysDbContext>();
            var role = new Role { Name = $"role-{suffix}" };
            foreach (var privilegeKey in privilegeKeys)
            {
                role.RolePrivileges.Add(new RolePrivilege { Privilege = new Privilege { Name = privilegeKey, Key = privilegeKey } });
            }
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

    private static CreateParticipantDto NewParticipant() => new()
    {
        CategoryId = ParticipantCategory.ParticipantId,
        FirstName = "Adgang",
        LastName = "Testesen",
    };

    [Fact]
    public async Task Anonymous_CannotListParticipants()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/participants", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserWithoutParticipantPrivilege_IsForbidden()
    {
        var client = await CreateClientWithPrivilegeAsync("activities_edit");
        var ct = TestContext.Current.CancellationToken;

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/participants", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/participants/1", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/participants/options", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/participants/search", new SearchParticipantsDto(null, null), ct)).StatusCode);
    }

    [Fact]
    public async Task ViewPrivilege_CanSearch_ButNotSeeTheNotes()
    {
        var viewer = await CreateClientWithPrivilegeAsync("participants_view");
        var ct = TestContext.Current.CancellationToken;

        var search = await viewer.PostAsJsonAsync("/api/participants/search", new SearchParticipantsDto([new ParticipantSearchStepDto("a", null)], ["email"]), ct);
        var notes = await viewer.PostAsJsonAsync("/api/participants/search", new SearchParticipantsDto(null, ["adminNote"]), ct);

        Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, notes.StatusCode);
    }

    [Fact]
    public async Task ViewPrivilege_CanReadThePersonalInfo_ButNotWrite()
    {
        var editor = await CreateClientWithPrivilegeAsync("participants_edit");
        var viewer = await CreateClientWithPrivilegeAsync("participants_view");
        var ct = TestContext.Current.CancellationToken;
        var created = await (await editor.PostAsJsonAsync("/api/participants", NewParticipant(), ct)).Content.ReadFromJsonAsync<ParticipantDto>(ct);
        Assert.NotNull(created);
        var url = $"/api/participants/{created.Id}";

        var list = await viewer.GetAsync("/api/participants", ct);
        var options = await viewer.GetAsync("/api/participants/options", ct);
        var get = await viewer.GetAsync(url, ct);
        var create = await viewer.PostAsJsonAsync("/api/participants", NewParticipant(), ct);
        var update = await viewer.PutAsJsonAsync(url, new UpdateParticipantDto { CategoryId = ParticipantCategory.ParticipantId }, ct);
        var field = await viewer.PutAsJsonAsync($"{url}/fields/adminNote", new { value = "x" }, ct);
        var delete = await viewer.DeleteAsync(url, ct);

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.OK, options.StatusCode);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        using (var json = JsonDocument.Parse(await get.Content.ReadAsStringAsync(ct)))
        {
            Assert.False(json.RootElement.GetProperty("canEdit").GetBoolean());
            Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("details").ValueKind);
            Assert.Equal("Adgang", json.RootElement.GetProperty("firstName").GetString());
        }
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, field.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task EditPrivilege_CanCreateChangeAnnulAndDelete()
    {
        var client = await CreateClientWithPrivilegeAsync("participants_edit");
        var ct = TestContext.Current.CancellationToken;

        var createResponse = await client.PostAsJsonAsync("/api/participants", NewParticipant(), ct);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ParticipantDto>(ct);
        Assert.NotNull(created);
        var url = $"/api/participants/{created.Id}";

        var search = await client.PostAsJsonAsync("/api/participants/search", new SearchParticipantsDto(null, ["adminNote", "createdAt", "smsSignup"]), ct);
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        using (var json = JsonDocument.Parse(await search.Content.ReadAsStringAsync(ct)))
        {
            var row = json.RootElement.GetProperty("rows").EnumerateArray().First(r => r.GetProperty("id").GetInt32() == created.Id);
            // Times go out as UTC with a "Z", yes/no as true/false
            Assert.EndsWith("Z", row.GetProperty("values").GetProperty("createdAt").GetString());
            Assert.Equal(JsonValueKind.False, row.GetProperty("values").GetProperty("smsSignup").ValueKind);
        }

        var get = await client.GetAsync(url, ct);
        using (var json = JsonDocument.Parse(await get.Content.ReadAsStringAsync(ct)))
        {
            Assert.True(json.RootElement.GetProperty("canEdit").GetBoolean());
            Assert.Equal(JsonValueKind.Object, json.RootElement.GetProperty("details").ValueKind);
            // Enums and lists of enums go out by name
            Assert.Equal("Danish", json.RootElement.GetProperty("preferredLanguage").GetString());
            Assert.Equal("Danish", json.RootElement.GetProperty("details").GetProperty("activityLanguages")[0].GetString());
        }

        var update = await client.PutAsJsonAsync(url, new UpdateParticipantDto { CategoryId = ParticipantCategory.ParticipantId, FirstName = "Ændret" }, ct);
        var field = await client.PutAsJsonAsync($"{url}/fields/adminNote", new { value = "Intern note" }, ct);
        var deleteRefused = await client.DeleteAsync(url, ct);
        var annul = await client.PutAsJsonAsync($"{url}/fields/isAnnulled", new { value = true }, ct);
        var delete = await client.DeleteAsync(url, ct);
        var deleted = await client.GetAsync(url, ct);

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(HttpStatusCode.OK, field.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, deleteRefused.StatusCode);
        Assert.Equal(HttpStatusCode.OK, annul.StatusCode);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deleted.StatusCode);
    }

    // Infosys deltager/updateaktivitet: the ParticipantController roles, and in the method Activity-admin, Infonaut or
    // admin. Fastasys asks for both participants_edit and activities_edit.
    [Theory]
    [InlineData("participants_view,activities_edit")]
    [InlineData("participants_edit")]
    [InlineData("activities_edit")]
    public async Task SeatEditor_NeedsTheRightToEditBothParticipantsAndActivities(string privileges)
    {
        var ct = TestContext.Current.CancellationToken;
        var editor = await CreateClientWithPrivilegeAsync("participants_edit");
        var created = await (await editor.PostAsJsonAsync("/api/participants", NewParticipant(), ct)).Content.ReadFromJsonAsync<ParticipantDto>(ct);
        Assert.NotNull(created);
        var client = await CreateClientWithPrivilegesAsync(privileges.Split(','));

        var get = await client.GetAsync($"/api/participants/{created.Id}/seats", ct);
        var update = await client.PutAsJsonAsync($"/api/participants/{created.Id}/seats", new UpdateParticipantSeatsDto([]), ct);

        Assert.Equal(HttpStatusCode.Forbidden, get.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
    }

    [Fact]
    public async Task SeatEditor_IsOpenToUsersWhoMayEditParticipantsAndActivities()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateClientWithPrivilegesAsync("participants_edit", "activities_edit");
        var created = await (await client.PostAsJsonAsync("/api/participants", NewParticipant(), ct)).Content.ReadFromJsonAsync<ParticipantDto>(ct);
        Assert.NotNull(created);

        var get = await client.GetAsync($"/api/participants/{created.Id}/seats", ct);
        var update = await client.PutAsJsonAsync($"/api/participants/{created.Id}/seats", new UpdateParticipantSeatsDto([]), ct);

        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);
    }
}
