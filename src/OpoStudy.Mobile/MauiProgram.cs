using System.Reflection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using OpoStudy.Mobile.Core;
using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.Navigation;
using OpoStudy.Mobile.Core.ViewModels;
using OpoStudy.Mobile.Services;
using OpoStudy.Mobile.Views;

using Refit;

namespace OpoStudy.Mobile;

public static class MauiProgram
{
    private static readonly TimeSpan ApiTimeout = TimeSpan.FromSeconds(15);

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
        const bool isDebug = true;
#else
        const bool isDebug = false;
#endif

        var services = builder.Services;

        services.AddSingleton(new AppEnvironment(isDebug));
        services.AddSingleton(new ApiOptions(ReadApiBaseAddress()));
        services.AddRefitGeneratedClient<IOpoStudyApi>(ApiJsonContext.Default)
            .ConfigureHttpClient((sp, client) =>
            {
                client.BaseAddress = sp.GetRequiredService<ApiOptions>().BaseAddress;
                client.Timeout = ApiTimeout;
            })
#if ANDROID
            .AddHttpMessageHandler(() => new JavaNetworkExceptionHandler())
#endif
            ;

        services.AddSingleton<INavigationService, ShellNavigationService>();

        services.AddTransient<HomeViewModel>();
        services.AddTransient<HomePage>();
#if DEBUG
        services.AddTransient<StyleGuidePage>();
#endif

        return builder.Build();
    }

    private static Uri ReadApiBaseAddress()
    {
        var value = typeof(MauiProgram).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "ApiBaseUrl")?.Value;

        if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException(
                "ApiBaseUrl is not configured. Build with -p:ApiBaseUrl=<url> (Release builds have no default).");
        }

        return uri;
    }
}