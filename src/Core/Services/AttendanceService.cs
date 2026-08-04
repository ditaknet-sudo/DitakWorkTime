using Ditak.Attendance.Core.Data;
using Ditak.Attendance.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ditak.Attendance.Core.Services;

public record AttendanceCommand(
    Guid EmployeeId,
    AttendanceEventSource Source,
    Guid? SiteId,
    string? ClientIp,
    string? DeviceId,
    string IdempotencyKey,
    string? Note,
    DateTimeOffset? OccurredAtUtc);

public record TodayStatusDto(
    Guid EmployeeId,
    bool IsCheckedIn,
    int WorkedMinutesToday,
    bool HasOpenShift,
    DateTimeOffset? LastEventAtUtc,
    AttendanceEventType? LastEventType);

public class AttendanceService
{
    private readonly AttendanceDbContext _db;
    private readonly string _companyTimezone;

    public AttendanceService(AttendanceDbContext db, string companyTimezone)
    {
        _db = db;
        _companyTimezone = companyTimezone;
    }

    public async Task<AttendanceEvent> CheckInAsync(AttendanceCommand cmd, CancellationToken ct = default)
        => await RecordAsync(cmd, AttendanceEventType.In, ct);

    public async Task<AttendanceEvent> CheckOutAsync(AttendanceCommand cmd, CancellationToken ct = default)
        => await RecordAsync(cmd, AttendanceEventType.Out, ct);

    private async Task<AttendanceEvent> RecordAsync(AttendanceCommand cmd, AttendanceEventType type, CancellationToken ct)
    {
        if (cmd.Source == AttendanceEventSource.Network)
        {
            throw new InvalidOperationException("Network source cannot create billing attendance events.");
        }

        if (string.IsNullOrWhiteSpace(cmd.IdempotencyKey) || cmd.IdempotencyKey.Length > 100)
        {
            throw new InvalidOperationException("Idempotency key is required and must not exceed 100 characters.");
        }

        if (cmd.DeviceId?.Length > 100 || cmd.Note?.Length > 500)
        {
            throw new InvalidOperationException("Attendance metadata is too long.");
        }

        var existing = await _db.AttendanceEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IdempotencyKey == cmd.IdempotencyKey, ct);
        if (existing is not null)
        {
            if (existing.EmployeeId == cmd.EmployeeId && existing.EventType == type)
            {
                return existing;
            }

            throw new InvalidOperationException("Idempotency key was already used for another attendance event.");
        }

        var employee = await _db.Employees.FirstOrDefaultAsync(x => x.Id == cmd.EmployeeId && x.IsActive, ct)
            ?? throw new InvalidOperationException("Employee not found or inactive.");

        if (cmd.SiteId.HasValue && !await _db.Sites.AnyAsync(x => x.Id == cmd.SiteId && x.IsActive, ct))
        {
            throw new InvalidOperationException("Site not found or inactive.");
        }

        var occurred = cmd.OccurredAtUtc ?? DateTimeOffset.UtcNow;
        if (cmd.Source != AttendanceEventSource.AutoCheckout && occurred > DateTimeOffset.UtcNow.AddMinutes(5))
        {
            throw new InvalidOperationException("Attendance event cannot be recorded in the future.");
        }

        if (cmd.Source is AttendanceEventSource.Web or AttendanceEventSource.Qr)
        {
            var last = await _db.AttendanceEvents.AsNoTracking()
                .Where(x => x.EmployeeId == employee.Id && x.Source != AttendanceEventSource.Network)
                .OrderByDescending(x => x.OccurredAtUtc)
                .ThenByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (type == AttendanceEventType.In && last?.EventType == AttendanceEventType.In)
            {
                throw new InvalidOperationException("Employee is already checked in.");
            }

            if (type == AttendanceEventType.Out && last?.EventType != AttendanceEventType.In)
            {
                throw new InvalidOperationException("Employee is not checked in.");
            }
        }

        var entity = new AttendanceEvent
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            SiteId = cmd.SiteId ?? employee.SiteId,
            EventType = type,
            Source = cmd.Source,
            OccurredAtUtc = occurred,
            ClientIp = cmd.ClientIp,
            DeviceId = cmd.DeviceId,
            IdempotencyKey = cmd.IdempotencyKey,
            Note = cmd.Note
        };

        _db.AttendanceEvents.Add(entity);
        await _db.SaveChangesAsync(ct);
        await RecalculateEmployeeAsync(employee.Id, ct);
        return entity;
    }

    /// <summary>
    /// Closes shifts left open past the employee company's local midnight.
    /// The generated event is timestamped at midnight, even if the worker
    /// catches up later after downtime.
    /// </summary>
    public async Task<int> AutoCloseOpenShiftsAsync(DateTimeOffset? asOfUtc = null, CancellationToken ct = default)
    {
        var now = asOfUtc ?? DateTimeOffset.UtcNow;
        var timezone = AttendanceCalculator.ResolveTimezone(_companyTimezone);
        var currentLocalDate = AttendanceCalculator.ToLocalDate(now, timezone);
        var employeeIds = await _db.Employees.AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => x.Id)
            .ToListAsync(ct);

        var lastEvents = await _db.AttendanceEvents.AsNoTracking()
            .Where(x => employeeIds.Contains(x.EmployeeId) && x.Source != AttendanceEventSource.Network)
            .OrderByDescending(x => x.OccurredAtUtc)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
        var lastByEmployee = lastEvents
            .GroupBy(x => x.EmployeeId)
            .ToDictionary(x => x.Key, x => x.First());

        var closed = 0;
        foreach (var employeeId in employeeIds)
        {
            if (!lastByEmployee.TryGetValue(employeeId, out var last) || last.EventType != AttendanceEventType.In)
            {
                continue;
            }

            var workDate = AttendanceCalculator.ToLocalDate(last.OccurredAtUtc, timezone);
            if (workDate >= currentLocalDate)
            {
                continue;
            }

            var closeAtUtc = GetNextLocalMidnightUtc(workDate, timezone);
            await CheckOutAsync(new AttendanceCommand(
                employeeId,
                AttendanceEventSource.AutoCheckout,
                last.SiteId,
                null,
                null,
                $"auto-checkout:{employeeId:N}:{workDate:yyyyMMdd}",
                "Automatic checkout at local midnight",
                closeAtUtc), ct);
            closed++;
        }

        return closed;
    }

    public static DateTimeOffset GetNextLocalMidnightUtc(DateOnly workDate, TimeZoneInfo timezone)
    {
        var localMidnight = DateTime.SpecifyKind(
            workDate.AddDays(1).ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localMidnight, timezone), TimeSpan.Zero);
    }

    public async Task RecalculateEmployeeAsync(Guid employeeId, CancellationToken ct = default)
    {
        var tz = AttendanceCalculator.ResolveTimezone(_companyTimezone);
        var events = await _db.AttendanceEvents
            .Where(x => x.EmployeeId == employeeId)
            .OrderBy(x => x.OccurredAtUtc)
            .ToListAsync(ct);

        var results = AttendanceCalculator.Calculate(events, tz);
        var existing = await _db.AttendanceDaySummaries
            .Where(x => x.EmployeeId == employeeId)
            .ToListAsync(ct);

        _db.AttendanceDaySummaries.RemoveRange(existing);

        foreach (var r in results)
        {
            _db.AttendanceDaySummaries.Add(new AttendanceDaySummary
            {
                Id = Guid.NewGuid(),
                EmployeeId = employeeId,
                WorkDate = r.WorkDate,
                WorkedMinutes = r.WorkedMinutes,
                HasOpenShift = r.HasOpenShift,
                CalculatedAtUtc = DateTimeOffset.UtcNow
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task RecalculateAllAsync(CancellationToken ct = default)
    {
        var ids = await _db.Employees.Where(x => x.IsActive).Select(x => x.Id).ToListAsync(ct);
        foreach (var id in ids)
        {
            await RecalculateEmployeeAsync(id, ct);
        }
    }

    public async Task<TodayStatusDto> GetTodayStatusAsync(Guid employeeId, CancellationToken ct = default)
    {
        var tz = AttendanceCalculator.ResolveTimezone(_companyTimezone);
        var today = AttendanceCalculator.ToLocalDate(DateTimeOffset.UtcNow, tz);
        var summary = await _db.AttendanceDaySummaries
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.WorkDate == today, ct);

        var last = await _db.AttendanceEvents
            .AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && x.Source != AttendanceEventSource.Network)
            .OrderByDescending(x => x.OccurredAtUtc)
            .FirstOrDefaultAsync(ct);

        var isCheckedIn = last?.EventType == AttendanceEventType.In;
        return new TodayStatusDto(
            employeeId,
            isCheckedIn,
            summary?.WorkedMinutes ?? 0,
            summary?.HasOpenShift ?? isCheckedIn,
            last?.OccurredAtUtc,
            last?.EventType);
    }
}
