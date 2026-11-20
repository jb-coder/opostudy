using OpoStudy.Contracts.Progress;
using OpoStudy.Mobile.Core.ViewModels;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests;

public sealed class DailyGoalCardTests
{
    private static StreakResponse Streak(int goal = 10, int answered = 0, bool met = false, int current = 0, params DayActivityResponse[] days) =>
        new(goal, new DateOnly(2026, 11, 20), answered, met, current, current, days, 1, 2, 200, 0);

    private static LevelResponse LevelWith(int spendable) => new(1, 500, 0, 100, 0, spendable);

    [Fact]
    public void FreezesText_Stock_ShowsCountOfMax() =>
        DailyGoalCard.FreezesText(Streak()).ShouldBe("🛡️ Protectores: 1 de 2");

    [Fact]
    public void BuyFreezeButtonText_EnoughXp_ShowsPrice() =>
        DailyGoalCard.BuyFreezeButtonText(Streak(), LevelWith(200)).ShouldBe("Comprar protector · 200 XP");

    [Fact]
    public void BuyFreezeButtonText_AtMax_ExplainsWhyDisabled()
    {
        var full = Streak() with { StreakFreezes = 2 };

        DailyGoalCard.CanBuyFreeze(full, LevelWith(1000)).ShouldBeFalse();
        DailyGoalCard.BuyFreezeButtonText(full, LevelWith(1000)).ShouldBe("Tienes el máximo de protectores");
    }

    [Fact]
    public void BuyFreezeButtonText_NotEnoughXp_ExplainsWhyDisabled()
    {
        DailyGoalCard.CanBuyFreeze(Streak(), LevelWith(199)).ShouldBeFalse();
        DailyGoalCard.BuyFreezeButtonText(Streak(), LevelWith(199)).ShouldBe("Necesitas 200 XP disponibles");
    }

    [Fact]
    public void CanBuyFreeze_LevelNotLoaded_IsFalse() =>
        DailyGoalCard.CanBuyFreeze(Streak(), null).ShouldBeFalse();

    [Theory]
    [InlineData(0, "")]
    [InlineData(1, "🛡️ Un protector ha salvado tu racha")]
    [InlineData(2, "🛡️ 2 protectores han salvado tu racha")]
    public void FreezesUsedNotice_UsedNow_FormatsSingularPluralAndNone(int used, string expected) =>
        DailyGoalCard.FreezesUsedNotice(Streak() with { FreezesUsedNow = used }).ShouldBe(expected);

    [Fact]
    public void Dots_ProtectedDay_HasOwnStateAndText()
    {
        var days = new[]
        {
            new DayActivityResponse(new DateOnly(2026, 11, 18), 3, 10, false, true),
            new DayActivityResponse(new DateOnly(2026, 11, 19), 10, 10, true, false),
            new DayActivityResponse(new DateOnly(2026, 11, 20), 0, 10, false, false),
        };

        var dots = DailyGoalCard.Dots(Streak(days: days));

        dots[0].IsProtected.ShouldBeTrue();
        dots[0].IsMet.ShouldBeFalse();
        dots[0].AccessibilityText.ShouldBe("X: día protegido");
        dots[1].IsProtected.ShouldBeFalse();
        dots[2].AccessibilityText.ShouldBe("V: objetivo no cumplido");
    }

    [Theory]
    [InlineData(0, "Sin racha")]
    [InlineData(1, "🔥 1 día")]
    [InlineData(5, "🔥 5 días")]
    public void StreakText_Days_FormatsSingularPluralAndNone(int days, string expected) =>
        DailyGoalCard.StreakText(days).ShouldBe(expected);

    [Fact]
    public void ProgressText_GoalMet_ShowsCompletedMessage() =>
        DailyGoalCard.ProgressText(Streak(answered: 10, met: true)).ShouldBe("¡Objetivo de hoy cumplido!");

    [Fact]
    public void ProgressText_GoalNotMet_ShowsCount() =>
        DailyGoalCard.ProgressText(Streak(answered: 7)).ShouldBe("7 de 10 preguntas");

    [Fact]
    public void ProgressFraction_AnsweredBeyondGoal_IsCappedAtOne() =>
        DailyGoalCard.ProgressFraction(Streak(answered: 14, met: true)).ShouldBe(1);

    [Fact]
    public void KeepStreakText_AliveAndNotMet_ShowsRemaining() =>
        DailyGoalCard.KeepStreakText(Streak(answered: 7, current: 4)).ShouldBe("Responde 3 preguntas más para mantener tu racha");

    [Fact]
    public void KeepStreakText_OneRemaining_UsesSingular() =>
        DailyGoalCard.KeepStreakText(Streak(answered: 9, current: 4)).ShouldBe("Responde 1 pregunta más para mantener tu racha");

    [Fact]
    public void KeepStreakText_NoStreak_IsEmpty() =>
        DailyGoalCard.KeepStreakText(Streak(answered: 2, current: 0)).ShouldBeEmpty();

    [Fact]
    public void KeepStreakText_GoalMet_IsEmpty() =>
        DailyGoalCard.KeepStreakText(Streak(answered: 10, met: true, current: 4)).ShouldBeEmpty();

    [Fact]
    public void Dots_LastSevenDays_UseSpanishInitialsAndMetState()
    {
        // 2026-11-14 is a Saturday.
        var days = Enumerable.Range(0, 7)
            .Select(i => new DayActivityResponse(new DateOnly(2026, 11, 14).AddDays(i), i, 10, i % 2 == 0, false))
            .ToArray();

        var dots = DailyGoalCard.Dots(Streak(days: days));

        dots.Select(d => d.Initial).ShouldBe(["S", "D", "L", "M", "X", "J", "V"]);
        dots.Select(d => d.IsMet).ShouldBe([true, false, true, false, true, false, true]);
        dots[0].AccessibilityText.ShouldBe("S: objetivo cumplido");
    }

    [Theory]
    [InlineData(1, "🎯 ¡Objetivo diario cumplido! Racha: 1 día")]
    [InlineData(6, "🎯 ¡Objetivo diario cumplido! Racha: 6 días")]
    public void GoalMetNotice_Streak_FormatsSingularAndPlural(int streak, string expected) =>
        DailyGoalCard.GoalMetNotice(streak).ShouldBe(expected);
}