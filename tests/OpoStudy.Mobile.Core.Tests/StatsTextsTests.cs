using OpoStudy.Contracts.Stats;
using OpoStudy.Mobile.Core.Stats;
using OpoStudy.Mobile.Core.ViewModels;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests;

public sealed class StatsTextsTests
{
    [Theory]
    [InlineData(18, 25, "72 %")]
    [InlineData(1, 8, "13 %")] // 12.5 rounds away from zero
    [InlineData(1, 3, "33 %")]
    [InlineData(2, 3, "67 %")]
    [InlineData(0, 5, "0 %")]
    [InlineData(5, 5, "100 %")]
    public void Percent_Answers_RoundsAwayFromZero(int correct, int answered, string expected) =>
        StatsTexts.Percent(correct, answered).ShouldBe(expected);

    [Fact]
    public void Percent_NoAnswers_IsDash() => StatsTexts.Percent(0, 0).ShouldBe("—");

    [Theory]
    [InlineData(5, 12, "5 de 12")]
    [InlineData(0, 0, "0 de 0")]
    public void TopicsPracticed_Counts_AreJoined(int practiced, int available, string expected) =>
        StatsTexts.TopicsPracticed(practiced, available).ShouldBe(expected);

    [Fact]
    public void TopicScore_Answers_ShowsPercentAndCounts() => StatsTexts.TopicScore(18, 25).ShouldBe("72 % · 18 de 25");

    [Fact]
    public void LastPractice_UtcInstant_ShownInTheGivenZone()
    {
        // 23:30 UTC on 12 November is already the 13th in Madrid (UTC+1).
        var at = new DateTimeOffset(2026, 11, 12, 23, 30, 0, TimeSpan.Zero);
        var madrid = TimeZoneInfo.CreateCustomTimeZone("Madrid", TimeSpan.FromHours(1), "Madrid", "Madrid");

        StatsTexts.LastPractice(at, TimeZoneInfo.Utc).ShouldBe("Última práctica: 12 nov 2026");
        StatsTexts.LastPractice(at, madrid).ShouldBe("Última práctica: 13 nov 2026");
    }

    [Theory]
    [InlineData(2026, 1, 5, "5 ene 2026")]
    [InlineData(2026, 9, 30, "30 sep 2026")]
    [InlineData(2026, 12, 1, "1 dic 2026")]
    public void ShortDate_Date_UsesSpanishAbbreviation(int year, int month, int day, string expected) =>
        StatsTexts.ShortDate(new DateOnly(year, month, day)).ShouldBe(expected);

    [Fact]
    public void LongDate_Date_HasCapitalizedWeekday() =>
        StatsTexts.LongDate(new DateOnly(2026, 11, 24)).ShouldBe("Martes 24 de noviembre");

    [Fact]
    public void MonthTitle_Month_IsCapitalized() => StatsTexts.MonthTitle(new DateOnly(2026, 11, 1)).ShouldBe("Noviembre de 2026");

    [Fact]
    public void DayMonth_Date_HasNoLeadingZeros() => StatsTexts.DayMonth(new DateOnly(2026, 3, 5)).ShouldBe("5/3");

    [Fact]
    public void ShowAllTopics_Count_IsInParentheses() => StatsTexts.ShowAllTopics(12).ShouldBe("Ver todos los temas (12)");

    [Theory]
    [InlineData(1, "1 intento")]
    [InlineData(2, "2 intentos")]
    public void Attempts_Count_UsesSingularOrPlural(int count, string expected) => StatsTexts.Attempts(count).ShouldBe(expected);

    [Theory]
    [InlineData(3, 74.5, "3 correcciones · media 74,5 %")]
    [InlineData(1, 80.0, "1 corrección · media 80 %")]
    [InlineData(2, 66.7, "2 correcciones · media 66,7 %")]
    public void GradingsSummary_Stats_UsesDecimalComma(int completed, double average, string expected) =>
        StatsTexts.GradingsSummary(Grading(completed, (decimal)average)).ShouldBe(expected);

    [Fact]
    public void GradingsSummary_NoGradings_OmitsTheAverage() =>
        StatsTexts.GradingsSummary(Grading(0, null)).ShouldBe("0 correcciones");

    [Theory]
    [InlineData(18.5, 25, 74.0, "18,5 / 25 · 74 %")]
    [InlineData(25, 25, 100.0, "25 / 25 · 100 %")]
    [InlineData(10, 30, 33.3, "10 / 30 · 33 %")]
    [InlineData(1, 8, 12.5, "1 / 8 · 13 %")]
    public void BestScore_Case_ShowsPointsAndWholePercent(double score, int max, double percent, string expected)
    {
        var best = new CaseBestScoreResponse(Guid.NewGuid(), "Caso", null, 2, Guid.NewGuid(), (decimal)score, max, (decimal)percent);

        StatsTexts.BestScore(best).ShouldBe(expected);
    }

    [Fact]
    public void ScoreBarItem_Score_HasFractionPercentAndDay()
    {
        var score = new CaseScoreResponse(Guid.NewGuid(), "Caso 1", new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero), 18.5m, 25, 74m);

        var bar = new ScoreBarItem(score, TimeZoneInfo.Utc);

        bar.Fraction.ShouldBe(0.74);
        bar.PercentText.ShouldBe("74 %");
        bar.DateText.ShouldBe("12/11");
        bar.AccessibilityText.ShouldBe("Caso 1, 12/11: 74 %");
    }

    [Fact]
    public void TopicStatItem_Topic_ExposesProgressAndTexts()
    {
        var topic = new TopicStatsResponse(Guid.NewGuid(), 3, "Tema 3. La Constitución", "Bloque I", 25, 18, new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero), 0);

        var item = new TopicStatItem(topic, TimeZoneInfo.Utc);

        item.Progress.ShouldBe(0.72);
        item.ScoreText.ShouldBe("72 % · 18 de 25");
        item.LastPracticeText.ShouldBe("Última práctica: 12 nov 2026");
        item.HasSection.ShouldBeTrue();
        item.HasQuestions.ShouldBeFalse();
    }

    private static PracticalCaseStatsResponse Grading(int completed, decimal? average) => new(completed, average, [], []);
}