using OpoStudy.Domain.Common;

namespace OpoStudy.Domain.Quizzes;

/// <summary>
/// When a user has to review a question again (Leitner boxes). The interval doubles with each box:
/// <c>2^Box</c> days, from box 0 (1 day) to <see cref="MaxBox"/> (64 days).
/// </summary>
public sealed class ReviewSchedule
{
    public const int MaxBox = 6;

    private ReviewSchedule()
    {
    }

    public Guid UserId { get; private set; }
    public Guid QuestionId { get; private set; }

    /// <summary>Leitner box, from 0 to <see cref="MaxBox"/>.</summary>
    public int Box { get; private set; }

    /// <summary>First day (in the user's time zone) the question is due for review.</summary>
    public DateOnly DueOn { get; private set; }

    /// <summary>Time (UTC) of the answer that last changed the schedule or was the latest seen.</summary>
    public DateTimeOffset LastAnsweredAt { get; private set; }

    public static int IntervalDays(int box) => 1 << box;

    /// <summary>First answer of the question: box 1 if correct, box 0 if not.</summary>
    public static ReviewSchedule Start(Guid userId, Guid questionId, bool isCorrect, DateOnly day, DateTimeOffset answeredAt)
    {
        CheckIds(userId, questionId);

        var box = isCorrect ? 1 : 0;
        return new ReviewSchedule
        {
            UserId = userId,
            QuestionId = questionId,
            Box = box,
            DueOn = day.AddDays(IntervalDays(box)),
            LastAnsweredAt = answeredAt.ToUniversalTime(),
        };
    }

    /// <summary>
    /// Applies an answer given on <paramref name="day"/>. A wrong answer goes back to box 0; a correct one while due moves
    /// up a box; a correct one before its date changes nothing, and neither does an answer older than the last one.
    /// Returns whether anything changed.
    /// </summary>
    public bool Register(bool isCorrect, DateOnly day, DateTimeOffset answeredAt)
    {
        answeredAt = answeredAt.ToUniversalTime();
        if (answeredAt < LastAnsweredAt)
        {
            return false;
        }

        if (isCorrect)
        {
            if (!IsDueOn(day))
            {
                return false;
            }

            Box = Math.Min(Box + 1, MaxBox);
        }
        else
        {
            Box = 0;
        }

        DueOn = day.AddDays(IntervalDays(Box));
        LastAnsweredAt = answeredAt;
        return true;
    }

    public bool IsDueOn(DateOnly today) => DueOn <= today;

    private static void CheckIds(Guid userId, Guid questionId)
    {
        if (userId == Guid.Empty || questionId == Guid.Empty)
        {
            throw new DomainRuleException("review_schedule.invalid_id", "La programación de repaso necesita usuario y pregunta.");
        }
    }
}
