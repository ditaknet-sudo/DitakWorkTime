using Ditak.Attendance.Core.Data;
using Ditak.Attendance.Core.Entities;
using Ditak.Attendance.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ditak.Attendance.Core;

public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddAttendanceCore(this IServiceCollection services, string connectionString, string companyTimezone)
    {
        services.AddDbContext<AttendanceDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped(sp => new AttendanceService(
            sp.GetRequiredService<AttendanceDbContext>(),
            companyTimezone));
        services.AddScoped<PresenceService>();
        services.AddScoped<ReportService>();
        return services;
    }
}

public static class DatabaseBootstrapper
{
    public static async Task MigrateAndSeedAsync(
        AttendanceDbContext db,
        string companyName,
        string companyTimezone,
        string adminEmail,
        string adminPassword,
        string adminName,
        CancellationToken ct = default)
    {
        // v1: create schema from model on empty volume. Additive EF migrations replace this
        // in later versions; informational data volume is never wiped by updates.
        await db.Database.EnsureCreatedAsync(ct);

        // Seed only when informational DB is empty (first install).
        if (await db.Companies.AnyAsync(ct))
        {
            return;
        }

        var company = new Company
        {
            Id = Guid.NewGuid(),
            Name = companyName,
            Timezone = companyTimezone
        };
        db.Companies.Add(company);

        var site = new Site
        {
            Id = Guid.NewGuid(),
            Name = "Main Office",
            Timezone = companyTimezone
        };
        db.Sites.Add(site);

        var roles = new[]
        {
            new Role { Id = Guid.NewGuid(), Name = "Admin" },
            new Role { Id = Guid.NewGuid(), Name = "Manager" },
            new Role { Id = Guid.NewGuid(), Name = "Employee" }
        };
        db.Roles.AddRange(roles);

        var admin = new User
        {
            Id = Guid.NewGuid(),
            Email = adminEmail.Trim().ToLowerInvariant(),
            DisplayName = adminName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
            PreferredLanguage = "en",
            ThemePreference = "system"
        };
        db.Users.Add(admin);
        db.UserRoles.Add(new UserRole { UserId = admin.Id, RoleId = roles[0].Id });

        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            UserId = admin.Id,
            SiteId = site.Id,
            EmployeeCode = "EMP-001",
            FullName = adminName,
            Department = "Administration"
        };
        db.Employees.Add(employee);

        await db.SaveChangesAsync(ct);
    }
}
