namespace OpoStudy.Contracts.MockExams;

/// <summary>An exercise of the exam (format of a mock exam) and how many active questions it can be drawn from.</summary>
public sealed record ExamExerciseResponse(
    int Number,
    string Title,
    int QuestionCount,
    int DurationMinutes,
    decimal MaxScore,
    decimal PenaltyRatio,
    decimal DefaultPassScore,
    int AvailableQuestionCount);
