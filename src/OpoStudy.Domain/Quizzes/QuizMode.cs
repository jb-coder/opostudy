namespace OpoStudy.Domain.Quizzes;

public enum QuizMode
{
    ByTopic,
    Random,

    /// <summary>Review of the questions the user currently has wrong; the topic is optional.</summary>
    Review,

    /// <summary>Timed exercise with penalty for wrong answers; answers are submitted all at once.</summary>
    MockExam,

    /// <summary>Spaced review of the questions due today; the topic is optional.</summary>
    SpacedReview,
}