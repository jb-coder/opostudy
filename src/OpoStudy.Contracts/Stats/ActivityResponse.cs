using OpoStudy.Contracts.Progress;

namespace OpoStudy.Contracts.Stats;

/// <summary>Days with activity in a date range.</summary>
/// <param name="Today">Today in the user's time zone.</param>
/// <param name="DailyGoal">Current daily goal.</param>
/// <param name="Days">Ascending; only days with activity, XP, a met goal or a streak freeze.</param>
public sealed record ActivityResponse(DateOnly Today, int DailyGoal, IReadOnlyList<DayActivityResponse> Days);