using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Extensions.Logging;

using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.Navigation;

namespace OpoStudy.Mobile.Core.ViewModels;

/// <summary>Home screen: checks the connection with the API.</summary>
public sealed partial class HomeViewModel(
    IOpoStudyApi api,
    INavigationService navigation,
    AppEnvironment environment,
    ILogger<HomeViewModel> logger) : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectionText))]
    public partial string? ApiVersion { get; set; }

    [ObservableProperty]
    public partial bool IsConnected { get; set; }

    /// <summary>Whether the style catalog button is shown (debug builds only).</summary>
    public bool IsStyleGuideVisible { get; } = environment.IsDebug;

    public string ConnectionText => ApiVersion is null ? string.Empty : $"Conectado · API {StripBuildMetadata(ApiVersion)}";

    public override async Task OnAppearingAsync()
    {
        if (ApiVersion is not null)
        {
            return;
        }

        await RefreshAsync();
    }

    /// <summary>Drops the SemVer build metadata (everything from '+'), e.g. "1.0.0+abc123" becomes "1.0.0".</summary>
    private static string StripBuildMetadata(string version)
    {
        var index = version.IndexOf('+', StringComparison.Ordinal);
        return index < 0 ? version : version[..index];
    }

    protected override void OnBusyChanged() => RefreshCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await api.GetVersionAsync();
            ApiVersion = response.Version;
            IsConnected = true;
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

    [RelayCommand]
    private Task OpenStyleGuideAsync() => navigation.GoToAsync(Routes.StyleGuide);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not load the API version ({ExceptionType})")]
    private partial void LogLoadFailed(Exception exception, string? exceptionType);
}
