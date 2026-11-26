namespace OpoStudy.Contracts.MockExams;

/// <summary>Result of submitting a mock exam and the XP its answers earned.</summary>
public sealed record SubmitMockExamResponse(MockExamResultResponse Result, int XpGained);
