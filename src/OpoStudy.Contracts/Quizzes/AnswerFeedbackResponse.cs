namespace OpoStudy.Contracts.Quizzes;

/// <summary>Feedback for the answered question plus the state of the quiz afterwards.</summary>
public sealed record AnswerFeedbackResponse(
    Guid QuestionId,
    AnswerFeedback Feedback,
    bool QuizFinished,
    int CorrectCount,
    int IncorrectCount,
    int AnsweredCount,
    int QuestionCount);