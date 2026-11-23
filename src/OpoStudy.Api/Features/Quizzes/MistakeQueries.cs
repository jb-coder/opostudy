using OpoStudy.Domain.Quizzes;
using OpoStudy.Infrastructure.Persistence;

namespace OpoStudy.Api.Features.Quizzes;

/// <summary>A question the user currently has wrong, with its topic.</summary>
internal sealed class PendingMistake
{
    public required Guid QuestionId { get; init; }

    public required Guid TopicId { get; init; }
}

internal static class MistakeQueries
{
    /// <summary>
    /// Active questions whose most recent answer by <paramref name="userId"/> is incorrect, whatever the quiz.
    /// "Most recent" is by <c>AnsweredAt</c>; on a tie, the answer of the greater quiz id (UUID v7) wins.
    /// Translated entirely to SQL.
    /// </summary>
    public static IQueryable<PendingMistake> PendingMistakes(OpoStudyDbContext db, Guid userId)
    {
        var answered =
            from x in db.Set<QuizAnswer>()
            join a in db.QuizAttempts on x.QuizAttemptId equals a.Id
            where a.UserId == userId && x.IsCorrect != null
            select new { QuizId = a.Id, x.QuestionId, x.IsCorrect, x.AnsweredAt };

        return answered
            .Where(x => x.IsCorrect == false)
            .Where(x => !answered.Any(y =>
                y.QuestionId == x.QuestionId
                && (y.AnsweredAt > x.AnsweredAt
                    || (y.AnsweredAt == x.AnsweredAt && y.QuizId.ToString().CompareTo(x.QuizId.ToString()) > 0))))
            .Join(
                db.Questions.Where(q => q.IsActive),
                x => x.QuestionId,
                q => q.Id,
                (x, q) => new PendingMistake { QuestionId = q.Id, TopicId = q.TopicId });
    }
}
