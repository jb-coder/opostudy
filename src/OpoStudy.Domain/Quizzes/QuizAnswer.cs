namespace OpoStudy.Domain.Quizzes;

/// <summary>A question of a <see cref="QuizAttempt"/> and what the user answered. Only modified through the attempt.</summary>
public sealed class QuizAnswer
{
    private QuizAnswer()
    {
    }

    public Guid QuizAttemptId { get; private set; }
    public Guid QuestionId { get; private set; }
    public int Order { get; private set; }
    public Guid? SelectedOptionId { get; private set; }
    public bool? IsCorrect { get; private set; }
    public DateTimeOffset? AnsweredAt { get; private set; }

    public bool IsAnswered => SelectedOptionId is not null;

    internal static QuizAnswer Create(Guid quizAttemptId, Guid questionId, int order) => new()
    {
        QuizAttemptId = quizAttemptId,
        QuestionId = questionId,
        Order = order,
    };

    internal void Record(Guid optionId, bool isCorrect, DateTimeOffset now)
    {
        SelectedOptionId = optionId;
        IsCorrect = isCorrect;
        AnsweredAt = now;
    }
}