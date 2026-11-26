namespace OpoStudy.Contracts.Reviews;

/// <summary>Questions of one exam that are due for spaced review today.</summary>
public sealed record ExamDueReviewsResponse(Guid ExamId, string ExamName, int Count);
