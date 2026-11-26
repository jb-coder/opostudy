using OpoStudy.Domain.Quizzes;

using Shouldly;

namespace OpoStudy.Domain.Tests;

public sealed class MockExamScoringTests
{
    [Fact]
    public void Calculate_WithSpecExample_ReturnsRoundedScore()
    {
        var score = MockExamScoring.Calculate(40, 10, 55, 20m, 0.5m);

        score.ShouldBe(12.73m);
    }

    [Fact]
    public void Calculate_WithNegativeNetCorrect_ReturnsZero()
    {
        var score = MockExamScoring.Calculate(10, 30, 55, 20m, 0.5m);

        score.ShouldBe(0m);
    }

    [Fact]
    public void Calculate_WithAllBlank_ReturnsZero()
    {
        MockExamScoring.Calculate(0, 0, 55, 20m, 0.5m).ShouldBe(0m);
    }

    [Fact]
    public void Calculate_WithAllCorrect_ReturnsMaxScore()
    {
        MockExamScoring.Calculate(55, 0, 55, 20m, 0.5m).ShouldBe(20m);
    }

    [Fact]
    public void Calculate_WithMidpoint_RoundsAwayFromZero()
    {
        // 1 * 1 / 8 = 0.125 -> 0.13
        MockExamScoring.Calculate(1, 0, 8, 1m, 0.5m).ShouldBe(0.13m);
    }

    [Fact]
    public void Calculate_WithoutPenalty_WrongAnswersDoNotSubtract()
    {
        MockExamScoring.Calculate(5, 5, 10, 10m, 0m).ShouldBe(5m);
    }
}