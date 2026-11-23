using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

using OpoStudy.Infrastructure.Gradings.Gemini;
using OpoStudy.Infrastructure.Progress;

namespace OpoStudy.Infrastructure.Gradings;

public static class GradingServiceCollectionExtensions
{
    /// <summary>Registers the <c>Grading</c> settings, validated when the host starts. Enough for the API, which only needs the quota.</summary>
    public static IServiceCollection AddGradingOptions(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<GradingOptions>, GradingOptionsValidator>());
        services.AddOptions<GradingOptions>()
            .Bind(configuration.GetSection(GradingOptions.SectionName))
            .ValidateOnStart();
        return services;
    }

    /// <summary>Registers everything the Worker needs: settings, the corrector chosen by <c>Grading:Provider</c> and the queue processor.</summary>
    public static IServiceCollection AddGrading(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddGradingOptions(configuration);
        services.TryAddSingleton(TimeProvider.System);

        // The validator guarantees the provider is a known one.
        var provider = configuration[$"{GradingOptions.SectionName}:Provider"];
        if (string.Equals(provider, GradingOptions.GeminiProvider, StringComparison.OrdinalIgnoreCase))
        {
            AddGemini(services, configuration);
        }
        else
        {
            services.TryAddSingleton<ILlmCorrector, FakeLlmCorrector>();
        }

        services.AddProgressTracking();
        services.TryAddScoped<GradingProcessor>();
        return services;
    }

    private static void AddGemini(IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<GeminiOptions>, GeminiOptionsValidator>());
        services.AddOptions<GeminiOptions>()
            .Bind(configuration.GetSection(GeminiOptions.SectionName))
            .ValidateOnStart();

#pragma warning disable EXTEXP0001 // Experimental API, but it is the only way to opt a client out of the ConfigureHttpClientDefaults handler; covered by a registration test.
        services.AddHttpClient<ILlmCorrector, GeminiLlmCorrector>((provider, client) =>
            {
                client.BaseAddress = new Uri(provider.GetRequiredService<IOptions<GeminiOptions>>().Value.BaseUrl);
                // The timeout is GradingProcessor's (CorrectorTimeout) and the retries are the queue's.
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            // ServiceDefaults adds the standard resilience handler to every HttpClient (10 s per try, retries): it would cut
            // long corrections and multiply the calls, which count against the free tier.
            .RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001
    }
}
