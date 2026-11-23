namespace OpoStudy.Domain.Quizzes;

public enum QuizMode
{
    ByTopic,
    Random,

    /// <summary>Review of the questions the user currently has wrong; the topic is optional.</summary>
    Review,
}