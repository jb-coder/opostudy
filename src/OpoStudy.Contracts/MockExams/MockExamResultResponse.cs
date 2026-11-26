namespace OpoStudy.Contracts.MockExams;

/// <summary>Result of a submitted mock exam: score, counts and correction of every question.</summary>
public sealed record MockExamResultResponse(
    Guid Id,
    int ExerciseNumber,
    string Title,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    bool SubmittedLate,
    int QuestionCount,
    int CorrectCount,
    int IncorrectCount,
    int BlankCount,
    decimal PenaltyRatio,
    decimal Score,
    decimal MaxScore,
    decimal PassScore,
    bool Passed,
    IReadOnlyList<MockExamResultItem> Items);
