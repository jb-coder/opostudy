namespace OpoStudy.Infrastructure.Persistence;

/// <summary>Database constraint names that callers need to recognise in database errors.</summary>
public static class DbConstraintNames
{
    /// <summary>Primary key of daily_activities: a violation means two requests created the same day's row at once.</summary>
    public const string DailyActivitiesPrimaryKey = "pk_daily_activities";

    /// <summary>Primary key of review_schedules: a violation means two requests scheduled the same new question at once.</summary>
    public const string ReviewSchedulesPrimaryKey = "pk_review_schedules";

    /// <summary>Primary key of quiz_attempts: a violation means two syncs created the same quiz id at once.</summary>
    public const string QuizAttemptsPrimaryKey = "pk_quiz_attempts";

    /// <summary>Primary key of question_bookmarks: a violation means two requests bookmarked the same question at once.</summary>
    public const string QuestionBookmarksPrimaryKey = "pk_question_bookmarks";

    /// <summary>Partial unique index of question_reports: a violation means the user already has an open report of the question (also under concurrent requests).</summary>
    public const string QuestionReportsOneOpenPerUserIndex = "ux_question_reports_one_open_per_user";

    /// <summary>Partial unique index of gradings: a violation means the user already has a grading in progress (also under concurrent submissions).</summary>
    public const string GradingsOneInProgressPerUserIndex = "ux_gradings_one_in_progress_per_user";
}
