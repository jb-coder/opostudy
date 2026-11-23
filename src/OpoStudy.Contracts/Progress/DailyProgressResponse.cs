namespace OpoStudy.Contracts.Progress;

/// <summary>Daily progress right after answering a question.</summary>
/// <param name="AnsweredToday">Questions answered today, including this one.</param>
/// <param name="DailyGoal">Goal in force today.</param>
/// <param name="GoalMetNow"><c>true</c> only for the answer that meets the goal.</param>
/// <param name="CurrentStreak">Current streak after this answer.</param>
public sealed record DailyProgressResponse(int AnsweredToday, int DailyGoal, bool GoalMetNow, int CurrentStreak);