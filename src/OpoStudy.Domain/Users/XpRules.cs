namespace OpoStudy.Domain.Users;

/// <summary>Pure rules of experience points (XP) and levels. The level is never stored: it derives from the total XP.</summary>
public static class XpRules
{
    public const int CorrectAnswer = 10;
    public const int IncorrectAnswer = 2;
    public const int GoalMetBonus = 50;
    public const int StreakBonusPerDay = 5;
    public const int MaxStreakBonus = 50;

    /// <summary>XP of a practical case with the maximum score (equivalent to 10 correct answers).</summary>
    public const int PracticalCaseMaxXp = 100;

    /// <summary>XP price of one streak freeze.</summary>
    public const int StreakFreezePrice = 200;

    /// <summary>Going from level N to N+1 costs <c>LevelStep * N</c> XP.</summary>
    public const int LevelStep = 100;

    public static int ForAnswer(bool isCorrect) => isCorrect ? CorrectAnswer : IncorrectAnswer;

    /// <summary>XP of a corrected practical case: proportional to the score, rounded away from zero and kept in [0, <see cref="PracticalCaseMaxXp"/>].</summary>
    public static int ForPracticalCase(decimal score, int maxPoints)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxPoints, 0);
        ArgumentOutOfRangeException.ThrowIfNegative(score);
        var xp = (int)Math.Round(PracticalCaseMaxXp * score / maxPoints, MidpointRounding.AwayFromZero);
        return Math.Clamp(xp, 0, PracticalCaseMaxXp);
    }

    /// <summary>Bonus for meeting the daily goal; <paramref name="currentStreak"/> is the streak after meeting today.</summary>
    public static int ForGoalMet(int currentStreak)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(currentStreak, 1);
        return GoalMetBonus + (int)Math.Min((long)StreakBonusPerDay * currentStreak, MaxStreakBonus);
    }

    /// <summary>Total XP needed to reach <paramref name="level"/>: <c>50 * L * (L - 1)</c>.</summary>
    public static int XpToReach(int level)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);
        return checked(LevelStep / 2 * level * (level - 1));
    }

    /// <summary>The highest level whose threshold is not above <paramref name="totalXp"/>.</summary>
    public static int LevelFor(int totalXp)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(totalXp);

        // Integer-only: start from the real root and correct it, so there are no floating-point errors at the edges.
        var level = Math.Max(1, (int)((1 + Math.Sqrt(1 + (4.0 * totalXp / (LevelStep / 2)))) / 2));
        while (XpToReachLong(level + 1) <= totalXp)
        {
            level++;
        }

        while (level > 1 && XpToReachLong(level) > totalXp)
        {
            level--;
        }

        return level;
    }

    private static long XpToReachLong(int level) => (long)(LevelStep / 2) * level * (level - 1);
}
