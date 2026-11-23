using OpoStudy.Domain.Common;

namespace OpoStudy.Domain.Users;

/// <summary>Questions a user answered on one day (in their time zone) and the goal in force that day.</summary>
public sealed class DailyActivity
{
    private DailyActivity()
    {
    }

    public Guid UserId { get; private set; }
    public DateOnly Date { get; private set; }
    public int AnsweredCount { get; private set; }

    /// <summary>Daily goal in force that day.</summary>
    public int Goal { get; private set; }

    public DateTimeOffset? GoalMetAt { get; private set; }

    /// <summary>XP earned that day, bonus included.</summary>
    public int XpEarned { get; private set; }

    /// <summary>When a streak freeze covered this day.</summary>
    public DateTimeOffset? FreezeUsedAt { get; private set; }

    public bool IsGoalMet => GoalMetAt is not null;

    public bool IsProtected => FreezeUsedAt is not null;

    public static DailyActivity Start(Guid userId, DateOnly date, int goal)
    {
        CheckGoal(goal);
        return new DailyActivity { UserId = userId, Date = date, Goal = goal };
    }

    /// <summary>Counts an answer. Returns <c>true</c> only for the answer that meets the goal.</summary>
    public bool RegisterAnswer(DateTimeOffset now)
    {
        AnsweredCount++;
        return TryMeetGoal(now);
    }

    /// <summary>Meets the goal without counting an answer (e.g. a corrected practical case). Returns <c>true</c> only if the day was not met yet.</summary>
    public bool MeetGoal(DateTimeOffset now)
    {
        if (IsGoalMet)
        {
            return false;
        }

        GoalMetAt = now.ToUniversalTime();
        return true;
    }

    /// <summary>Changes the goal of the day. A day already met stays untouched. Returns <c>true</c> if the new goal leaves the day met.</summary>
    public bool ChangeGoal(int goal, DateTimeOffset now)
    {
        if (IsGoalMet)
        {
            return false;
        }

        CheckGoal(goal);
        Goal = goal;
        return TryMeetGoal(now);
    }

    /// <summary>Covers the day with a streak freeze. The day must be neither met nor already protected.</summary>
    public void Protect(DateTimeOffset now)
    {
        if (IsGoalMet || IsProtected)
        {
            throw new InvalidOperationException("The day is already met or protected.");
        }

        FreezeUsedAt = now.ToUniversalTime();
    }

    public void AddXp(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        XpEarned = checked(XpEarned + amount);
    }

    private bool TryMeetGoal(DateTimeOffset now)
    {
        if (IsGoalMet || AnsweredCount < Goal)
        {
            return false;
        }

        GoalMetAt = now.ToUniversalTime();
        return true;
    }

    private static void CheckGoal(int goal)
    {
        if (goal is < Limits.MinDailyGoal or > Limits.MaxDailyGoal)
        {
            throw new DomainRuleException(
                "user.invalid_daily_goal",
                $"El objetivo diario debe estar entre {Limits.MinDailyGoal} y {Limits.MaxDailyGoal} preguntas.");
        }
    }
}