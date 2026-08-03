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

        var existing = await _db.AttendanceEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IdempotencyKey == cmd.IdempotencyKey, ct);
        if (existing is not null)
        {
            return existing;
        }

        var employee = await _db.Employees.FirstOrDefaultAsync(x => x.Id == cmd.EmployeeId && x.IsActive, ct)
            ?? throw new InvalidOperationException("Employee not found or inactive.");

        var occurred = cmd.OccurredAtUtc ?? DateTimeOffset.UtcNow;
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
