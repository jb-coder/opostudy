namespace OpoStudy.Contracts.Quizzes;

/// <summary>A quiz with its questions. <paramref name="Mode"/> is <c>"ByTopic"</c>, <c>"Random"</c> or <c>"Review"</c>.</summary>
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