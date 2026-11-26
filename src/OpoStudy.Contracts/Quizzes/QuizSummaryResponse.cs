namespace OpoStudy.Contracts.Quizzes;

/// <summary>Summary of a quiz for the history list. <paramref name="Mode"/> is <c>"ByTopic"</c>, <c>"Random"</c>, <c>"Review"</c>, <c>"SpacedReview"</c> or <c>"Bookmarks"</c>.</summary>
public sealed record QuizSummaryResponse(
    Guid Id,
    Guid ExamId,
    Guid? TopicId,
    string? TopicTitle,
    string Mode,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    int QuestionCount,
    int CorrectCount,
    int IncorrectCount);