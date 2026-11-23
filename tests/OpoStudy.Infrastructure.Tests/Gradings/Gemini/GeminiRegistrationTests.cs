using System.Net;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

using OpoStudy.Domain.PracticalCases;
using OpoStudy.Infrastructure.Gradings;
using OpoStudy.Infrastructure.Gradings.Gemini;

using Shouldly;

namespace OpoStudy.Infrastructure.Tests.Gradings.Gemini;

public sealed class GeminiRegistrationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ServiceCollection Services(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddGrading(configuration);
        return services;
    }

    private static Dictionary<string, string?> Gemini(params (string Key, string? Value)[] extra)
    {
        var settings = new Dictionary<string, string?> { ["Grading:Provider"] = "Gemini", ["Gemini:ApiKey"] = "test-key" };
        foreach (var (key, value) in extra)
        {
            settings[key] = value;
        }

        return settings;
    }

    [Fact]
    public void AddGrading_WithGeminiProvider_ResolvesTheGeminiCorrector()
    {
        using var provider = Services(Gemini()).BuildServiceProvider();

        provider.GetRequiredService<ILlmCorrector>().ShouldBeOfType<GeminiLlmCorrector>();
    }

    [Fact]
    public void AddGrading_WithFakeProvider_ResolvesTheFakeCorrector()
    {
        using var provider = Services(new() { ["Grading:Provider"] = "Fake" }).BuildServiceProvider();

        provider.GetRequiredService<ILlmCorrector>().ShouldBeOfType<FakeLlmCorrector>();
    }

    [Fact]
    public void AddGradingOptions_WithGeminiProvider_DoesNotRegisterGemini()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(Gemini()).Build();
        var services = new ServiceCollection().AddGradingOptions(configuration);

        services.Any(d => d.ServiceType == typeof(IConfigureOptions<GeminiOptions>)).ShouldBeFalse();
        services.Any(d => d.ServiceType == typeof(ILlmCorrector)).ShouldBeFalse();
    }

    [Fact]
    public async Task GeminiClient_WithStandardResilienceDefaults_CallsTheServerOnlyOnce()
    {
        var calls = 0;
        var services = Services(Gemini());
        services.ConfigureHttpClientDefaults(h => h.AddStandardResilienceHandler());
        services.AddHttpClient<ILlmCorrector, GeminiLlmCorrector>()
            .ConfigurePrimaryHttpMessageHandler(() => new FakeHttpHandler(_ =>
            {
                Interlocked.Increment(ref calls);
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }));
        using var provider = services.BuildServiceProvider();
        var corrector = provider.GetRequiredService<ILlmCorrector>();
        var request = new GradingRequest("c", [new GradingPartRequest("p", "m", [new GradingCriterion("A", 1)], "a")]);

        await Should.ThrowAsync<LlmTransientException>(() => corrector.GradeAsync(request, Ct));

        calls.ShouldBe(1);
    }

    [Fact]
    public void GeminiOptions_WithoutConfiguration_UseTheDefaults()
    {
        using var provider = Services(Gemini()).BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<GeminiOptions>>().Value;

        options.Model.ShouldBe("gemini-3.8-flash");
        options.ThinkingLevel.ShouldBe("LOW");
        options.MaxOutputTokens.ShouldBe(8192);
        options.BaseUrl.ShouldBe("https://generativelanguage.googleapis.com/v1beta/");
    }

    [Fact]
    public void GeminiOptions_WithoutApiKey_FailOnValidation()
    {
        using var provider = Services(new() { ["Grading:Provider"] = "Gemini" }).BuildServiceProvider();

        Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<GeminiOptions>>().Value);
    }

    [Theory]
    [InlineData("Gemini:Model", "Gemini/../x")]
    [InlineData("Gemini:Model", "")]
    [InlineData("Gemini:Model", "Gemini-Flash")]
    [InlineData("Gemini:ThinkingLevel", "EXTREME")]
    [InlineData("Gemini:MaxOutputTokens", "100")]
    [InlineData("Gemini:MaxOutputTokens", "100000")]
    [InlineData("Gemini:BaseUrl", "http://generativelanguage.googleapis.com/v1beta/")]
    [InlineData("Gemini:BaseUrl", "https://generativelanguage.googleapis.com/v1beta")]
    [InlineData("Gemini:BaseUrl", "not a url")]
    public void GeminiOptions_WithInvalidValue_FailOnValidation(string key, string value)
    {
        using var provider = Services(Gemini((key, value))).BuildServiceProvider();

        Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<GeminiOptions>>().Value);
    }
}