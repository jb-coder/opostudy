using Microsoft.Extensions.Logging;

using OpoStudy.Mobile.Core.Navigation;
using OpoStudy.Mobile.Core.Services;

namespace OpoStudy.Mobile.Core.Auth;

/// <summary>When the session is closed by Firebase, sends the user to login and explains why.</summary>
public sealed partial class SessionExpiredHandler(
    IAuthService auth,
    INavigationService navigation,
    IDialogService dialogs,
    ILogger<SessionExpiredHandler> logger)
{
    private bool _started;

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        auth.SessionExpired += OnSessionExpired;
    }

    public async Task HandleAsync(string code)
    {
        try
        {
            await navigation.GoToAsync(Routes.Root(Routes.Login));
            await dialogs.AlertAsync("Sesión cerrada", AuthErrorMessages.From(code));
        }
        catch (Exception ex)
        {
            LogHandleFailed(ex, ex.GetType().FullName);
        }
    }

    private void OnSessionExpired(object? sender, SessionExpiredEventArgs e) => _ = HandleAsync(e.Code);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not show the expired-session notice ({ExceptionType})")]
    private partial void LogHandleFailed(Exception exception, string? exceptionType);
}