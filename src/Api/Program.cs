using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Ditak.Attendance.Api.Auth;
using Ditak.Attendance.Core;
using Ditak.Attendance.Core.Data;
using Ditak.Attendance.Core.Entities;
using Ditak.Attendance.Core.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is required.");
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret is required.");
if (jwtSecret.Length < 32)
{
    throw new InvalidOperationException("Security Error: Jwt:Secret must be at least 32 characters long.");
}

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
        builder.Configuration["Seed:AdminPassword"] ?? "ChangeMe123!",
        builder.Configuration["Seed:AdminName"] ?? "System Admin");
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    version = builder.Configuration["Product:Version"] ?? "1.0.0",
    utc = DateTimeOffset.UtcNow
}));

app.MapPost("/api/auth/login", async ([FromBody] LoginRequest req, AuthService auth, CancellationToken ct) =>
{
    var result = await auth.LoginAsync(req.Email, req.Password, ct);
    return result is null
        ? Results.Unauthorized()
        : Results.Ok(new { token = result.Value.Token, user = result.Value.User });
});

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
    var employeeId = await ResolveEmployeeIdAsync(principal, req.EmployeeId, http.RequestServices, ct);
    if (employeeId is null) return Results.BadRequest(new { error = "Employee profile required." });

    var source = ParseSource(req.Source) ?? AttendanceEventSource.Web;
    if (source is AttendanceEventSource.Network) return Results.BadRequest(new { error = "Invalid source." });

    try
    {
        var ev = await attendance.CheckInAsync(new AttendanceCommand(
            employeeId.Value,
            source,
            req.SiteId,
            http.Connection.RemoteIpAddress?.ToString(),
            req.DeviceId,
            string.IsNullOrWhiteSpace(req.IdempotencyKey) ? Guid.NewGuid().ToString("N") : req.IdempotencyKey,
            req.Note,
            req.OccurredAtUtc), ct);
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
    var employeeId = await ResolveEmployeeIdAsync(principal, req.EmployeeId, http.RequestServices, ct);
    if (employeeId is null) return Results.BadRequest(new { error = "Employee profile required." });

    var source = ParseSource(req.Source) ?? AttendanceEventSource.Web;
    if (source is AttendanceEventSource.Network) return Results.BadRequest(new { error = "Invalid source." });

    try
    {
        var ev = await attendance.CheckOutAsync(new AttendanceCommand(
            employeeId.Value,
            source,
            req.SiteId,
            http.Connection.RemoteIpAddress?.ToString(),
            req.DeviceId,
            string.IsNullOrWhiteSpace(req.IdempotencyKey) ? Guid.NewGuid().ToString("N") : req.IdempotencyKey,
            req.Note,
            req.OccurredAtUtc), ct);
        return Results.Ok(ev);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/attendance/me/today", [Authorize] async (ClaimsPrincipal principal, AttendanceService attendance, IServiceProvider sp, CancellationToken ct) =>
{
    var employeeId = await ResolveEmployeeIdAsync(principal, null, sp, ct);
    if (employeeId is null) return Results.BadRequest(new { error = "Employee profile required." });
    return Results.Ok(await attendance.GetTodayStatusAsync(employeeId.Value, ct));
});

app.MapGet("/api/attendance/presence", [Authorize] async (PresenceService presence, CancellationToken ct) =>
    Results.Ok(await presence.GetPresenceBoardAsync(ct)));

app.MapPost("/api/devices/heartbeat", [Authorize] async (
    [FromBody] HeartbeatRequest req,
    ClaimsPrincipal principal,
    PresenceService presence,
    HttpContext http,
    CancellationToken ct) =>
{
    var employeeId = await ResolveEmployeeIdAsync(principal, req.EmployeeId, http.RequestServices, ct);
    if (employeeId is null) return Results.BadRequest(new { error = "Employee profile required." });
    var ip = req.ClientIp ?? http.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
    await presence.UpsertHintAsync(employeeId.Value, ip, req.SiteId, ct);
    return Results.Ok(new { status = "hint_recorded", billing = false });
});

app.MapGet("/api/reports/employees/{employeeId:guid}/daily", [Authorize] async (
    Guid employeeId, DateOnly? from, DateOnly? to, ReportService reports, CancellationToken ct) =>
{
    var f = from ?? DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-7));
    var t = to ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
    return Results.Ok(await reports.GetDailyAsync(employeeId, f, t, ct));
});

app.MapGet("/api/reports/employees/{employeeId:guid}/monthly", [Authorize] async (
    Guid employeeId, int? year, int? month, ReportService reports, CancellationToken ct) =>
{
    var y = year ?? DateTime.UtcNow.Year;
    var m = month ?? DateTime.UtcNow.Month;
    return Results.Ok(await reports.GetMonthlyAsync(employeeId, y, m, ct));
});

app.MapGet("/api/reports/export", [Authorize] async (
    Guid employeeId, string format, int? year, int? month, ReportService reports, CancellationToken ct) =>
{
    var y = year ?? DateTime.UtcNow.Year;
    var m = month ?? DateTime.UtcNow.Month;
    format = (format ?? "xlsx").ToLowerInvariant();
    if (format == "pdf")
    {
        var bytes = await reports.ExportPdfAsync(employeeId, y, m, ct);
        return Results.File(bytes, "application/pdf", $"attendance_{y}_{m:00}.pdf");
    }

    var xlsx = await reports.ExportExcelAsync(employeeId, y, m, ct);
    return Results.File(xlsx, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"attendance_{y}_{m:00}.xlsx");
});

// Admin CRUD via API (Laravel consumes these)
var admin = app.MapGroup("/api/admin").RequireAuthorization(new AuthorizeAttribute { Roles = "Admin,Manager" });

admin.MapGet("/employees", async (AttendanceDbContext db, CancellationToken ct) =>
    Results.Ok(await db.Employees.AsNoTracking().OrderBy(x => x.FullName).ToListAsync(ct)));

admin.MapPost("/employees", [Authorize(Roles = "Admin")] async ([FromBody] EmployeeUpsertRequest req, AttendanceDbContext db, CancellationToken ct) =>
{
    var emp = new Employee
    {
        Id = Guid.NewGuid(),
        EmployeeCode = req.EmployeeCode.Trim(),
        FullName = req.FullName.Trim(),
        Department = req.Department,
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
    emp.EmployeeCode = req.EmployeeCode.Trim();
    emp.FullName = req.FullName.Trim();
    emp.Department = req.Department;
    emp.SiteId = req.SiteId;
    emp.UserId = req.UserId;
    if (req.IsActive.HasValue) emp.IsActive = req.IsActive.Value;
    await db.SaveChangesAsync(ct);
    return Results.Ok(emp);
});

admin.MapGet("/sites", async (AttendanceDbContext db, CancellationToken ct) =>
    Results.Ok(await db.Sites.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct)));

admin.MapPost("/sites", [Authorize(Roles = "Admin")] async ([FromBody] SiteUpsertRequest req, AttendanceDbContext db, CancellationToken ct) =>
{
    var site = new Site
    {
        Id = Guid.NewGuid(),
        Name = req.Name.Trim(),
        Timezone = string.IsNullOrWhiteSpace(req.Timezone) ? companyTimezone : req.Timezone,
        AllowedCidrs = req.AllowedCidrs,
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
            Roles = x.UserRoles.Select(r => r.Role.Name).ToList()
        })
        .ToListAsync(ct);
    return Results.Ok(users);
});

admin.MapPost("/attendance/manual", [Authorize(Roles = "Admin,Manager")] async (
    [FromBody] ManualAttendanceRequest req,
    AttendanceService attendance,
    HttpContext http,
    CancellationToken ct) =>
{
    var type = req.EventType?.Equals("Out", StringComparison.OrdinalIgnoreCase) == true
        ? AttendanceEventType.Out
        : AttendanceEventType.In;
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

static async Task<Guid?> ResolveEmployeeIdAsync(ClaimsPrincipal principal, Guid? requested, IServiceProvider sp, CancellationToken ct)
{
    var roles = principal.FindAll(ClaimTypes.Role).Select(x => x.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
    var claimEmp = principal.FindFirstValue("employee_id");
    Guid? selfEmp = Guid.TryParse(claimEmp, out var e) ? e : null;

    if (requested.HasValue)
    {
        if (roles.Contains("Admin") || roles.Contains("Manager") || selfEmp == requested)
        {
            return requested;
        }

        return null;
    }

    if (selfEmp.HasValue) return selfEmp;

    var db = sp.GetRequiredService<AttendanceDbContext>();
    var userId = GetUserId(principal);
    return await db.Employees.Where(x => x.UserId == userId).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
}

static AttendanceEventSource? ParseSource(string? source)
{
    if (string.IsNullOrWhiteSpace(source)) return null;
    return Enum.TryParse<AttendanceEventSource>(source, true, out var s) ? s : null;
}

record LoginRequest(string Email, string Password);
record PreferencesRequest(string? Language, string? Theme);
record AttendanceRequest(Guid? EmployeeId, Guid? SiteId, string? Source, string? DeviceId, string? IdempotencyKey, string? Note, DateTimeOffset? OccurredAtUtc);
record HeartbeatRequest(Guid? EmployeeId, Guid? SiteId, string? ClientIp);
record EmployeeUpsertRequest(string EmployeeCode, string FullName, string? Department, Guid? SiteId, Guid? UserId, bool? IsActive);
record SiteUpsertRequest(string Name, string? Timezone, string? AllowedCidrs, bool? IsActive);
record ManualAttendanceRequest(Guid EmployeeId, string? EventType, Guid? SiteId, DateTimeOffset? OccurredAtUtc, string? Note, string? IdempotencyKey);
