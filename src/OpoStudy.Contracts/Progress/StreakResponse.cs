namespace OpoStudy.Contracts.Progress;

/// <summary>Daily goal progress and streak of the user.</summary>
/// <param name="DailyGoal">Current daily goal (questions per day).</param>
/// <param name="Today">Today's date in the user's time zone.</param>
/// <param name="AnsweredToday">Questions answered today.</param>
/// <param name="GoalMetToday">Whether today's goal is met.</param>
/// <param name="CurrentStreak">Consecutive days meeting the goal (0 if the streak is broken).</param>
/// <param name="LongestStreak">Best streak ever.</param>
/// <param name="LastDays">The 7 days ending today in ascending order, including days without activity.</param>
public sealed record StreakResponse(
    int DailyGoal,
    DateOnly Today,
    int AnsweredToday,
    bool GoalMetToday,
    int CurrentStreak,
    int LongestStreak,
    IReadOnlyList<DayActivityResponse> LastDays);