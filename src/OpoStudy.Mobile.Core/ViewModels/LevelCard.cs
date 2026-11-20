using System.Globalization;

using OpoStudy.Contracts.Progress;

namespace OpoStudy.Mobile.Core.ViewModels;

/// <summary>Texts of the level card, the XP label of an answer and the quiz notice. Pure formatting, no state.</summary>
public static class LevelCard
{
    public static string TitleText(LevelResponse level)
    {
        ArgumentNullException.ThrowIfNull(level);
        return string.Create(CultureInfo.InvariantCulture, $"Nivel {level.Level}");
    }

    public static double ProgressFraction(LevelResponse level)
    {
        ArgumentNullException.ThrowIfNull(level);
        return level.XpForNextLevel <= 0 ? 0 : Math.Clamp((double)level.XpIntoLevel / level.XpForNextLevel, 0, 1);
    }

    public static string DetailText(LevelResponse level)
    {
        ArgumentNullException.ThrowIfNull(level);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{level.XpIntoLevel} / {level.XpForNextLevel} XP para el nivel {level.Level + 1}");
    }

    public static string TodayText(LevelResponse level)
    {
        ArgumentNullException.ThrowIfNull(level);
        return level.XpToday <= 0
            ? "Hoy aún no has ganado XP"
            : string.Create(CultureInfo.InvariantCulture, $"Hoy: +{level.XpToday} XP");
    }

    /// <summary>XP label of one answer, without the daily-goal bonus (that one goes in the notice).</summary>
    public static string AnswerXpText(XpGainResponse xp)
    {
        ArgumentNullException.ThrowIfNull(xp);
        return string.Create(CultureInfo.InvariantCulture, $"+{xp.Gained - xp.Bonus} XP");
    }

    public static string QuizTotalText(int totalXp) => string.Create(CultureInfo.InvariantCulture, $"+{totalXp} XP en este test");

    /// <summary>
    /// Non-blocking notice after an answer: goal met (with its bonus) and/or level up, one line each.
    /// Empty when neither happened.
    /// </summary>
    public static string Notice(DailyProgressResponse? daily, XpGainResponse? xp)
    {
        var lines = new List<string>(2);
        if (daily is { GoalMetNow: true })
        {
            lines.Add(DailyGoalCard.GoalMetNotice(daily.CurrentStreak, xp?.Bonus ?? 0));
        }

        if (xp is { LeveledUp: true })
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"⭐ ¡Has subido al nivel {xp.Level}!"));
        }

        return string.Join('\n', lines);
    }
}
