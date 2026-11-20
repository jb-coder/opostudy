namespace OpoStudy.Mobile.Core.Reminders;

/// <summary>Local copy of the daily progress, read by the alarm receiver without network access.</summary>
public sealed record ReminderSnapshot(DateOnly Date, int AnsweredToday, int DailyGoal, bool GoalMetToday, int CurrentStreak);