using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

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

        // The validator guarantees the provider is a known one (only the fake one for now).
        services.TryAddSingleton<ILlmCorrector, FakeLlmCorrector>();
        services.TryAddScoped<GradingProcessor>();
        return services;
    }
}