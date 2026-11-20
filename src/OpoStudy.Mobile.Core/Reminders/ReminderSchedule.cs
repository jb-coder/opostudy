namespace OpoStudy.Mobile.Core.Reminders;

/// <summary>Computes when the next reminder fires.</summary>
public static class ReminderSchedule
{
    /// <summary>
    /// Today at <paramref name="time"/> if still in the future, otherwise tomorrow, in <paramref name="zone"/>.
    /// A time skipped by a daylight-saving jump moves to the next valid one.
    /// </summary>
    public static DateTimeOffset NextTrigger(DateTimeOffset now, TimeOnly time, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        var localNow = TimeZoneInfo.ConvertTime(now, zone);
        var date = DateOnly.FromDateTime(localNow.DateTime);
        var candidate = At(date, time, zone);
        return candidate > now ? candidate : At(date.AddDays(1), time, zone);
    }

    private static DateTimeOffset At(DateOnly date, TimeOnly time, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        while (zone.IsInvalidTime(local))
        {
            local = local.AddMinutes(30);
        }

        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }
}