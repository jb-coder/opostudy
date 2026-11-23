namespace OpoStudy.Contracts.Progress;

/// <summary>Activity of one day. On days without activity <paramref name="Goal"/> is the current goal. <paramref name="Protected"/>: a streak freeze covered the day.</summary>
public sealed record DayActivityResponse(DateOnly Date, int AnsweredCount, int Goal, bool GoalMet, bool Protected);