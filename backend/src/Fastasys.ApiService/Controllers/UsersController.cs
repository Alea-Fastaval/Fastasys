using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fastasys.ApiService.Controllers;

public record PrivilegeDto(int Id, string Name, string Key);

public record RoleDto(int Id, string Name, string Description, List<PrivilegeDto> Privileges);

public record UserDto(
    int Id,
    string Username,
    string Email,
    string FirstName,
    string LastName,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    List<RoleDto> Roles
);

public record CreateUserRequestDto(
    string Username,
    string Password,
    string Email,
    string FirstName,
    string LastName,
    bool IsActive,
    List<int> RoleIds
);

public record UpdateUserRequestDto(
    string Email,
    string FirstName,
    string LastName,
    bool IsActive,
    string? Password,
    List<int> RoleIds
);

public record CreateRoleRequestDto(string Name, string Description, List<int> PrivilegeIds);

public record UpdateRoleRequestDto(string Name, string Description, List<int> PrivilegeIds);

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController(InfosysDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetAll([FromQuery] string? search, [FromQuery] int? roleId)
    {
        var query = db
            .Users.Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePrivileges)
                        .ThenInclude(rp => rp.Privilege)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(u =>
                u.Username.ToLower().Contains(searchLower)
                || u.Email.ToLower().Contains(searchLower)
                || u.FirstName.ToLower().Contains(searchLower)
                || u.LastName.ToLower().Contains(searchLower)
            );
        }

        if (roleId.HasValue)
        {
            query = query.Where(u => u.UserRoles.Any(ur => ur.RoleId == roleId.Value));
        }

        var users = await query.OrderByDescending(u => u.Id).Select(u => MapToUserDto(u)).ToListAsync();

        return Ok(users);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserDto>> GetById(int id)
    {
        var user = await db
            .Users.Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePrivileges)
                        .ThenInclude(rp => rp.Privilege)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
            return NotFound(new { message = "User not found" });

        return Ok(MapToUserDto(user));
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
        {
            return BadRequest(new { message = "Username and Password are required" });
        }

        var existingUser = await db.Users.AnyAsync(u => u.Username.ToLower() == dto.Username.ToLower());
        if (existingUser)
        {
            return BadRequest(new { message = "Username is already taken" });
        }

        var user = new User
        {
            Username = dto.Username.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Email = dto.Email?.Trim() ?? string.Empty,
            FirstName = dto.FirstName?.Trim() ?? string.Empty,
            LastName = dto.LastName?.Trim() ?? string.Empty,
            IsActive = dto.IsActive,
            CreatedAt = DateTime.UtcNow,
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        if (dto.RoleIds != null && dto.RoleIds.Any())
        {
            foreach (var rId in dto.RoleIds.Distinct())
            {
                var roleExists = await db.Roles.AnyAsync(r => r.Id == rId);
                if (roleExists)
                {
                    db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = rId });
                }
            }
            await db.SaveChangesAsync();
        }

        var createdUser = await db
            .Users.Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePrivileges)
                        .ThenInclude(rp => rp.Privilege)
            .FirstAsync(u => u.Id == user.Id);

        return CreatedAtAction(nameof(GetById), new { id = user.Id }, MapToUserDto(createdUser));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<UserDto>> Update(int id, [FromBody] UpdateUserRequestDto dto)
    {
        var user = await db.Users.Include(u => u.UserRoles).FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
            return NotFound(new { message = "User not found" });

        user.Email = dto.Email?.Trim() ?? string.Empty;
        user.FirstName = dto.FirstName?.Trim() ?? string.Empty;
        user.LastName = dto.LastName?.Trim() ?? string.Empty;
        user.IsActive = dto.IsActive;

        if (!string.IsNullOrWhiteSpace(dto.Password))
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
        }

        // Update Roles
        db.UserRoles.RemoveRange(user.UserRoles);
        if (dto.RoleIds != null && dto.RoleIds.Any())
        {
            foreach (var rId in dto.RoleIds.Distinct())
            {
                var roleExists = await db.Roles.AnyAsync(r => r.Id == rId);
                if (roleExists)
                {
                    db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = rId });
                }
            }
        }

        await db.SaveChangesAsync();

        var updatedUser = await db
            .Users.Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePrivileges)
                        .ThenInclude(rp => rp.Privilege)
            .FirstAsync(u => u.Id == id);

        return Ok(MapToUserDto(updatedUser));
    }

    [HttpPost("{id:int}/toggle-active")]
    public async Task<ActionResult<UserDto>> ToggleActive(int id)
    {
        var user = await db
            .Users.Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePrivileges)
                        .ThenInclude(rp => rp.Privilege)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
            return NotFound(new { message = "User not found" });

        user.IsActive = !user.IsActive;
        await db.SaveChangesAsync();

        return Ok(MapToUserDto(user));
    }

    [HttpGet("privileges")]
    public async Task<ActionResult<IEnumerable<PrivilegeDto>>> GetPrivileges()
    {
        var privileges = await db
            .Privileges.AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new PrivilegeDto(p.Id, p.Name, p.Key))
            .ToListAsync();

        return Ok(privileges);
    }

    [HttpGet("roles")]
    public async Task<ActionResult<IEnumerable<RoleDto>>> GetRoles()
    {
        var roles = await db
            .Roles.Include(r => r.RolePrivileges)
                .ThenInclude(rp => rp.Privilege)
            .AsNoTracking()
            .ToListAsync();

        var dtos = roles
            .Select(r => new RoleDto(
                r.Id,
                r.Name,
                r.Description,
                r.RolePrivileges.Select(rp => new PrivilegeDto(rp.Privilege.Id, rp.Privilege.Name, rp.Privilege.Key))
                    .ToList()
            ))
            .ToList();

        return Ok(dtos);
    }

    [HttpPost("roles")]
    public async Task<ActionResult<RoleDto>> CreateRole([FromBody] CreateRoleRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return BadRequest(new { message = "Role name is required" });
        }

        var existing = await db.Roles.AnyAsync(r => r.Name.ToLower() == dto.Name.ToLower());
        if (existing)
        {
            return BadRequest(new { message = "A role with this name already exists" });
        }

        var role = new Role { Name = dto.Name.Trim(), Description = dto.Description?.Trim() ?? string.Empty };

        db.Roles.Add(role);
        await db.SaveChangesAsync();

        if (dto.PrivilegeIds != null && dto.PrivilegeIds.Any())
        {
            foreach (var pId in dto.PrivilegeIds.Distinct())
            {
                if (await db.Privileges.AnyAsync(p => p.Id == pId))
                {
                    db.RolePrivileges.Add(new RolePrivilege { RoleId = role.Id, PrivilegeId = pId });
                }
            }
            await db.SaveChangesAsync();
        }

        var createdRole = await db
            .Roles.Include(r => r.RolePrivileges)
                .ThenInclude(rp => rp.Privilege)
            .FirstAsync(r => r.Id == role.Id);

        return Ok(
            new RoleDto(
                createdRole.Id,
                createdRole.Name,
                createdRole.Description,
                createdRole
                    .RolePrivileges.Select(rp => new PrivilegeDto(rp.Privilege.Id, rp.Privilege.Name, rp.Privilege.Key))
                    .ToList()
            )
        );
    }

    [HttpPut("roles/{id:int}")]
    public async Task<ActionResult<RoleDto>> UpdateRole(int id, [FromBody] UpdateRoleRequestDto dto)
    {
        var role = await db.Roles.Include(r => r.RolePrivileges).FirstOrDefaultAsync(r => r.Id == id);

        if (role == null)
            return NotFound(new { message = "Role not found" });

        role.Name = dto.Name.Trim();
        role.Description = dto.Description?.Trim() ?? string.Empty;

        db.RolePrivileges.RemoveRange(role.RolePrivileges);
        if (dto.PrivilegeIds != null && dto.PrivilegeIds.Any())
        {
            foreach (var pId in dto.PrivilegeIds.Distinct())
            {
                if (await db.Privileges.AnyAsync(p => p.Id == pId))
                {
                    db.RolePrivileges.Add(new RolePrivilege { RoleId = role.Id, PrivilegeId = pId });
                }
            }
        }

        await db.SaveChangesAsync();

        var updatedRole = await db
            .Roles.Include(r => r.RolePrivileges)
                .ThenInclude(rp => rp.Privilege)
            .FirstAsync(r => r.Id == id);

        return Ok(
            new RoleDto(
                updatedRole.Id,
                updatedRole.Name,
                updatedRole.Description,
                updatedRole
                    .RolePrivileges.Select(rp => new PrivilegeDto(rp.Privilege.Id, rp.Privilege.Name, rp.Privilege.Key))
                    .ToList()
            )
        );
    }

    [HttpDelete("roles/{id:int}")]
    public async Task<IActionResult> DeleteRole(int id)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == id);
        if (role == null)
            return NotFound(new { message = "Role not found" });

        if (role.Name.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Cannot delete the system Admin role" });
        }

        db.Roles.Remove(role);
        await db.SaveChangesAsync();

        return Ok(new { message = "Role deleted successfully" });
    }

    private static UserDto MapToUserDto(User u)
    {
        var roles = u
            .UserRoles.Select(ur => new RoleDto(
                ur.Role.Id,
                ur.Role.Name,
                ur.Role.Description,
                ur.Role.RolePrivileges.Select(rp => new PrivilegeDto(
                        rp.Privilege.Id,
                        rp.Privilege.Name,
                        rp.Privilege.Key
                    ))
                    .ToList()
            ))
            .ToList();

        return new UserDto(
            u.Id,
            u.Username,
            u.Email,
            u.FirstName,
            u.LastName,
            u.IsActive,
            u.CreatedAt,
            u.LastLoginAt,
            roles
        );
    }
}
