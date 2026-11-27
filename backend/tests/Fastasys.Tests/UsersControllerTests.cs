using Fastasys.ApiService.Controllers;
using Fastasys.ApiService.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fastasys.Tests;

public class UsersControllerTests
{
    private InfosysDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<InfosysDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var context = new InfosysDbContext(options);
        context.Database.EnsureCreated();
        DbSeeder.Seed(context);
        return context;
    }

    [Fact]
    public async Task GetAll_ReturnsUsers_IncludingSeededAdmin()
    {
        using var db = CreateDbContext();
        var controller = new UsersController(db);

        var actionResult = await controller.GetAll(null, null);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var users = Assert.IsAssignableFrom<IEnumerable<UserDto>>(okResult.Value);

        Assert.NotEmpty(users);
        Assert.Contains(users, u => u.Username == "admin");
    }

    [Fact]
    public async Task Create_AddsNewUser_HashedPasswordAndRoles()
    {
        using var db = CreateDbContext();
        var controller = new UsersController(db);


        var adminRole = await db.Roles.FirstAsync(r => r.Name == "Admin", TestContext.Current.CancellationToken);

        var createDto = new CreateUserRequestDto(
            Username: "newuser",
            Password: "Password123!",
            Email: "newuser@fastaval.dk",
            FirstName: "New",
            LastName: "User",
            IsActive: true,
            RoleIds: [adminRole.Id]
        );

        var actionResult = await controller.Create(createDto);
        var createdResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
        var user = Assert.IsType<UserDto>(createdResult.Value);

        Assert.Equal("newuser", user.Username);
        Assert.Single(user.Roles);
        Assert.Equal("Admin", user.Roles[0].Name);

        // Verify password hash in DB
        var dbUser = await db.Users.FindAsync([user.Id], TestContext.Current.CancellationToken);
        Assert.NotNull(dbUser);
        Assert.True(BCrypt.Net.BCrypt.Verify("Password123!", dbUser.PasswordHash));
    }

    [Fact]
    public async Task Update_UpdatesUserFieldsAndActiveStatus()
    {
        using var db = CreateDbContext();
        var controller = new UsersController(db);

        var adminUser = await db.Users.FirstAsync(u => u.Username == "admin", TestContext.Current.CancellationToken);

        var updateDto = new UpdateUserRequestDto(
            Email: "updatedadmin@fastaval.dk",
            FirstName: "UpdatedAdmin",
            LastName: "SuperUser",
            IsActive: false,
            Password: null,
            RoleIds: new List<int>()
        );

        var actionResult = await controller.Update(adminUser.Id, updateDto);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var user = Assert.IsType<UserDto>(okResult.Value);

        Assert.Equal("updatedadmin@fastaval.dk", user.Email);
        Assert.Equal("UpdatedAdmin", user.FirstName);
        Assert.False(user.IsActive);
    }

    [Fact]
    public async Task ToggleActive_TogglesStatus()
    {
        using var db = CreateDbContext();
        var controller = new UsersController(db);

        var adminUser = await db.Users.FirstAsync(u => u.Username == "admin", TestContext.Current.CancellationToken);
        Assert.True(adminUser.IsActive);

        var actionResult = await controller.ToggleActive(adminUser.Id);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var user = Assert.IsType<UserDto>(okResult.Value);

        Assert.False(user.IsActive);
    }

    [Fact]
    public async Task GetRoles_ReturnsSeededRoles()
    {
        using var db = CreateDbContext();
        var controller = new UsersController(db);

        var actionResult = await controller.GetRoles();
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var roles = Assert.IsAssignableFrom<IEnumerable<RoleDto>>(okResult.Value);

        Assert.NotEmpty(roles);
        Assert.Contains(roles, r => r.Name == "Admin");
    }

    [Fact]
    public async Task GetPrivileges_ReturnsSeededPrivileges()
    {
        using var db = CreateDbContext();
        var controller = new UsersController(db);

        var actionResult = await controller.GetPrivileges();
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var privileges = Assert.IsType<IEnumerable<PrivilegeDto>>(okResult.Value, exactMatch: false);

        Console.WriteLine(privileges);

        Assert.NotEmpty(privileges);
        Assert.Contains(privileges, p => p.Key == "users_view");
        Assert.Contains(privileges, p => p.Key == "boardgame_view");
    }

    [Fact]
    public async Task CreateRole_AddsNewRole_WithAssignedPrivileges()
    {
        using var db = CreateDbContext();
        var controller = new UsersController(db);

        var usersViewPriv = await db.Privileges.FirstAsync(p => p.Key == "users_view", TestContext.Current.CancellationToken);

        var createDto = new CreateRoleRequestDto(
            Name: "GM Manager",
            Description: "Manages GM schedules and activities",
            PrivilegeIds: new List<int> { usersViewPriv.Id }
        );

        var actionResult = await controller.CreateRole(createDto);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var role = Assert.IsType<RoleDto>(okResult.Value);

        Assert.Equal("GM Manager", role.Name);
        Assert.Single(role.Privileges);
        Assert.Equal("users_view", role.Privileges[0].Key);
    }

    [Fact]
    public async Task DeleteRole_PreventsAdminDeletion()
    {
        using var db = CreateDbContext();
        var controller = new UsersController(db);

        var adminRole = await db.Roles.FirstAsync(r => r.Name == "Admin", TestContext.Current.CancellationToken);

        var actionResult = await controller.DeleteRole(adminRole.Id);
        Assert.IsType<BadRequestObjectResult>(actionResult);
    }
}
