using Ditak.Attendance.Core.Entities;
using Ditak.Attendance.Core.Services;

namespace Ditak.Attendance.Core.Tests;

public class AttendanceCalculatorTests
{
    [Fact]
    public void Calculate_PairsClosedShift()
    {
        var events = new[]
        {
            Event(AttendanceEventType.In, "2026-08-04T05:00:00Z"),
            Event(AttendanceEventType.Out, "2026-08-04T13:30:00Z")
        };

        var result = AttendanceCalculator.Calculate(events, TimeZoneInfo.Utc);

        var day = Assert.Single(result);
        Assert.Equal(new DateOnly(2026, 8, 4), day.WorkDate);
        Assert.Equal(510, day.WorkedMinutes);
        Assert.False(day.HasOpenShift);
    }

    [Fact]
    public void Calculate_OpenShiftUsesProvidedAsOfTime()
    {
        var events = new[] { Event(AttendanceEventType.In, "2026-08-04T05:00:00Z") };

        var result = AttendanceCalculator.Calculate(
            events,
            TimeZoneInfo.Utc,
            DateTimeOffset.Parse("2026-08-04T07:15:00Z"));

        var day = Assert.Single(result);
        Assert.Equal(135, day.WorkedMinutes);
        Assert.True(day.HasOpenShift);
    }

    [Fact]
    public void Calculate_AttributesCrossMidnightShiftToCheckInDate()
    {
        var events = new[]
        {
            Event(AttendanceEventType.In, "2026-08-04T21:30:00Z"),
            Event(AttendanceEventType.Out, "2026-08-05T01:00:00Z")
        };

        var result = AttendanceCalculator.Calculate(events, TimeZoneInfo.Utc);

        var day = Assert.Single(result);
        Assert.Equal(new DateOnly(2026, 8, 4), day.WorkDate);
        Assert.Equal(210, day.WorkedMinutes);
    }

    [Fact]
    public void NextLocalMidnight_IsConvertedToUtc()
    {
        var timezone = AttendanceCalculator.ResolveTimezone("Asia/Yerevan");

        var result = AttendanceService.GetNextLocalMidnightUtc(new DateOnly(2026, 8, 4), timezone);

        Assert.Equal(DateTimeOffset.Parse("2026-08-04T20:00:00Z"), result);
    }

    private static AttendanceEvent Event(AttendanceEventType type, string occurredAtUtc)
    {
        var occurred = DateTimeOffset.Parse(occurredAtUtc);
        return new AttendanceEvent
        {
            Id = Guid.NewGuid(),
            EmployeeId = Guid.NewGuid(),
            EventType = type,
            Source = AttendanceEventSource.Web,
            OccurredAtUtc = occurred,
            CreatedAt = occurred,
            IdempotencyKey = Guid.NewGuid().ToString("N")
        };
    }
}
