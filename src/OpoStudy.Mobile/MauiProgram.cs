using System.Reflection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using OpoStudy.Mobile.Core;
using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.Auth;
using OpoStudy.Mobile.Core.Navigation;
using OpoStudy.Mobile.Core.Services;
using OpoStudy.Mobile.Core.ViewModels;
using OpoStudy.Mobile.Services;
using OpoStudy.Mobile.Views;

using Refit;

namespace OpoStudy.Mobile;

public static class MauiProgram
{
    private static readonly TimeSpan ApiTimeout = TimeSpan.FromSeconds(15);

    private const string GoogleServicesResource = "google-services.json";

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

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(LoadFirebaseOptions());
        services.AddSingleton<ISecureStore, SecureStore>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IAuthService, AuthService>();
        services.AddTransient<AuthHeaderHandler>();
#if ANDROID
        services.AddSingleton<IGoogleSignInProvider, Platforms.Android.GoogleSignInProvider>();
#endif
        services.AddHttpClient<IFirebaseAuthClient, FirebaseAuthClient>(client => client.Timeout = ApiTimeout)
#if ANDROID
            .AddHttpMessageHandler(() => new JavaNetworkExceptionHandler())
#endif
            ;

        services.AddRefitGeneratedClient<IOpoStudyApi>(ApiJsonContext.Default)
            .ConfigureHttpClient((sp, client) =>
            {
                client.BaseAddress = sp.GetRequiredService<ApiOptions>().BaseAddress;
                client.Timeout = ApiTimeout;
            })
            .AddHttpMessageHandler<AuthHeaderHandler>()
#if ANDROID
            .AddHttpMessageHandler(() => new JavaNetworkExceptionHandler())
#endif
            ;

        services.AddSingleton<INavigationService, ShellNavigationService>();

        services.AddSingleton<AppShell>();

        services.AddTransient<StartupViewModel>();
        services.AddTransient<StartupPage>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<LoginPage>();
        services.AddTransient<RegisterViewModel>();
        services.AddTransient<RegisterPage>();
        services.AddTransient<ForgotPasswordViewModel>();
        services.AddTransient<ForgotPasswordPage>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<HomePage>();
#if DEBUG
        services.AddTransient<StyleGuidePage>();
#endif

        return builder.Build();
    }

    /// <summary>Reads the embedded <c>google-services.json</c>; without it the app runs but sign-in reports NOT_CONFIGURED.</summary>
    private static FirebaseOptions LoadFirebaseOptions()
    {
        using var stream = typeof(MauiProgram).Assembly.GetManifestResourceStream(GoogleServicesResource);
        if (stream is null)
        {
            return FirebaseOptions.Empty;
        }

        try
        {
            return GoogleServicesConfig.Parse(stream, AppInfo.PackageName);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Text.Json.JsonException)
        {
            System.Diagnostics.Debug.WriteLine($"google-services.json is not usable: {ex.Message}");
            return FirebaseOptions.Empty;
        }
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