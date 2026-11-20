namespace OpoStudy.Contracts.Progress;

/// <summary>Changes the daily goal (questions per day).</summary>
public sealed record UpdateDailyGoalRequest(int DailyGoal);