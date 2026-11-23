using System.Net;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Extensions.Logging;

using OpoStudy.Contracts.Gradings;
using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.Navigation;
using OpoStudy.Mobile.Core.Offline;
using OpoStudy.Mobile.Core.PracticalCases;
using OpoStudy.Mobile.Core.Services;

using Refit;

namespace OpoStudy.Mobile.Core.ViewModels;

/// <summary>Status of a grading: polls the server while the page is visible until it is completed or failed.</summary>
public sealed partial class GradingViewModel(
    IOpoStudyApi api,
    INavigationService navigation,
    IDialogService dialogs,
    IPracticalCaseDraftStore drafts,
    TimeProvider timeProvider,
    ILogger<GradingViewModel> logger) : ViewModelBase, IQueryReceiver
{
    /// <summary>Wait before the second query.</summary>
    internal static readonly TimeSpan FirstDelay = TimeSpan.FromSeconds(3);

    internal static readonly TimeSpan FastInterval = TimeSpan.FromSeconds(5);

    internal static readonly TimeSpan SlowInterval = TimeSpan.FromSeconds(15);

    /// <summary>The fast interval is used during this time after the page appeared.</summary>
    internal static readonly TimeSpan FastPhase = TimeSpan.FromMinutes(3);

    private Guid _gradingId;

    private GradingResponse? _grading;

    /// <summary>The grading ended (or does not exist): nothing left to poll.</summary>
    private bool _finished;

    /// <summary>A polling session is starting or running; set synchronously so a second appearing cannot start another one.</summary>
    private bool _loopActive;

    private CancellationToken _loopToken;

    public bool IsInProgress { get; private set; } = true;

    [ObservableProperty]
    public partial string CaseTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SubmittedText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? DetailText { get; set; }

    [ObservableProperty]
    public partial string? ScoreText { get; set; }

    /// <summary>Shown while the server cannot be reached; the polling keeps trying.</summary>
    [ObservableProperty]
    public partial string? ConnectionNotice { get; set; }

    [ObservableProperty]
    public partial bool IsCompleted { get; set; }

    [ObservableProperty]
    public partial bool IsFailed { get; set; }

    public bool HasDetail => !string.IsNullOrEmpty(DetailText);

    public bool HasScore => !string.IsNullOrEmpty(ScoreText);

    public bool HasConnectionNotice => !string.IsNullOrEmpty(ConnectionNotice);

    public bool ShowInProgressHint => IsInProgress && _grading is not null;

    public void ApplyQuery(IReadOnlyDictionary<string, object> query)
    {
        if (QueryValues.ReadGuid(query, QueryKeys.GradingId) is { } id)
        {
            _gradingId = id;
        }
    }

    public override async Task OnAppearingAsync()
    {
        if (_gradingId == Guid.Empty || _finished)
        {
            return;
        }

        // A single loop at a time: a second appearing without disappearing in between does nothing.
        if (_loopActive && !_loopToken.IsCancellationRequested)
        {
            return;
        }

        var token = PageToken;
        _loopToken = token;
        _loopActive = true;
        var handedOver = false;
        try
        {
            var started = timeProvider.GetTimestamp();
            var first = await PollOnceAsync(alertOnFinish: false, token);
            if (first == PollResult.Stop || token.IsCancellationRequested)
            {
                return;
            }

            _ = RunLoopAsync(first, started, token);
            handedOver = true;
        }
        finally
        {
            if (!handedOver)
            {
                EndLoop(token);
            }
        }
    }

    private void EndLoop(CancellationToken token)
    {
        if (_loopToken == token)
        {
            _loopActive = false;
        }
    }

    private async Task RunLoopAsync(PollResult previous, long started, CancellationToken token)
    {
        var delay = previous == PollResult.Transient ? SlowInterval : FirstDelay;
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(delay, timeProvider, token);
                var result = await PollOnceAsync(alertOnFinish: true, token);
                if (result == PollResult.Stop)
                {
                    return;
                }

                delay = result == PollResult.Transient || timeProvider.GetElapsedTime(started) >= FastPhase
                    ? SlowInterval
                    : FastInterval;
            }
        }
        catch (OperationCanceledException)
        {
            // The page went away.
        }
        catch (Exception ex)
        {
            // The loop runs unobserved: it must never fail silently.
            LogPollingFailed(ex, ex.GetType().FullName);
        }
        finally
        {
            EndLoop(token);
        }
    }

    private async Task<PollResult> PollOnceAsync(bool alertOnFinish, CancellationToken token)
    {
        GradingResponse grading;
        try
        {
            grading = await api.GetGradingAsync(_gradingId, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return PollResult.Stop;
        }
        catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
        {
            // Not found or not allowed: there is nothing to wait for.
            LogPollFailed(ex, ex.GetType().FullName);
            _finished = true;
            ConnectionNotice = null;
            ErrorMessage = ApiErrorMessages.From(ex);
            return PollResult.Stop;
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            // The expired session is handled elsewhere (sign-in again); just stop asking.
            LogPollFailed(ex, ex.GetType().FullName);
            _finished = true;
            ConnectionNotice = null;
            return PollResult.Stop;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !token.IsCancellationRequested)
        {
            // No connection, timeout, 5xx or other transient 4xx (408, 429...): keep trying, but slowly.
            LogPollFailed(ex, ex.GetType().FullName);
            ConnectionNotice = PracticalCaseTexts.PollOffline;
            return PollResult.Transient;
        }

        if (token.IsCancellationRequested)
        {
            return PollResult.Stop;
        }

        ConnectionNotice = null;
        ErrorMessage = null;
        Apply(grading);
        if (grading.Status is not (GradingStatus.Completed or GradingStatus.Failed))
        {
            return PollResult.Continue;
        }

        _finished = true;
        if (alertOnFinish)
        {
            await AlertFinishedAsync(grading);
        }

        return PollResult.Stop;
    }

    private void Apply(GradingResponse grading)
    {
        _grading = grading;
        CaseTitle = grading.CaseTitle;
        SubmittedText = PracticalCaseTexts.Submitted(grading.SubmittedAt);
        IsCompleted = grading.Status == GradingStatus.Completed;
        IsFailed = grading.Status == GradingStatus.Failed;
        IsInProgress = !IsCompleted && !IsFailed;
        OnPropertyChanged(nameof(IsInProgress));
        OnPropertyChanged(nameof(ShowInProgressHint));

        ScoreText = IsCompleted ? PracticalCaseTexts.Score(grading.Score, grading.MaxPoints) : null;
        DetailText = IsCompleted ? PracticalCaseTexts.CompletedDetailLater : null;
        StatusText = grading.Status switch
        {
            GradingStatus.Completed => PracticalCaseTexts.StatusCompleted,
            GradingStatus.Failed => PracticalCaseTexts.Failure(grading.FailureCode),
            GradingStatus.Processing => PracticalCaseTexts.StatusProcessing,
            _ when grading.Attempts > 0 && grading.Status == GradingStatus.Pending => PracticalCaseTexts.StatusRetrying,
            _ when grading.Status == GradingStatus.Pending => PracticalCaseTexts.StatusQueued,
            // Unknown status: treated as in progress.
            _ => PracticalCaseTexts.StatusProcessing,
        };
        RecoverAnswersCommand.NotifyCanExecuteChanged();
    }

    private Task AlertFinishedAsync(GradingResponse grading) =>
        grading.Status == GradingStatus.Completed
            ? dialogs.AlertAsync("Corrección lista", PracticalCaseTexts.ScoreAlert(grading.Score, grading.MaxPoints))
            : dialogs.AlertAsync("No se ha podido corregir", PracticalCaseTexts.Failure(grading.FailureCode));

    partial void OnDetailTextChanged(string? value) => OnPropertyChanged(nameof(HasDetail));

    partial void OnScoreTextChanged(string? value) => OnPropertyChanged(nameof(HasScore));

    partial void OnConnectionNoticeChanged(string? value) => OnPropertyChanged(nameof(HasConnectionNotice));

    [RelayCommand]
    private Task GoBackAsync() => navigation.GoToAsync(Routes.Back);

    private bool CanRecoverAnswers() => IsFailed && _grading is { Answers.Count: > 0 };

    /// <summary>Puts the answers of a failed grading back in the editor as a draft, so nothing has to be written again.</summary>
    [RelayCommand(CanExecute = nameof(CanRecoverAnswers))]
    private async Task RecoverAnswersAsync()
    {
        if (_grading is not { } grading)
        {
            return;
        }

        drafts.Save(
            grading.PracticalCaseId,
            new PracticalCaseDraft([.. grading.Answers.OrderBy(a => a.Order).Select(a => a.Text)], timeProvider.GetUtcNow()));

        // Replaces this page so "Back" from the editor goes to the list.
        await navigation.GoToAsync(
            "../" + Routes.PracticalCase,
            new Dictionary<string, object> { [QueryKeys.PracticalCaseId] = grading.PracticalCaseId });
    }

    private enum PollResult
    {
        Continue,
        Transient,
        Stop,
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not query the grading ({ExceptionType})")]
    private partial void LogPollFailed(Exception exception, string? exceptionType);

    [LoggerMessage(Level = LogLevel.Error, Message = "The grading polling stopped unexpectedly ({ExceptionType})")]
    private partial void LogPollingFailed(Exception exception, string? exceptionType);
}