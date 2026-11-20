namespace OpoStudy.Infrastructure.Persistence;

/// <summary>Database constraint names that callers need to recognise in database errors.</summary>
public static class DbConstraintNames
{
    /// <summary>Primary key of daily_activities: a violation means two requests created the same day's row at once.</summary>
    public const string DailyActivitiesPrimaryKey = "pk_daily_activities";
}
