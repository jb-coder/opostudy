using OpoStudy.Contracts.Progress;
using OpoStudy.Mobile.Core.ViewModels;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests;

public sealed class LevelCardTests
{
    private static readonly LevelResponse Level4 = new(4, 830, 230, 400, 120, 830);

    [Fact]
    public void Texts_LevelWithXpToday_FormatsCard()
    {
        LevelCard.TitleText(Level4).ShouldBe("Nivel 4");
        LevelCard.DetailText(Level4).ShouldBe("230 / 400 XP para el nivel 5");
        LevelCard.TodayText(Level4).ShouldBe("Hoy: +120 XP");
        LevelCard.ProgressFraction(Level4).ShouldBe(0.575);
    }

    [Fact]
    public void TodayText_NoXpToday_SaysNothingEarnedYet() =>
        LevelCard.TodayText(Level4 with { XpToday = 0 }).ShouldBe("Hoy aún no has ganado XP");

    [Fact]
    public void SpendableText_XpSpent_ShowsAvailable() =>
        LevelCard.SpendableText(Level4 with { SpendableXp = 630 }).ShouldBe("Disponible: 630 XP");

    [Fact]
    public void SpendableText_NothingSpent_IsEmpty() =>
        LevelCard.SpendableText(Level4).ShouldBeEmpty();

    [Fact]
    public void ProgressFraction_NewUser_IsZero() =>
        LevelCard.ProgressFraction(new LevelResponse(1, 0, 0, 100, 0, 0)).ShouldBe(0);

    [Fact]
    public void AnswerXpText_WithBonus_ExcludesTheBonus() =>
        LevelCard.AnswerXpText(new XpGainResponse(65, 55, 100, 2, true)).ShouldBe("+10 XP");

    [Fact]
    public void AnswerXpText_IncorrectAnswer_ShowsTwo() =>
        LevelCard.AnswerXpText(new XpGainResponse(2, 0, 2, 1, false)).ShouldBe("+2 XP");

    [Fact]
    public void QuizTotalText_Total_FormatsSummaryLine() =>
        LevelCard.QuizTotalText(87).ShouldBe("+87 XP en este test");

    [Fact]
    public void Notice_NothingHappened_IsEmpty() =>
        LevelCard.Notice(new DailyProgressResponse(3, 10, false, 1), new XpGainResponse(10, 0, 30, 1, false)).ShouldBeEmpty();

    [Fact]
    public void Notice_LevelUpOnly_ShowsLevelLine() =>
        LevelCard.Notice(new DailyProgressResponse(3, 10, false, 1), new XpGainResponse(10, 0, 100, 2, true))
            .ShouldBe("⭐ ¡Has subido al nivel 2!");

    [Fact]
    public void Notice_GoalMetAndLevelUp_ShowsSingleNoticeWithTwoLines() =>
        LevelCard.Notice(new DailyProgressResponse(10, 10, true, 1), new XpGainResponse(65, 55, 120, 2, true))
            .ShouldBe("🎯 ¡Objetivo diario cumplido! Racha: 1 día · +55 XP\n⭐ ¡Has subido al nivel 2!");
}
