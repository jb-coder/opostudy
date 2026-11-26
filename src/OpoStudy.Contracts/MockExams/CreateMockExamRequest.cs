namespace OpoStudy.Contracts.MockExams;

/// <summary>Starts a mock exam of an exercise. <paramref name="PassScore"/> defaults to half of the maximum score.</summary>
public sealed record CreateMockExamRequest(Guid ExamId, int ExerciseNumber, decimal? PassScore);
