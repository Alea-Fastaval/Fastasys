using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Fastasys.ApiService.Data;
using Fastasys.ApiService.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Fastasys.ApiService.Services;

public class AuthService(InfosysDbContext db, IConfiguration configuration) : IAuthService
{
    public async Task<AuthResponseDto?> LoginAsync(LoginDto dto)
    {
        var user = await db
            .Users.Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePrivileges)
                        .ThenInclude(rp => rp.Privilege)
            .FirstOrDefaultAsync(u => u.Username == dto.Username && u.IsActive);

        if (user == null)
        {
            return null;
        }

        var (isValid, needsUpgrade) = VerifyPassword(dto.Password, user.PasswordHash);
        if (!isValid)
        {
            return null;
        }

        if (needsUpgrade)
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
        }

        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return GenerateAuthResponse(user);
    }

    public async Task<AuthResponseDto?> RefreshTokenAsync(RefreshTokenDto dto)
    {
        // Simple token validation / refresh logic for initial implementation
        var principal = GetPrincipalFromExpiredToken(dto.Token);
        if (principal == null)
            return null;

        var username = principal.Identity?.Name;
        var user = await db
            .Users.Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePrivileges)
                        .ThenInclude(rp => rp.Privilege)
            .FirstOrDefaultAsync(u => u.Username == username && u.IsActive);

        if (user == null)
            return null;

        return GenerateAuthResponse(user);
    }

    public async Task<bool> ResetPasswordRequestAsync(ResetPasswordDto dto)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
        if (user == null)
            return false;

        // Reset password request token logic - returns true if email matches
        return true;
    }

    private AuthResponseDto GenerateAuthResponse(User user)
    {
        var jwtSecret = configuration["Jwt:Secret"] ?? "FastasysSuperSecretSecurityKey2026!FormatLengthForHS256";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var roles = user.UserRoles.Select(ur => ur.Role.Name).Distinct().ToList();
        var privileges = user
            .UserRoles.SelectMany(ur => ur.Role.RolePrivileges)
            .Select(rp => rp.Privilege.Key)
            .Distinct()
            .ToList();

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("FirstName", user.FirstName),
            new Claim("LastName", user.LastName),
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        foreach (var privilege in privileges)
        {
            claims.Add(new Claim("privilege", privilege));
        }

        var expiresAt = DateTime.UtcNow.AddHours(8);
        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"] ?? "Fastasys",
            audience: configuration["Jwt:Audience"] ?? "FastasysClients",
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        var refreshToken = Guid.NewGuid().ToString("N");

        var userInfo = new UserInfoDto(
            user.Id,
            user.Username,
            user.Email,
            user.FirstName,
            user.LastName,
            roles,
            privileges
        );

        return new AuthResponseDto(tokenString, refreshToken, expiresAt, userInfo);
    }

    private static (bool IsValid, bool NeedsUpgrade) VerifyPassword(string inputPassword, string passwordHash)
    {
        if (string.IsNullOrEmpty(passwordHash))
            return (false, false);

        // 1. Check BCrypt hash (starts with $2a$, $2b$, or $2y$)
        if (passwordHash.StartsWith("$2") && passwordHash.Length >= 60)
        {
            try
            {
                bool valid = BCrypt.Net.BCrypt.Verify(inputPassword, passwordHash);
                return (valid, false);
            }
            catch
            {
                // Fallthrough if parsing fails
            }
        }

        // 2. Check legacy MD5 hash (32 hex characters) from Infosys PHP
        using var md5 = System.Security.Cryptography.MD5.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(inputPassword);
        var hashBytes = md5.ComputeHash(bytes);
        var md5String = Convert.ToHexString(hashBytes).ToLowerInvariant();

        if (passwordHash.Equals(md5String, StringComparison.OrdinalIgnoreCase))
        {
            return (true, true); // Match! Signal to upgrade to BCrypt.
        }

        // 3. Dev plaintext fallback (e.g., initial seed "admin123")
        if (passwordHash == inputPassword)
        {
            return (true, true); // Match! Signal to upgrade to BCrypt.
        }

        return (false, false);
    }

    private ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        var jwtSecret = configuration["Jwt:Secret"] ?? "FastasysSuperSecretSecurityKey2026!FormatLengthForHS256";
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false,
            ValidateIssuer = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateLifetime = false,
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            var principal = tokenHandler.ValidateToken(
                token,
                tokenValidationParameters,
                out SecurityToken securityToken
            );
            if (
                securityToken is not JwtSecurityToken jwtSecurityToken
                || !jwtSecurityToken.Header.Alg.Equals(
                    SecurityAlgorithms.HmacSha256,
                    StringComparison.InvariantCultureIgnoreCase
                )
            )
            {
                return null;
            }
            return principal;
        }
        catch
        {
            return null;
        }
    }
}
