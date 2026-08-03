using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Ditak.Attendance.Core.Data;
using Ditak.Attendance.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Ditak.Attendance.Api.Auth;

public class JwtOptions
{
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "DitakWorkTime";
    public string Audience { get; set; } = "DitakWorkTimeWeb";
    public int ExpiresMinutes { get; set; } = 480;
}

public record AuthUserDto(Guid Id, string Email, string DisplayName, string PreferredLanguage, string ThemePreference, Guid? EmployeeId, IReadOnlyList<string> Roles);

public class AuthService
{
    private readonly AttendanceDbContext _db;
    private readonly JwtOptions _jwt;

    public AuthService(AttendanceDbContext db, JwtOptions jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    public async Task<(string Token, AuthUserDto User)?> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var user = await _db.Users
            .Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .Include(x => x.Employee)
            .FirstOrDefaultAsync(x => x.Email == normalized && x.IsActive, ct);

        if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            return null;
        }

        var roles = user.UserRoles.Select(x => x.Role.Name).ToList();
        var dto = ToDto(user, roles);
        var token = CreateToken(user, roles);
        return (token, dto);
    }

    public async Task<AuthUserDto?> GetMeAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users
            .Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .Include(x => x.Employee)
            .FirstOrDefaultAsync(x => x.Id == userId && x.IsActive, ct);
        if (user is null) return null;
        return ToDto(user, user.UserRoles.Select(x => x.Role.Name).ToList());
    }

    public async Task<AuthUserDto?> UpdatePreferencesAsync(Guid userId, string? language, string? theme, CancellationToken ct = default)
    {
        var user = await _db.Users
            .Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .Include(x => x.Employee)
            .FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null) return null;

        if (!string.IsNullOrWhiteSpace(language) && new[] { "en", "hy", "ru" }.Contains(language))
        {
            user.PreferredLanguage = language;
        }

        if (!string.IsNullOrWhiteSpace(theme) && new[] { "system", "light", "dark" }.Contains(theme))
        {
            user.ThemePreference = theme;
        }

        await _db.SaveChangesAsync(ct);
        return ToDto(user, user.UserRoles.Select(x => x.Role.Name).ToList());
    }

    private string CreateToken(User user, IReadOnlyList<string> roles)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("name", user.DisplayName)
        };
        if (user.Employee is not null)
        {
            claims.Add(new Claim("employee_id", user.Employee.Id.ToString()));
        }

        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwt.ExpiresMinutes),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static AuthUserDto ToDto(User user, IReadOnlyList<string> roles)
        => new(user.Id, user.Email, user.DisplayName, user.PreferredLanguage, user.ThemePreference, user.Employee?.Id, roles);
}
