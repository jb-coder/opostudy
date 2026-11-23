using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using OpoStudy.Infrastructure.Gradings;

using Shouldly;

namespace OpoStudy.Infrastructure.Tests;

public sealed class GradingOptionsTests
{
    private static IOptions<GradingOptions> Resolve(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var provider = new ServiceCollection().AddGradingOptions(configuration).BuildServiceProvider();
        return provider.GetRequiredService<IOptions<GradingOptions>>();
    }

    [Fact]
    public void Options_WithoutConfiguration_UseTheDefaults()
    {
        var options = Resolve([]).Value;

        options.Provider.ShouldBe("Fake");
        options.MonthlyQuota.ShouldBe(10);
        options.MaxAttempts.ShouldBe(3);
        options.RetryDelayAfter(1).ShouldBe(TimeSpan.FromMinutes(1));
        options.RetryDelayAfter(2).ShouldBe(TimeSpan.FromMinutes(5));
        options.RetryDelayAfter(7).ShouldBe(TimeSpan.FromMinutes(5));
        options.CorrectorTimeout.ShouldBe(TimeSpan.FromMinutes(2));
        options.LeaseTimeout.ShouldBe(TimeSpan.FromMinutes(10));
        options.PollInterval.ShouldBe(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Options_WithConfiguredRetryDelays_DoNotMixThemWithTheDefaults()
    {
        var options = Resolve(new()
        {
            ["Grading:RetryDelays:0"] = "00:00:10",
            ["Grading:RetryDelays:1"] = "00:00:20",
        }).Value;

        options.RetryDelays.ShouldBe([TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20)]);
    }

    [Fact]
    public void Options_WithUnknownProvider_FailOnValidation()
    {
        var options = Resolve(new() { ["Grading:Provider"] = "OpenAI" });

        Should.Throw<OptionsValidationException>(() => options.Value);
    }

    [Fact]
    public void Options_WithGeminiProvider_AreValid()
    {
        var options = Resolve(new() { ["Grading:Provider"] = "gemini" });

        options.Value.Provider.ShouldBe("gemini");
    }

    [Fact]
    public void Options_WithLeaseNotGreaterThanTimeout_FailOnValidation()
    {
        var options = Resolve(new()
        {
            ["Grading:CorrectorTimeout"] = "00:05:00",
            ["Grading:LeaseTimeout"] = "00:05:00",
        });

        Should.Throw<OptionsValidationException>(() => options.Value);
    }
}