namespace OpoStudy.Contracts.PracticalCases;

/// <summary>A practical case with its parts. It never includes the rubric or the model answers.</summary>
public sealed record PracticalCaseResponse(
    Guid Id,
    Guid ExamId,
    string Title,
    string Statement,
    int EstimatedMinutes,
    int MaxPoints,
    IReadOnlyList<PracticalCasePartResponse> Parts);
