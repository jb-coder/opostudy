using OpoStudy.Contracts.Quizzes;

namespace OpoStudy.Mobile.Core.Offline;

/// <summary>Result of answering a question of a local quiz: the feedback plus the state of the quiz. No XP, it is only known after syncing.</summary>
public sealed record OfflineAnswerResult(
    AnswerFeedback Feedback,
    bool QuizFinished,
    int CorrectCount,
    int IncorrectCount,
    int AnsweredCount,
    int QuestionCount);