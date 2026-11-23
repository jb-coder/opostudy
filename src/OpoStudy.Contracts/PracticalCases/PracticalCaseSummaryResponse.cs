namespace OpoStudy.Contracts.PracticalCases;

/// <summary>A practical case in the list of an exam.</summary>
public sealed record PracticalCaseSummaryResponse(Guid Id, string Title, string? Section, int PartCount, int MaxPoints, int EstimatedMinutes);
