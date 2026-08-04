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
    private const string InitialMigrationId = "20260727120000_InitialCreate";

    private static readonly string[] InitialTables =
    [
        "companies",
        "sites",
        "roles",
        "users",
        "user_roles",
        "employees",
        "attendance_events",
        "attendance_day_summaries",
        "presence_hints"
    ];

    public static async Task MigrateAndSeedAsync(
        AttendanceDbContext db,
        string companyName,
        string companyTimezone,
        string adminEmail,
        string adminPassword,
        string adminName,
        CancellationToken ct = default)
    {
        // Earlier v1 builds used EnsureCreated, which did not create EF's migration
        // history table. Baseline that exact schema once, then use forward-only
        // migrations for both new and upgraded installations.
        await BaselineLegacySchemaAsync(db, ct);
        await db.Database.MigrateAsync(ct);

        var standardRoleNames = new[] { "Admin", "Director", "Accountant", "Manager", "Employee" };
        var roles = await db.Roles.ToListAsync(ct);
        foreach (var roleName in standardRoleNames)
        {
            if (roles.Any(x => x.Name.Equals(roleName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var role = new Role { Id = Guid.NewGuid(), Name = roleName };
            roles.Add(role);
            db.Roles.Add(role);
        }

        await db.SaveChangesAsync(ct);

        // Seed only when informational DB is empty (first install).
        if (await db.Companies.AnyAsync(ct))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(adminEmail) || !adminEmail.Contains('@'))
        {
            throw new InvalidOperationException("Seed:AdminEmail must be a valid email address.");
        }

        if (adminPassword.Length < 12 || adminPassword.Equals("ChangeMe123!", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Seed:AdminPassword must be at least 12 characters and must not use the example password.");
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
        var adminRole = roles.Single(x => x.Name.Equals("Admin", StringComparison.OrdinalIgnoreCase));
        db.UserRoles.Add(new UserRole { UserId = admin.Id, RoleId = adminRole.Id });

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

    private static async Task BaselineLegacySchemaAsync(AttendanceDbContext db, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(ct);
        }

        try
        {
            var existingTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'";
                await using var reader = await command.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    existingTables.Add(reader.GetString(0));
                }
            }

            var existingInitialTables = InitialTables.Count(existingTables.Contains);
            if (existingInitialTables == 0)
            {
                return;
            }

            if (existingInitialTables != InitialTables.Length)
            {
                var missing = InitialTables.Where(x => !existingTables.Contains(x));
                throw new InvalidOperationException(
                    $"Database schema is incomplete; missing tables: {string.Join(", ", missing)}. Restore a valid backup before starting.");
            }

            await using var baseline = connection.CreateCommand();
            baseline.CommandText = $"""
                CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                    "MigrationId" character varying(150) NOT NULL,
                    "ProductVersion" character varying(32) NOT NULL,
                    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
                );
                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                VALUES ('{InitialMigrationId}', '8.0.11')
                ON CONFLICT ("MigrationId") DO NOTHING;
                """;
            await baseline.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}
