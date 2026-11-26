namespace OpoStudy.Contracts.Reviews;

/// <summary>Questions due for spaced review today: the total and the count per exam (only exams with at least one).</summary>
public sealed record DueReviewSummaryResponse(int Total, IReadOnlyList<ExamDueReviewsResponse> Exams);
