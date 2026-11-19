namespace OpoStudy.Contracts.Quizzes;

/// <summary>A quiz with its questions. <paramref name="Mode"/> is <c>"ByTopic"</c> or <c>"Random"</c>.</summary>
public sealed record QuizResponse(
    Guid Id,
    Guid ExamId,
    Guid? TopicId,
    string Mode,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    int CorrectCount,
    int IncorrectCount,
    IReadOnlyList<QuizQuestionResponse> Questions);