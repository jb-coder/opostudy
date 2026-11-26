using OpoStudy.Mobile.Core.MockExams;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests;

public sealed class MockExamTextsTests
{
    [Theory]
    [InlineData(0, "Tiempo agotado")]
    [InlineData(45, "Queda menos de un minuto")]
    [InlineData(60, "Queda 1 minuto")]
    [InlineData(61, "Queda 1 minuto y 1 segundo")]
    [InlineData(72, "Queda 1 minuto y 12 segundos")]
    [InlineData(120, "Quedan 2 minutos")]
    [InlineData(1410, "Quedan 23 minutos y 30 segundos")]
    public void RemainingDescription_GivenRemainingSeconds_ReadsExactTime(int seconds, string expected) =>
        MockExamTexts.RemainingDescription(TimeSpan.FromSeconds(seconds)).ShouldBe(expected);

    [Theory]
    [InlineData(1, 1, "Netas: 1 − 1 × 0,5 = 0,5")]
    [InlineData(0, 2, "Netas: 0 − 2 × 0,5 = −1")]
    public void NetCalculation_GivenCounts_UsesMinusSign(int correct, int incorrect, string expected) =>
        MockExamTexts.NetCalculation(correct, incorrect, 0.5m).ShouldBe(expected);
}
