namespace OpoStudy.Contracts.Gradings;

/// <summary>A grading in the history of the user.</summary>
public sealed record GradingSummaryResponse(
    Guid Id,
    Guid PracticalCaseId,
    string CaseTitle,
    string? Section,
    string Status,
    DateTimeOffset SubmittedAt,
    int MaxPoints,
    decimal? Score);
