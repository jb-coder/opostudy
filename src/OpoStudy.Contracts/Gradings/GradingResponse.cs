namespace OpoStudy.Contracts.Gradings;

/// <summary>
/// A grading. <c>Status</c> is <c>Pending</c>, <c>Processing</c>, <c>Completed</c> or <c>Failed</c>.
/// The rubric and the model answers are only included when it is <c>Completed</c>.
/// </summary>
public sealed record GradingResponse(
    Guid Id,
    Guid PracticalCaseId,
    string CaseTitle,
    string Status,
    int Attempts,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? CompletedAt,
    int MaxPoints,
    decimal? Score,
    string? Feedback,
    string? FailureCode,
    IReadOnlyList<GradingAnswerResponse> Answers);

/// <summary>The answer to a part. <c>Score</c>, <c>Feedback</c>, <c>ModelAnswer</c> and <c>Criteria</c> are <c>null</c> until the grading is completed.</summary>
public sealed record GradingAnswerResponse(
    int Order,
    string Statement,
    string Text,
    int MaxPoints,
    decimal? Score,
    string? Feedback,
    string? ModelAnswer,
    IReadOnlyList<CriterionScoreResponse>? Criteria);

/// <summary>A criterion of the rubric with the points awarded.</summary>
public sealed record CriterionScoreResponse(string Description, int Points, decimal Awarded);
