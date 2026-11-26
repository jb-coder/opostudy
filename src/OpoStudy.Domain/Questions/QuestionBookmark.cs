using OpoStudy.Domain.Common;

namespace OpoStudy.Domain.Questions;

/// <summary>A question a user marked to review later. One per user and question.</summary>
public sealed class QuestionBookmark
{
    private QuestionBookmark()
    {
    }

    public Guid UserId { get; private set; }
    public Guid QuestionId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static QuestionBookmark Create(Guid userId, Guid questionId, DateTimeOffset now)
    {
        if (userId == Guid.Empty || questionId == Guid.Empty)
        {
            throw new DomainRuleException("question_bookmark.invalid_id", "La pregunta marcada necesita usuario y pregunta.");
        }

        return new QuestionBookmark
        {
            UserId = userId,
            QuestionId = questionId,
            CreatedAt = now.ToUniversalTime(),
        };
    }
}
