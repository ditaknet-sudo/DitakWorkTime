using Ditak.Attendance.Core.Entities;

namespace Ditak.Attendance.Core.Services;

public record DayCalculationResult(DateOnly WorkDate, int WorkedMinutes, bool HasOpenShift);

public static class AttendanceCalculator
{
    /// <summary>
    /// Pairs In→Out chronologically. Supports shifts that cross midnight by
    /// attributing worked minutes to the date of the In event (site-local).
    /// </summary>
    public static IReadOnlyList<DayCalculationResult> Calculate(
        IEnumerable<AttendanceEvent> events,
        TimeZoneInfo timezone,
        DateTimeOffset? asOfUtc = null)
    {
        asOfUtc ??= DateTimeOffset.UtcNow;
        var ordered = events
            .Where(e => e.Source != AttendanceEventSource.Network)
            .OrderBy(e => e.OccurredAtUtc)
            .ThenBy(e => e.CreatedAt)
            .ToList();

        var buckets = new Dictionary<DateOnly, (int Minutes, bool Open)>();
        AttendanceEvent? openIn = null;

        foreach (var ev in ordered)
        {
            if (ev.EventType == AttendanceEventType.In)
            {
                // Consecutive In replaces previous open In (no double count).
                openIn = ev;
                continue;
            }

            if (ev.EventType == AttendanceEventType.Out && openIn is not null)
            {
                var workDate = ToLocalDate(openIn.OccurredAtUtc, timezone);
                var minutes = (int)Math.Max(0, (ev.OccurredAtUtc - openIn.OccurredAtUtc).TotalMinutes);
                Add(buckets, workDate, minutes, open: false);
                openIn = null;
            }
        }

        if (openIn is not null)
        {
            var workDate = ToLocalDate(openIn.OccurredAtUtc, timezone);
            var minutes = (int)Math.Max(0, (asOfUtc.Value - openIn.OccurredAtUtc).TotalMinutes);
            Add(buckets, workDate, minutes, open: true);
        }

        return buckets
            .OrderBy(x => x.Key)
            .Select(x => new DayCalculationResult(x.Key, x.Value.Minutes, x.Value.Open))
            .ToList();
    }

    private static void Add(Dictionary<DateOnly, (int Minutes, bool Open)> buckets, DateOnly date, int minutes, bool open)
    {
        if (buckets.TryGetValue(date, out var existing))
        {
            buckets[date] = (existing.Minutes + minutes, existing.Open || open);
        }
        else
        {
            buckets[date] = (minutes, open);
        }
    }

    public static DateOnly ToLocalDate(DateTimeOffset utc, TimeZoneInfo timezone)
    {
        var local = TimeZoneInfo.ConvertTime(utc, timezone);
        return DateOnly.FromDateTime(local.DateTime);
    }

    public static TimeZoneInfo ResolveTimezone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("UTC");
            }
            catch
            {
                return TimeZoneInfo.Utc;
            }
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
