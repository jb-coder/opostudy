using Microsoft.Extensions.Logging;

using OpoStudy.Mobile.Core.Auth;
using OpoStudy.Mobile.Core.Navigation;
using OpoStudy.Mobile.Views;

namespace OpoStudy.Mobile;

public partial class AppShell : Shell
{
    private readonly IAuthService _auth;
    private readonly INavigationService _navigation;
    private readonly ILogger<AppShell> _logger;

    public AppShell(IAuthService auth, INavigationService navigation, ILogger<AppShell> logger)
    {
        _auth = auth;
        _navigation = navigation;
        _logger = logger;

        InitializeComponent();

        Routing.RegisterRoute(Routes.Register, typeof(RegisterPage));
        Routing.RegisterRoute(Routes.ForgotPassword, typeof(ForgotPasswordPage));
#if DEBUG
        Routing.RegisterRoute(Routes.StyleGuide, typeof(StyleGuidePage));
#endif

        _auth.SessionExpired += OnSessionExpired;
    }

    private void OnSessionExpired(object? sender, EventArgs e) => _ = GoToLoginAsync();

    private async Task GoToLoginAsync()
    {
        try
        {
            await _navigation.GoToAsync(Routes.Root(Routes.Login));
        }
        catch (Exception ex)
        {
            LogNavigationFailed(_logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not navigate to login after the session expired")]
    private static partial void LogNavigationFailed(ILogger logger, Exception exception);
}