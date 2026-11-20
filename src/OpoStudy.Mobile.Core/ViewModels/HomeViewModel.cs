using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Extensions.Logging;

using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.Auth;
using OpoStudy.Mobile.Core.Navigation;
using OpoStudy.Mobile.Core.Services;

namespace OpoStudy.Mobile.Core.ViewModels;

/// <summary>Home screen: API connection status and the user account card.</summary>
public sealed partial class HomeViewModel(
    IOpoStudyApi api,
    INavigationService navigation,
    AppEnvironment environment,
    IAuthService auth,
    IDialogService dialogs,
    TimeProvider time,
    ILogger<HomeViewModel> logger) : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectionText))]
    public partial string? ApiVersion { get; set; }

    [ObservableProperty]
    public partial bool IsConnected { get; set; }

    [ObservableProperty]
    public partial string? AccountEmail { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MemberSinceText))]
    [NotifyPropertyChangedFor(nameof(HasMemberSince))]
    public partial string? MemberSince { get; set; }

    public bool HasMemberSince => MemberSince is not null;

    public string MemberSinceText => MemberSince is null ? string.Empty : $"Miembro desde {MemberSince}";

    private static readonly CultureInfo SpanishCulture = CultureInfo.GetCultureInfo("es-ES");

    /// <summary>Whether the style catalog button is shown (debug builds only).</summary>
    public bool IsStyleGuideVisible { get; } = environment.IsDebug;

    public string ConnectionText => ApiVersion is null ? string.Empty : $"Conectado · API {StripBuildMetadata(ApiVersion)}";

    public override async Task OnAppearingAsync()
    {
        if (ApiVersion is not null)
        {
            return;
        }

        if (RefreshCommand.CanExecute(null))
        {
            await RefreshCommand.ExecuteAsync(null);
        }
    }

    /// <summary>Drops the SemVer build metadata (everything from '+'), e.g. "1.0.0+abc123" becomes "1.0.0".</summary>
    private static string StripBuildMetadata(string version)
    {
        var index = version.IndexOf('+', StringComparison.Ordinal);
        return index < 0 ? version : version[..index];
    }

    protected override void OnBusyChanged()
    {
        RefreshCommand.NotifyCanExecuteChanged();
        DeleteAccountCommand.NotifyCanExecuteChanged();
        SignOutCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        ErrorMessage = null;
        AccountEmail = auth.Email;
        try
        {
            var response = await api.GetVersionAsync(cancellationToken);
            ApiVersion = response.Version;
            IsConnected = true;

            var me = await api.GetMeAsync(cancellationToken);
            MemberSince = TimeZoneInfo.ConvertTime(me.CreatedAt, time.LocalTimeZone).ToString("d 'de' MMMM 'de' yyyy", SpanishCulture);
        }
        catch (Exception ex)
        {
            LogLoadFailed(ex, ex.GetType().FullName);
            IsConnected = false;
            ErrorMessage = ApiErrorMessages.From(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task SignOutAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await auth.SignOutAsync();
            await navigation.GoToAsync(Routes.Root(Routes.Login));
        }
        catch (Exception ex)
        {
            LogSignOutFailed(ex, ex.GetType().FullName);
            ErrorMessage = ApiErrorMessages.From(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task DeleteAccountAsync(CancellationToken cancellationToken)
    {
        var confirmed = await dialogs.ConfirmAsync(
            "¿Borrar tu cuenta?",
            "Se borrarán tu cuenta y todo tu progreso. No se puede deshacer.",
            "Borrar",
            "Cancelar");
        if (!confirmed)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            // API data first, while the ID token is still valid; then the Firebase account.
            await api.DeleteMeAsync(cancellationToken);
            await auth.DeleteFirebaseAccountAsync(cancellationToken);
            await navigation.GoToAsync(Routes.Root(Routes.Login));
        }
        catch (FirebaseAuthException ex) when (ex.Code == "CREDENTIAL_TOO_OLD_LOGIN_AGAIN")
        {
            // The API data is already gone and the session was closed: go back to login and explain.
            await navigation.GoToAsync(Routes.Root(Routes.Login));
            await dialogs.AlertAsync("Borrar cuenta", AuthErrorMessages.From(ex.Code));
        }
        catch (Exception ex)
        {
            LogDeleteFailed(ex, ex.GetType().FullName);
            ErrorMessage = ApiErrorMessages.From(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task StartQuizAsync() => navigation.GoToAsync(Routes.Exams);

    [RelayCommand]
    private Task OpenStyleGuideAsync() => navigation.GoToAsync(Routes.StyleGuide);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not sign out ({ExceptionType})")]
    private partial void LogSignOutFailed(Exception exception, string? exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not delete the account ({ExceptionType})")]
    private partial void LogDeleteFailed(Exception exception, string? exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not load the API version ({ExceptionType})")]
    private partial void LogLoadFailed(Exception exception, string? exceptionType);
}