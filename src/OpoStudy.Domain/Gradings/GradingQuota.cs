namespace OpoStudy.Domain.Gradings;

/// <summary>The monthly quota of gradings: the period is the calendar month in the time zone of the user.</summary>
public static class GradingQuota
{
    /// <summary>The calendar month of the user that contains <paramref name="now"/>, as UTC instants: <c>Start</c> inclusive and <c>ResetsAt</c> (start of the next month) exclusive.</summary>
    public static (DateTimeOffset Start, DateTimeOffset ResetsAt) PeriodAt(string timeZone, DateTimeOffset now)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        var local = TimeZoneInfo.ConvertTime(now, zone);
        var start = new DateTime(local.Year, local.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        return (StartOfLocalDay(start, zone), StartOfLocalDay(start.AddMonths(1), zone));
    }

    private static DateTimeOffset StartOfLocalDay(DateTime localMidnight, TimeZoneInfo zone)
    {
        // Midnight does not exist on the day of a clock change that skips it: the day then starts an hour later.
        var local = zone.IsInvalidTime(localMidnight) ? localMidnight.AddHours(1) : localMidnight;
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
    }
}
