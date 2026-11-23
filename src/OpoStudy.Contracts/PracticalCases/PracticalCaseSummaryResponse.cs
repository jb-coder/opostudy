namespace OpoStudy.Contracts.PracticalCases;

/// <summary>A practical case in the list of an exam.</summary>
public sealed record PracticalCaseSummaryResponse(Guid Id, string Title, int PartCount, int MaxPoints, int EstimatedMinutes);
