using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Extensions.Logging;

using OpoStudy.Contracts.Exams;
using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.Navigation;
using OpoStudy.Mobile.Core.Offline;

namespace OpoStudy.Mobile.Core.ViewModels;

/// <summary>An exam as shown in the list.</summary>
public sealed record ExamItem(ExamResponse Exam)
{
    public string Name => Exam.Name;

    public string Subtitle => $"{Exam.Organization} · Grupo {Exam.Group}";
}

/// <summary>Exam selection screen.</summary>
public sealed partial class ExamsViewModel(
    IOpoStudyApi api,
    INavigationService navigation,
    IOfflineContentStore offlineContent,
    OfflineState offlineState,
    ILogger<ExamsViewModel> logger) : OfflineAwareViewModel(offlineState)
{
    private bool _loaded;

    public ObservableCollection<ExamItem> Exams { get; } = [];

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    public override async Task OnAppearingAsync()
    {
        ListenToOfflineState();
        if (_loaded)
        {
            return;
        }

        if (RefreshCommand.CanExecute(null))
        {
            await RefreshCommand.ExecuteAsync(null);
        }
    }

    protected override void OnBusyChanged() => RefreshCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var exams = await api.GetExamsAsync(cancellationToken);
            OfflineState.ReportSuccess();
            Exams.Clear();
            foreach (var exam in exams)
            {
                Exams.Add(new ExamItem(exam));
            }

            _loaded = true;
            IsEmpty = Exams.Count == 0;
        }
        catch (Exception ex) when (ConnectivityErrors.IsConnectivityError(ex, cancellationToken))
        {
            LogLoadFailed(ex, ex.GetType().FullName);
            OfflineState.ReportConnectivityFailure();
            await ShowDownloadedExamsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            LogLoadFailed(ex, ex.GetType().FullName);
            IsEmpty = false;
            ErrorMessage = ApiErrorMessages.From(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Without connection: the exams whose content was downloaded. Not marked as loaded, so the next visit tries the server again.</summary>
    private async Task ShowDownloadedExamsAsync(CancellationToken cancellationToken)
    {
        Exams.Clear();
        IsEmpty = false;
        try
        {
            foreach (var exam in await offlineContent.ListExamsAsync(cancellationToken))
            {
                Exams.Add(new ExamItem(exam));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogLoadFailed(ex, ex.GetType().FullName);
        }

        if (Exams.Count == 0)
        {
            ErrorMessage = OfflineQuizException.NoContentDownloaded;
        }
    }

    [RelayCommand]
    private Task SelectExamAsync(ExamResponse exam)
    {
        ArgumentNullException.ThrowIfNull(exam);

        return navigation.GoToAsync(
            Routes.Topics,
            new Dictionary<string, object> { [QueryKeys.ExamId] = exam.Id, [QueryKeys.ExamName] = exam.Name });
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not load the exams ({ExceptionType})")]
    private partial void LogLoadFailed(Exception exception, string? exceptionType);
}