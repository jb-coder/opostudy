using System.Globalization;

using OpoStudy.Contracts.Progress;

namespace OpoStudy.Mobile.Core.ViewModels;

/// <summary>One of the last days shown as a dot on the daily goal card.</summary>
public sealed record DayDot(string Initial, bool IsMet, bool IsProtected, string AccessibilityText);

/// <summary>Texts and dots of the daily goal card and the quiz notice. Pure formatting, no state.</summary>
public static class DailyGoalCard
{
    private const string Initials = "LMXJVSD";

    public static string ProgressText(StreakResponse streak)
    {
        ArgumentNullException.ThrowIfNull(streak);
        return streak.GoalMetToday
            ? "¡Objetivo de hoy cumplido!"
            : string.Create(CultureInfo.InvariantCulture, $"{streak.AnsweredToday} de {streak.DailyGoal} preguntas");
    }

    public static double ProgressFraction(StreakResponse streak)
    {
        ArgumentNullException.ThrowIfNull(streak);
        if (streak.GoalMetToday)
        {
            return 1;
        }

        return streak.DailyGoal <= 0 ? 0 : Math.Clamp((double)streak.AnsweredToday / streak.DailyGoal, 0, 1);
    }

    public static string StreakText(int days) => days switch
    {
        <= 0 => "Sin racha",
        1 => "🔥 1 día",
        _ => string.Create(CultureInfo.InvariantCulture, $"🔥 {days} días"),
    };

    public static string LongestText(int days) => string.Create(CultureInfo.InvariantCulture, $"Mejor racha: {days}");

    /// <summary>Hint to keep the streak alive; empty when the streak is not alive or today is already met.</summary>
    public static string KeepStreakText(StreakResponse streak)
    {
        ArgumentNullException.ThrowIfNull(streak);
        if (streak.CurrentStreak <= 0 || streak.GoalMetToday)
        {
            return string.Empty;
        }

        var remaining = Math.Max(streak.DailyGoal - streak.AnsweredToday, 1);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"Responde {remaining} {(remaining == 1 ? "pregunta" : "preguntas")} más para mantener tu racha");
    }

    public static IReadOnlyList<DayDot> Dots(StreakResponse streak)
    {
        ArgumentNullException.ThrowIfNull(streak);
        return
        [
            .. streak.LastDays.Select(day =>
            {
                var initial = Initials[((int)day.Date.DayOfWeek + 6) % 7].ToString();
                var state = day.GoalMet ? "objetivo cumplido" : day.Protected ? "día protegido" : "objetivo no cumplido";
                return new DayDot(initial, day.GoalMet, day.Protected && !day.GoalMet, $"{initial}: {state}");
            }),
        ];
    }

    /// <summary>Streak freezes in stock, e.g. "🛡️ Protectores: 1 de 2".</summary>
    public static string FreezesText(StreakResponse streak)
    {
        ArgumentNullException.ThrowIfNull(streak);
        return string.Create(CultureInfo.InvariantCulture, $"🛡️ Protectores: {streak.StreakFreezes} de {streak.MaxStreakFreezes}");
    }

    /// <summary>Whether the buy button can be pressed: below the maximum and with enough spendable XP.</summary>
    public static bool CanBuyFreeze(StreakResponse streak, LevelResponse? level)
    {
        ArgumentNullException.ThrowIfNull(streak);
        return streak.StreakFreezes < streak.MaxStreakFreezes
            && level is not null
            && level.SpendableXp >= streak.StreakFreezePrice;
    }

    /// <summary>Text of the buy button, or the reason why it is disabled.</summary>
    public static string BuyFreezeButtonText(StreakResponse streak, LevelResponse? level)
    {
        ArgumentNullException.ThrowIfNull(streak);
        if (streak.StreakFreezes >= streak.MaxStreakFreezes)
        {
            return "Tienes el máximo de protectores";
        }

        var price = string.Create(CultureInfo.InvariantCulture, $"{streak.StreakFreezePrice} XP");
        return CanBuyFreeze(streak, level) ? $"Comprar protector · {price}" : $"Necesitas {price} disponibles";
    }

    public static string BuyFreezeConfirmText(StreakResponse streak)
    {
        ArgumentNullException.ThrowIfNull(streak);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"¿Comprar un protector de racha por {streak.StreakFreezePrice} XP? Se gastará solo si un día no cumples el objetivo.");
    }

    /// <summary>Non-blocking notice when freezes saved the streak in this response; empty if none were used.</summary>
    public static string FreezesUsedNotice(StreakResponse streak)
    {
        ArgumentNullException.ThrowIfNull(streak);
        return streak.FreezesUsedNow switch
        {
            <= 0 => string.Empty,
            1 => "🛡️ Un protector ha salvado tu racha",
            var n => string.Create(CultureInfo.InvariantCulture, $"🛡️ {n} protectores han salvado tu racha"),
        };
    }

    public static string GoalMetNotice(int currentStreak) =>
        string.Create(CultureInfo.InvariantCulture, $"🎯 ¡Objetivo diario cumplido! Racha: {currentStreak} {(currentStreak == 1 ? "día" : "días")}");

    public static string GoalMetNotice(int currentStreak, int bonusXp) =>
        bonusXp <= 0
            ? GoalMetNotice(currentStreak)
            : string.Create(CultureInfo.InvariantCulture, $"{GoalMetNotice(currentStreak)} · +{bonusXp} XP");
}
