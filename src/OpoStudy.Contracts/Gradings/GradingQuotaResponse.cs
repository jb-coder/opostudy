namespace OpoStudy.Contracts.Gradings;

/// <summary>The monthly quota of gradings of the user; <c>ResetsAt</c> is the start of the next month in the user's time zone, in UTC.</summary>
public sealed record GradingQuotaResponse(int Limit, int Used, int Remaining, DateTimeOffset ResetsAt);
