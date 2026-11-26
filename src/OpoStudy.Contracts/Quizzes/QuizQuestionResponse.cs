namespace OpoStudy.Contracts.Quizzes;

/// <summary>A question of a quiz; <paramref name="Feedback"/> is <c>null</c> until it is answered; <paramref name="IsBookmarked"/> tells whether the user bookmarked it.</summary>
public sealed record QuizQuestionResponse(
    Guid QuestionId,
    int Order,
    string Statement,
    IReadOnlyList<QuizOptionResponse> Options,
    AnswerFeedback? Feedback,
    bool IsBookmarked);