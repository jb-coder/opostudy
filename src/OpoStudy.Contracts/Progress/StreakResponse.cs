namespace OpoStudy.Contracts.Progress;

/// <summary>Daily goal progress and streak of the user.</summary>
/// <param name="DailyGoal">Current daily goal (questions per day).</param>
/// <param name="Today">Today's date in the user's time zone.</param>
/// <param name="AnsweredToday">Questions answered today.</param>
/// <param name="GoalMetToday">Whether today's goal is met.</param>
/// <param name="CurrentStreak">Consecutive days meeting the goal (0 if the streak is broken).</param>
/// <param name="LongestStreak">Best streak ever.</param>
/// <param name="LastDays">The 7 days ending today in ascending order, including days without activity.</param>
/// <param name="StreakFreezes">Streak freezes in stock.</param>
/// <param name="MaxStreakFreezes">Maximum freezes that can be stored.</param>
/// <param name="StreakFreezePrice">XP price of one freeze.</param>
/// <param name="FreezesUsedNow">Days protected by this very request (0 in later requests).</param>
public sealed record StreakResponse(
    int DailyGoal,
    DateOnly Today,
    int AnsweredToday,
    bool GoalMetToday,
    int CurrentStreak,
    int LongestStreak,
    IReadOnlyList<DayActivityResponse> LastDays,
    int StreakFreezes,
    int MaxStreakFreezes,
    int StreakFreezePrice,
    int FreezesUsedNow);