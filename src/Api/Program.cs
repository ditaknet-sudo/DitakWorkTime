using System.IdentityModel.Tokens.Jwt;
using System.Net.Mail;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Ditak.Attendance.Api.Auth;
using Ditak.Attendance.Core;
using Ditak.Attendance.Core.Data;
using Ditak.Attendance.Core.Entities;
using Ditak.Attendance.Core.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is required.");
var companyTimezone = builder.Configuration["Company:Timezone"] ?? "UTC";
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret is required.");
if (jwtSecret.Length < 32 || jwtSecret.StartsWith("change_me", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException("Security Error: Jwt:Secret must be a non-example value of at least 32 characters.");
}
var seedAdminPassword = builder.Configuration["Seed:AdminPassword"] ?? string.Empty;

var jwtOptions = new JwtOptions
{
    Secret = jwtSecret,
    Issuer = builder.Configuration["Jwt:Issuer"] ?? "DitakWorkTime",
    Audience = builder.Configuration["Jwt:Audience"] ?? "DitakWorkTimeWeb",
    ExpiresMinutes = int.TryParse(builder.Configuration["Jwt:ExpiresMinutes"], out var m) ? m : 480
};

builder.Services.AddSingleton(jwtOptions);
builder.Services.AddAttendanceCore(connectionString, companyTimezone);
builder.Services.AddScoped<AuthService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    // The API is reachable only on the private Compose network; the reverse
    // proxy is the sole ingress and has a dynamic container address.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

var corsOrigins = (builder.Configuration["Cors:Origins"] ?? "http://localhost")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = JwtRegisteredClaimNames.Sub
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AttendanceDbContext>();
    await DatabaseBootstrapper.MigrateAndSeedAsync(
        db,
        builder.Configuration["Company:Name"] ?? "Company",
        companyTimezone,
        builder.Configuration["Seed:AdminEmail"] ?? "admin@company.local",
        seedAdminPassword,
        builder.Configuration["Seed:AdminName"] ?? "System Admin");
}

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", async (AttendanceDbContext db, CancellationToken ct) =>
{
    var databaseOk = false;
    try
    {
        databaseOk = await db.Database.CanConnectAsync(ct);
    }
    catch
    {
        // Health checks report dependency failure without leaking internals.
    }

    var payload = new
    {
        status = databaseOk ? "ok" : "degraded",
        database = databaseOk ? "ok" : "unavailable",
        version = builder.Configuration["Product:Version"] ?? "1.0.0",
        utc = DateTimeOffset.UtcNow
    };
    return databaseOk
        ? Results.Ok(payload)
        : Results.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable);
});

app.MapPost("/api/auth/login", async ([FromBody] LoginRequest req, AuthService auth, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrEmpty(req.Password))
    {
        return Results.Unauthorized();
    }
    var result = await auth.LoginAsync(req.Email, req.Password, ct);
    return result is null
        ? Results.Unauthorized()
        : Results.Ok(new { token = result.Value.Token, user = result.Value.User });
}).RequireRateLimiting("login");

app.MapGet("/api/me", [Authorize] async (ClaimsPrincipal principal, AuthService auth, CancellationToken ct) =>
{
    var userId = GetUserId(principal);
    var me = await auth.GetMeAsync(userId, ct);
    return me is null ? Results.Unauthorized() : Results.Ok(me);
});

app.MapPut("/api/me/preferences", [Authorize] async ([FromBody] PreferencesRequest req, ClaimsPrincipal principal, AuthService auth, CancellationToken ct) =>
{
    var me = await auth.UpdatePreferencesAsync(GetUserId(principal), req.Language, req.Theme, ct);
    return me is null ? Results.NotFound() : Results.Ok(me);
});

app.MapPost("/api/attendance/check-in", [Authorize] async (
    [FromBody] AttendanceRequest req,
    ClaimsPrincipal principal,
    AttendanceService attendance,
    HttpContext http,
    CancellationToken ct) =>
{
    var employeeId = await ResolveSelfEmployeeIdAsync(principal, http.RequestServices, ct);
    if (employeeId is null) return Results.BadRequest(new { error = "Employee profile required." });
    if (req.EmployeeId.HasValue && req.EmployeeId.Value != employeeId.Value) return Results.Forbid();
    if (req.OccurredAtUtc.HasValue) return Results.BadRequest(new { error = "Custom event time requires the manual attendance endpoint." });

    var source = ParseSelfServiceSource(req.Source);
    if (source is null) return Results.BadRequest(new { error = "Source must be Web or Qr." });

    try
    {
        var ev = await attendance.CheckInAsync(new AttendanceCommand(
            employeeId.Value,
            source.Value,
            req.SiteId,
            http.Connection.RemoteIpAddress?.ToString(),
            req.DeviceId,
            string.IsNullOrWhiteSpace(req.IdempotencyKey) ? Guid.NewGuid().ToString("N") : req.IdempotencyKey,
            req.Note,
            null), ct);
        return Results.Ok(ev);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/attendance/check-out", [Authorize] async (
    [FromBody] AttendanceRequest req,
    ClaimsPrincipal principal,
    AttendanceService attendance,
    HttpContext http,
    CancellationToken ct) =>
{
    var employeeId = await ResolveSelfEmployeeIdAsync(principal, http.RequestServices, ct);
    if (employeeId is null) return Results.BadRequest(new { error = "Employee profile required." });
    if (req.EmployeeId.HasValue && req.EmployeeId.Value != employeeId.Value) return Results.Forbid();
    if (req.OccurredAtUtc.HasValue) return Results.BadRequest(new { error = "Custom event time requires the manual attendance endpoint." });

    var source = ParseSelfServiceSource(req.Source);
    if (source is null) return Results.BadRequest(new { error = "Source must be Web or Qr." });

    try
    {
        var ev = await attendance.CheckOutAsync(new AttendanceCommand(
            employeeId.Value,
            source.Value,
            req.SiteId,
            http.Connection.RemoteIpAddress?.ToString(),
            req.DeviceId,
            string.IsNullOrWhiteSpace(req.IdempotencyKey) ? Guid.NewGuid().ToString("N") : req.IdempotencyKey,
            req.Note,
            null), ct);
        return Results.Ok(ev);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/attendance/me/today", [Authorize] async (ClaimsPrincipal principal, AttendanceService attendance, IServiceProvider sp, CancellationToken ct) =>
{
    var employeeId = await ResolveSelfEmployeeIdAsync(principal, sp, ct);
    if (employeeId is null) return Results.BadRequest(new { error = "Employee profile required." });
    return Results.Ok(await attendance.GetTodayStatusAsync(employeeId.Value, ct));
});

app.MapGet("/api/attendance/presence", [Authorize(Roles = "Admin,Manager,Director")] async (PresenceService presence, CancellationToken ct) =>
    Results.Ok(await presence.GetPresenceBoardAsync(ct)));

app.MapPost("/api/devices/heartbeat", [Authorize] async (
    [FromBody] HeartbeatRequest req,
    ClaimsPrincipal principal,
    PresenceService presence,
    HttpContext http,
    CancellationToken ct) =>
{
    var employeeId = await ResolveSelfEmployeeIdAsync(principal, http.RequestServices, ct);
    if (employeeId is null) return Results.BadRequest(new { error = "Employee profile required." });
    if (req.EmployeeId.HasValue && req.EmployeeId.Value != employeeId.Value) return Results.Forbid();
    var ip = http.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
    await presence.UpsertHintAsync(employeeId.Value, ip, req.SiteId, ct);
    return Results.Ok(new { status = "hint_recorded", billing = false });
});

app.MapGet("/api/reports/employees/{employeeId:guid}/daily", [Authorize] async (
    Guid employeeId, DateOnly? from, DateOnly? to, ClaimsPrincipal principal, ReportService reports, IServiceProvider sp, CancellationToken ct) =>
{
    if (!await CanReadEmployeeAsync(principal, employeeId, sp, ct)) return Results.Forbid();
    var f = from ?? DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-7));
    var t = to ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
    if (f > t || t.DayNumber - f.DayNumber > 366)
    {
        return Results.BadRequest(new { error = "Date range must be ordered and no longer than 366 days." });
    }
    return Results.Ok(await reports.GetDailyAsync(employeeId, f, t, ct));
});

app.MapGet("/api/reports/employees/{employeeId:guid}/monthly", [Authorize] async (
    Guid employeeId, int? year, int? month, ClaimsPrincipal principal, ReportService reports, IServiceProvider sp, CancellationToken ct) =>
{
    if (!await CanReadEmployeeAsync(principal, employeeId, sp, ct)) return Results.Forbid();
    var y = year ?? DateTime.UtcNow.Year;
    var m = month ?? DateTime.UtcNow.Month;
    if (!IsValidPeriod(y, m)) return Results.BadRequest(new { error = "Year or month is outside the supported range." });
    try
    {
        return Results.Ok(await reports.GetMonthlyAsync(employeeId, y, m, ct));
    }
    catch (InvalidOperationException)
    {
        return Results.NotFound(new { error = "Employee not found." });
    }
});

app.MapGet("/api/reports/export", [Authorize] async (
    Guid employeeId, string? format, int? year, int? month, ClaimsPrincipal principal, ReportService reports, IServiceProvider sp, CancellationToken ct) =>
{
    if (!await CanReadEmployeeAsync(principal, employeeId, sp, ct)) return Results.Forbid();
    var y = year ?? DateTime.UtcNow.Year;
    var m = month ?? DateTime.UtcNow.Month;
    if (!IsValidPeriod(y, m)) return Results.BadRequest(new { error = "Year or month is outside the supported range." });
    var normalizedFormat = (format ?? "xlsx").ToLowerInvariant();
    if (normalizedFormat is not ("xlsx" or "pdf"))
    {
        return Results.BadRequest(new { error = "Format must be xlsx or pdf." });
    }

    try
    {
        if (normalizedFormat == "pdf")
        {
            var bytes = await reports.ExportPdfAsync(employeeId, y, m, ct);
            return Results.File(bytes, "application/pdf", $"attendance_{y}_{m:00}.pdf");
        }

        var xlsx = await reports.ExportExcelAsync(employeeId, y, m, ct);
        return Results.File(xlsx, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"attendance_{y}_{m:00}.xlsx");
    }
    catch (InvalidOperationException)
    {
        return Results.NotFound(new { error = "Employee not found." });
    }
});

// Reporting: Director and Accountant can fetch employee list (for report selection)
app.MapGet("/api/employees", [Authorize(Roles = "Admin,Manager,Director,Accountant")] async (AttendanceDbContext db, CancellationToken ct) =>
{
    var employees = await db.Employees.AsNoTracking()
        .Where(x => x.IsActive)
        .OrderBy(x => x.FullName)
        .Select(x => new { x.Id, x.FullName, x.EmployeeCode, x.Department })
        .ToListAsync(ct);
    return Results.Ok(employees);
});

// Admin CRUD via API (Laravel consumes these)
// GET endpoints accessible by Admin, Manager, Director, Accountant
var adminRead = app.MapGroup("/api/admin").RequireAuthorization(
    new AuthorizeAttribute { Roles = "Admin,Manager,Director,Accountant" });
var admin = app.MapGroup("/api/admin").RequireAuthorization(
    new AuthorizeAttribute { Roles = "Admin,Manager" });

adminRead.MapGet("/employees", async (AttendanceDbContext db, CancellationToken ct) =>
    Results.Ok(await db.Employees.AsNoTracking().OrderBy(x => x.FullName).ToListAsync(ct)));

admin.MapPost("/employees", [Authorize(Roles = "Admin")] async ([FromBody] EmployeeUpsertRequest req, AttendanceDbContext db, CancellationToken ct) =>
{
    var employeeCode = CleanRequired(req.EmployeeCode, 50);
    var fullName = CleanRequired(req.FullName, 200);
    if (employeeCode is null || fullName is null)
    {
        return Results.BadRequest(new { error = "Employee code and full name are required and must fit their maximum lengths." });
    }
    if (req.Department?.Length > 200) return Results.BadRequest(new { error = "Department is too long." });
    if (await db.Employees.AnyAsync(x => x.EmployeeCode == employeeCode, ct))
    {
        return Results.Conflict(new { error = "Employee code already exists." });
    }
    if (req.SiteId.HasValue && !await db.Sites.AnyAsync(x => x.Id == req.SiteId, ct))
    {
        return Results.BadRequest(new { error = "Site not found." });
    }
    if (req.UserId.HasValue && !await db.Users.AnyAsync(x => x.Id == req.UserId, ct))
    {
        return Results.BadRequest(new { error = "User not found." });
    }

    var emp = new Employee
    {
        Id = Guid.NewGuid(),
        EmployeeCode = employeeCode,
        FullName = fullName,
        Department = CleanOptional(req.Department),
        SiteId = req.SiteId,
        UserId = req.UserId,
        IsActive = req.IsActive ?? true
    };
    db.Employees.Add(emp);
    await db.SaveChangesAsync(ct);
    return Results.Created($"/api/admin/employees/{emp.Id}", emp);
});

admin.MapPut("/employees/{id:guid}", [Authorize(Roles = "Admin")] async (Guid id, [FromBody] EmployeeUpsertRequest req, AttendanceDbContext db, CancellationToken ct) =>
{
    var emp = await db.Employees.FirstOrDefaultAsync(x => x.Id == id, ct);
    if (emp is null) return Results.NotFound();
    var employeeCode = CleanRequired(req.EmployeeCode, 50);
    var fullName = CleanRequired(req.FullName, 200);
    if (employeeCode is null || fullName is null)
    {
        return Results.BadRequest(new { error = "Employee code and full name are required and must fit their maximum lengths." });
    }
    if (req.Department?.Length > 200) return Results.BadRequest(new { error = "Department is too long." });
    if (await db.Employees.AnyAsync(x => x.Id != id && x.EmployeeCode == employeeCode, ct))
    {
        return Results.Conflict(new { error = "Employee code already exists." });
    }
    if (req.SiteId.HasValue && !await db.Sites.AnyAsync(x => x.Id == req.SiteId, ct))
    {
        return Results.BadRequest(new { error = "Site not found." });
    }
    if (req.UserId.HasValue && !await db.Users.AnyAsync(x => x.Id == req.UserId, ct))
    {
        return Results.BadRequest(new { error = "User not found." });
    }

    emp.EmployeeCode = employeeCode;
    emp.FullName = fullName;
    emp.Department = CleanOptional(req.Department);
    emp.SiteId = req.SiteId;
    emp.UserId = req.UserId;
    if (req.IsActive.HasValue) emp.IsActive = req.IsActive.Value;
    await db.SaveChangesAsync(ct);
    return Results.Ok(emp);
});

adminRead.MapGet("/sites", async (AttendanceDbContext db, CancellationToken ct) =>
    Results.Ok(await db.Sites.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct)));

admin.MapPost("/sites", [Authorize(Roles = "Admin")] async ([FromBody] SiteUpsertRequest req, AttendanceDbContext db, CancellationToken ct) =>
{
    var name = CleanRequired(req.Name, 200);
    var timezone = string.IsNullOrWhiteSpace(req.Timezone) ? companyTimezone : req.Timezone.Trim();
    if (name is null || !IsValidTimezone(timezone))
    {
        return Results.BadRequest(new { error = "Site name or timezone is invalid." });
    }
    if (req.AllowedCidrs?.Length > 1000) return Results.BadRequest(new { error = "Allowed CIDRs value is too long." });

    var site = new Site
    {
        Id = Guid.NewGuid(),
        Name = name,
        Timezone = timezone,
        AllowedCidrs = CleanOptional(req.AllowedCidrs),
        IsActive = req.IsActive ?? true
    };
    db.Sites.Add(site);
    await db.SaveChangesAsync(ct);
    return Results.Created($"/api/admin/sites/{site.Id}", site);
});

admin.MapGet("/users", [Authorize(Roles = "Admin")] async (AttendanceDbContext db, CancellationToken ct) =>
{
    var users = await db.Users.AsNoTracking()
        .Include(x => x.UserRoles).ThenInclude(x => x.Role)
        .OrderBy(x => x.Email)
        .Select(x => new
        {
            x.Id,
            x.Email,
            x.DisplayName,
            x.IsActive,
            EmployeeId = x.Employee == null ? (Guid?)null : x.Employee.Id,
            Roles = x.UserRoles.Select(r => r.Role.Name).ToList()
        })
        .ToListAsync(ct);
    return Results.Ok(users);
});

admin.MapPost("/users", [Authorize(Roles = "Admin")] async ([FromBody] UserCreateRequest req, AttendanceDbContext db, CancellationToken ct) =>
{
    var email = req.Email?.Trim().ToLowerInvariant();
    var displayName = CleanRequired(req.DisplayName, 200);
    if (string.IsNullOrWhiteSpace(email) || email.Length > 256 || !MailAddress.TryCreate(email, out _) || displayName is null)
    {
        return Results.BadRequest(new { error = "Email or display name is invalid." });
    }
    if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 12 || req.Password.Length > 128)
    {
        return Results.BadRequest(new { error = "Password must be between 12 and 128 characters." });
    }
    if (await db.Users.AnyAsync(x => x.Email == email, ct))
    {
        return Results.Conflict(new { error = "Email already exists." });
    }

    var requestedRoleNames = (req.Roles ?? [])
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Select(x => x.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
    if (requestedRoleNames.Count == 0)
    {
        return Results.BadRequest(new { error = "At least one role is required." });
    }

    var availableRoles = await db.Roles.ToListAsync(ct);
    var selectedRoles = requestedRoleNames
        .Select(name => availableRoles.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        .ToList();
    if (selectedRoles.Any(x => x is null))
    {
        return Results.BadRequest(new { error = "One or more roles are invalid." });
    }

    Employee? employee = null;
    if (req.EmployeeId.HasValue)
    {
        employee = await db.Employees.FirstOrDefaultAsync(x => x.Id == req.EmployeeId, ct);
        if (employee is null) return Results.BadRequest(new { error = "Employee not found." });
        if (employee.UserId.HasValue) return Results.Conflict(new { error = "Employee is already linked to a user." });
    }

    await using var transaction = await db.Database.BeginTransactionAsync(ct);
    var user = new User
    {
        Id = Guid.NewGuid(),
        Email = email,
        DisplayName = displayName,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
        PreferredLanguage = "en",
        ThemePreference = "system",
        IsActive = true
    };
    db.Users.Add(user);
    foreach (var role in selectedRoles)
    {
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role!.Id });
    }
    if (employee is not null) employee.UserId = user.Id;
    await db.SaveChangesAsync(ct);
    await transaction.CommitAsync(ct);

    return Results.Created($"/api/admin/users/{user.Id}", new
    {
        user.Id,
        user.Email,
        user.DisplayName,
        EmployeeId = employee?.Id,
        Roles = selectedRoles.Select(x => x!.Name).ToList()
    });
});

admin.MapPost("/attendance/manual", [Authorize(Roles = "Admin,Manager")] async (
    [FromBody] ManualAttendanceRequest req,
    AttendanceService attendance,
    HttpContext http,
    CancellationToken ct) =>
{
    if (!Enum.TryParse<AttendanceEventType>(req.EventType, true, out var type) ||
        type is not (AttendanceEventType.In or AttendanceEventType.Out))
    {
        return Results.BadRequest(new { error = "EventType must be In or Out." });
    }
    var cmd = new AttendanceCommand(
        req.EmployeeId,
        AttendanceEventSource.Manual,
        req.SiteId,
        http.Connection.RemoteIpAddress?.ToString(),
        null,
        string.IsNullOrWhiteSpace(req.IdempotencyKey) ? Guid.NewGuid().ToString("N") : req.IdempotencyKey,
        req.Note ?? "Manual correction",
        req.OccurredAtUtc);

    try
    {
        var ev = type == AttendanceEventType.In
            ? await attendance.CheckInAsync(cmd, ct)
            : await attendance.CheckOutAsync(cmd, ct);
        return Results.Ok(ev);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.Run();

static Guid GetUserId(ClaimsPrincipal principal)
{
    var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? principal.FindFirstValue("sub")
        ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
    return Guid.Parse(sub!);
}

static async Task<Guid?> ResolveSelfEmployeeIdAsync(ClaimsPrincipal principal, IServiceProvider sp, CancellationToken ct)
{
    var claimEmp = principal.FindFirstValue("employee_id");
    Guid? selfEmp = Guid.TryParse(claimEmp, out var e) ? e : null;
    if (selfEmp.HasValue) return selfEmp;

    var db = sp.GetRequiredService<AttendanceDbContext>();
    var userId = GetUserId(principal);
        return await db.Employees.Where(x => x.UserId == userId).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
}

static async Task<bool> CanReadEmployeeAsync(
    ClaimsPrincipal principal,
    Guid employeeId,
    IServiceProvider sp,
    CancellationToken ct)
{
    if (HasAnyRole(principal, "Admin", "Manager", "Director", "Accountant"))
    {
        return true;
    }

    return await ResolveSelfEmployeeIdAsync(principal, sp, ct) == employeeId;
}

static bool HasAnyRole(ClaimsPrincipal principal, params string[] acceptedRoles)
{
    var roles = principal.FindAll(ClaimTypes.Role).Select(x => x.Value);
    return roles.Any(role => acceptedRoles.Contains(role, StringComparer.OrdinalIgnoreCase));
}

static AttendanceEventSource? ParseSelfServiceSource(string? source)
{
    if (string.IsNullOrWhiteSpace(source)) return AttendanceEventSource.Web;
    if (!Enum.TryParse<AttendanceEventSource>(source, true, out var parsed)) return null;
    return parsed is AttendanceEventSource.Web or AttendanceEventSource.Qr ? parsed : null;
}

static bool IsValidPeriod(int year, int month) => year is >= 2000 and <= 2100 && month is >= 1 and <= 12;

static bool IsValidTimezone(string timezone)
{
    try
    {
        _ = TimeZoneInfo.FindSystemTimeZoneById(timezone);
        return true;
    }
    catch (TimeZoneNotFoundException)
    {
        return false;
    }
    catch (InvalidTimeZoneException)
    {
        return false;
    }
}

static string? CleanRequired(string? value, int maxLength)
{
    var cleaned = value?.Trim();
    return string.IsNullOrEmpty(cleaned) || cleaned.Length > maxLength ? null : cleaned;
}

static string? CleanOptional(string? value)
{
    var cleaned = value?.Trim();
    return string.IsNullOrEmpty(cleaned) ? null : cleaned;
}

record LoginRequest(string? Email, string? Password);
record PreferencesRequest(string? Language, string? Theme);
record AttendanceRequest(Guid? EmployeeId, Guid? SiteId, string? Source, string? DeviceId, string? IdempotencyKey, string? Note, DateTimeOffset? OccurredAtUtc);
record HeartbeatRequest(Guid? EmployeeId, Guid? SiteId);
record EmployeeUpsertRequest(string? EmployeeCode, string? FullName, string? Department, Guid? SiteId, Guid? UserId, bool? IsActive);
record SiteUpsertRequest(string? Name, string? Timezone, string? AllowedCidrs, bool? IsActive);
record UserCreateRequest(string? Email, string? Password, string? DisplayName, IReadOnlyList<string>? Roles, Guid? EmployeeId);
record ManualAttendanceRequest(Guid EmployeeId, string? EventType, Guid? SiteId, DateTimeOffset? OccurredAtUtc, string? Note, string? IdempotencyKey);
