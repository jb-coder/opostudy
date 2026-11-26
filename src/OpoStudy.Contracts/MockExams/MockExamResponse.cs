namespace OpoStudy.Contracts.MockExams;

/// <summary>A mock exam just created: its rules and its questions, without answers.</summary>
public sealed record MockExamResponse(
    Guid Id,
    Guid ExamId,
    int ExerciseNumber,
    string Title,
    DateTimeOffset StartedAt,
    DateTimeOffset DeadlineAt,
    int DurationMinutes,
    decimal MaxScore,
    decimal PenaltyRatio,
    decimal PassScore,
    IReadOnlyList<MockExamQuestionResponse> Questions);
