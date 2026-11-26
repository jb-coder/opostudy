namespace OpoStudy.Contracts.MockExams;

/// <summary>Submits a mock exam with the answered questions only.</summary>
public sealed record SubmitMockExamRequest(IReadOnlyList<MockExamAnswer> Answers);
