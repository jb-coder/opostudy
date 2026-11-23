namespace OpoStudy.Infrastructure.Persistence;

/// <summary>Database constraint names that callers need to recognise in database errors.</summary>
public static class DbConstraintNames
{
    /// <summary>Primary key of daily_activities: a violation means two requests created the same day's row at once.</summary>
    public const string DailyActivitiesPrimaryKey = "pk_daily_activities";

    /// <summary>Primary key of quiz_attempts: a violation means two syncs created the same quiz id at once.</summary>
    public const string QuizAttemptsPrimaryKey = "pk_quiz_attempts";

    /// <summary>Partial unique index of gradings: a violation means the user already has a grading in progress (also under concurrent submissions).</summary>
    public const string GradingsOneInProgressPerUserIndex = "ux_gradings_one_in_progress_per_user";
}
