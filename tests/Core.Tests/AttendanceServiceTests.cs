using Ditak.Attendance.Core.Data;
using Ditak.Attendance.Core.Entities;
using Ditak.Attendance.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace Ditak.Attendance.Core.Tests;

public class AttendanceServiceTests
{
    [Fact]
    public async Task AutoCloseOpenShifts_ClosesAtMidnightAndIsIdempotent()
    {
        await using var db = CreateDatabase();
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            EmployeeCode = "TEST-1",
            FullName = "Test Employee"
        };
        db.Employees.Add(employee);
        db.AttendanceEvents.Add(new AttendanceEvent
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            EventType = AttendanceEventType.In,
            Source = AttendanceEventSource.Web,
            OccurredAtUtc = DateTimeOffset.Parse("2026-08-04T10:00:00Z"),
            CreatedAt = DateTimeOffset.Parse("2026-08-04T10:00:00Z"),
            IdempotencyKey = "initial-check-in"
        });
        await db.SaveChangesAsync();
        var service = new AttendanceService(db, "UTC");

        var firstRun = await service.AutoCloseOpenShiftsAsync(DateTimeOffset.Parse("2026-08-05T12:00:00Z"));
        var secondRun = await service.AutoCloseOpenShiftsAsync(DateTimeOffset.Parse("2026-08-05T12:05:00Z"));

        Assert.Equal(1, firstRun);
        Assert.Equal(0, secondRun);
        var checkout = await db.AttendanceEvents.SingleAsync(x => x.EventType == AttendanceEventType.Out);
        Assert.Equal(AttendanceEventSource.AutoCheckout, checkout.Source);
        Assert.Equal(DateTimeOffset.Parse("2026-08-05T00:00:00Z"), checkout.OccurredAtUtc);
        var summary = await db.AttendanceDaySummaries.SingleAsync();
        Assert.Equal(840, summary.WorkedMinutes);
        Assert.False(summary.HasOpenShift);
    }

    [Fact]
    public async Task WebAttendance_RejectsInvalidStateTransitions()
    {
        await using var db = CreateDatabase();
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            EmployeeCode = "TEST-2",
            FullName = "Test Employee"
        };
        db.Employees.Add(employee);
        await db.SaveChangesAsync();
        var service = new AttendanceService(db, "UTC");
        var checkIn = Command(employee.Id, "check-in");

        await service.CheckInAsync(checkIn);

        var duplicate = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CheckInAsync(Command(employee.Id, "second-check-in")));
        Assert.Equal("Employee is already checked in.", duplicate.Message);

        await service.CheckOutAsync(Command(employee.Id, "check-out"));
        var extraCheckout = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CheckOutAsync(Command(employee.Id, "second-check-out")));
        Assert.Equal("Employee is not checked in.", extraCheckout.Message);
    }

    private static AttendanceDbContext CreateDatabase()
    {
        var options = new DbContextOptionsBuilder<AttendanceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new AttendanceDbContext(options);
    }

    private static AttendanceCommand Command(Guid employeeId, string idempotencyKey) => new(
        employeeId,
        AttendanceEventSource.Web,
        null,
        "127.0.0.1",
        null,
        idempotencyKey,
        null,
        null);
}
