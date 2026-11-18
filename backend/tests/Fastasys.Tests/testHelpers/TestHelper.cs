using System.Net.Http.Headers;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Fastasys.ApiService.Services;

namespace Fastasys.Tests.testHelpers;

public class TestHelper
{

    private readonly TestWebApplicationFactory _factory;
    public TestHelper(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task<HttpClient> CreateClientWithPrivilegeAsync(string privilegeKey)
    {
        return await CreateClientWithPrivilegeAsync([privilegeKey]);
    }

    public async Task<HttpClient> CreateClientWithPrivilegeAsync(string[] privilegeKeys)
    {
        var suffix = Guid.NewGuid().ToString("N");
        const string password = "test-password";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InfosysDbContext>();
            var role = new Role { Name = $"role-{suffix}" };
            foreach (var privilegeKey in privilegeKeys)
            {
                role
                .RolePrivileges
                .Add(new RolePrivilege
                {
                    Privilege = new Privilege
                    {
                        Name = privilegeKey,
                        Key = privilegeKey
                    }
                });
                db.SaveChanges();
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

}
