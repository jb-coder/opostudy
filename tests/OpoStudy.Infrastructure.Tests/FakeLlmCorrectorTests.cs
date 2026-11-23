using OpoStudy.Domain.PracticalCases;
using OpoStudy.Infrastructure.Gradings;

using Shouldly;

namespace OpoStudy.Infrastructure.Tests;

public sealed class FakeLlmCorrectorTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static GradingRequest Request(params string[] answers) =>
        new(
            "Case statement",
            [.. answers.Select(a => new GradingPartRequest("Part", "Model", [new GradingCriterion("A", 2), new GradingCriterion("B", 3)], a))]);

    [Fact]
    public async Task GradeAsync_WithBlankAnswer_AwardsZero()
    {
        var outcome = await new FakeLlmCorrector().GradeAsync(Request("   "), Ct);

        outcome.Parts.Single().Awarded.ShouldBe([0m, 0m]);
    }

    [Fact]
    public async Task GradeAsync_WithLongAnswer_AwardsTheMaximum()
    {
        var outcome = await new FakeLlmCorrector().GradeAsync(Request(new string('x', 300), new string('x', 5000)), Ct);

        outcome.Parts.Count.ShouldBe(2);
        outcome.Parts[0].Awarded.ShouldBe([2m, 3m]);
        outcome.Parts[1].Awarded.ShouldBe([2m, 3m]);
    }

    [Fact]
    public async Task GradeAsync_WithPartialAnswer_RoundsDownToHalfPoints()
    {
        // 150 characters = 0.5 of the maximum: 2 -> 1, 3 -> 1.5. 90 characters = 0.3: 2 -> floor(1.2) / 2 = 0.5, 3 -> floor(1.8) / 2 = 0.5.
        var half = await new FakeLlmCorrector().GradeAsync(Request(new string('x', 150)), Ct);
        var third = await new FakeLlmCorrector().GradeAsync(Request(new string('x', 90)), Ct);

        half.Parts.Single().Awarded.ShouldBe([1m, 1.5m]);
        third.Parts.Single().Awarded.ShouldBe([0.5m, 0.5m]);
    }

    [Fact]
    public async Task GradeAsync_IgnoresWhitespaceWhenCounting()
    {
        var outcome = await new FakeLlmCorrector().GradeAsync(Request(string.Concat(Enumerable.Repeat("x ", 300))), Ct);

        outcome.Parts.Single().Awarded.ShouldBe([2m, 3m]);
    }

    [Fact]
    public async Task GradeAsync_WithSameRequest_ReturnsSameOutcome()
    {
        var corrector = new FakeLlmCorrector();

        var first = await corrector.GradeAsync(Request("some answer", "another"), Ct);
        var second = await corrector.GradeAsync(Request("some answer", "another"), Ct);

        first.Parts.Select(p => p.Awarded).ShouldBe(second.Parts.Select(p => p.Awarded));
        first.Feedback.ShouldBe(second.Feedback);
        first.Corrector.ShouldBe("fake");
        first.InputTokens.ShouldBeNull();
        first.OutputTokens.ShouldBeNull();
        first.Feedback.ShouldStartWith("Corrección de prueba (sin LLM)");
    }
}