using OpoStudy.Contracts.Progress;
using OpoStudy.Mobile.Core.ViewModels;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests;

public sealed class DailyGoalCardTests
{
    private static StreakResponse Streak(int goal = 10, int answered = 0, bool met = false, int current = 0, params DayActivityResponse[] days) =>
        new(goal, new DateOnly(2026, 11, 20), answered, met, current, current, days);

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
            .Select(i => new DayActivityResponse(new DateOnly(2026, 11, 14).AddDays(i), i, 10, i % 2 == 0))
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