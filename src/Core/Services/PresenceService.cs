using Ditak.Attendance.Core.Data;
using Ditak.Attendance.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ditak.Attendance.Core.Services;

public record PresenceRowDto(
    Guid EmployeeId,
    string FullName,
    string? Department,
    bool OfficiallyCheckedIn,
    bool SeenOnNetwork,
    DateTimeOffset? LastSeenAtUtc,
    string? ClientIp);

public class PresenceService
{
    private readonly AttendanceDbContext _db;

    public PresenceService(AttendanceDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Upserts a non-billing presence hint. Never creates attendance In/Out events.
    /// </summary>
    public async Task UpsertHintAsync(Guid employeeId, string clientIp, Guid? siteId, CancellationToken ct = default)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(x => x.Id == employeeId && x.IsActive, ct)
            ?? throw new InvalidOperationException("Employee not found or inactive.");

        var hint = await _db.PresenceHints.FirstOrDefaultAsync(x => x.EmployeeId == employeeId, ct);
        if (hint is null)
        {
            hint = new PresenceHint
            {
                Id = Guid.NewGuid(),
                EmployeeId = employee.Id
            };
            _db.PresenceHints.Add(hint);
        }

        hint.ClientIp = clientIp;
        hint.SiteId = siteId ?? employee.SiteId;
        hint.LastSeenAtUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PresenceRowDto>> GetPresenceBoardAsync(CancellationToken ct = default)
    {
        var employees = await _db.Employees.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.FullName)
            .ToListAsync(ct);

        var hints = await _db.PresenceHints.AsNoTracking().ToListAsync(ct);
        var hintMap = hints.ToDictionary(x => x.EmployeeId);

        var employeeIds = employees.Select(x => x.Id).ToList();
        var recentEvents = await _db.AttendanceEvents.AsNoTracking()
            .Where(x => employeeIds.Contains(x.EmployeeId) && x.Source != AttendanceEventSource.Network)
            .OrderByDescending(x => x.OccurredAtUtc)
            .ToListAsync(ct);
        var lastMap = recentEvents
            .GroupBy(x => x.EmployeeId)
            .ToDictionary(g => g.Key, g => g.First());

        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-15);
        var rows = new List<PresenceRowDto>();
        foreach (var emp in employees)
        {
            hintMap.TryGetValue(emp.Id, out var hint);
            lastMap.TryGetValue(emp.Id, out var last);
            var checkedIn = last?.EventType == AttendanceEventType.In;
            var seen = hint is not null && hint.LastSeenAtUtc >= cutoff;
            rows.Add(new PresenceRowDto(
                emp.Id,
                emp.FullName,
                emp.Department,
                checkedIn,
                seen,
                hint?.LastSeenAtUtc,
                hint?.ClientIp));
        }

        return rows;
    }
}
