using System.Collections.ObjectModel;
using System.Net;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Extensions.Logging;

using OpoStudy.Contracts.Gradings;
using OpoStudy.Contracts.PracticalCases;
using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.Navigation;
using OpoStudy.Mobile.Core.Offline;
using OpoStudy.Mobile.Core.PracticalCases;
using OpoStudy.Mobile.Core.Services;

using Refit;

namespace OpoStudy.Mobile.Core.ViewModels;

/// <summary>A part of the practical case with the answer being written.</summary>
public sealed partial class PracticalCasePartItem(PracticalCasePartResponse part, Action? onAnswerChanged = null) : ObservableObject
{
    public int Order => part.Order;

    public string Title => PracticalCaseTexts.PartTitle(part.Order, part.MaxPoints);

    public string Statement => part.Statement;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CharacterCountText))]
    [NotifyPropertyChangedFor(nameof(IsNearLimit))]
    [NotifyPropertyChangedFor(nameof(IsNotNearLimit))]
    [NotifyPropertyChangedFor(nameof(HasContent))]
    public partial string Answer { get; set; } = string.Empty;

    public string CharacterCountText => IsNearLimit
        ? PracticalCaseTexts.CharacterCountNearLimit(Answer.Length)
        : PracticalCaseTexts.CharacterCount(Answer.Length);

    public bool IsNearLimit => Answer.Length >= PracticalCaseLimits.NearLimitLength;

    public bool IsNotNearLimit => !IsNearLimit;

    /// <summary>Whether the answer has anything besides white space.</summary>
    public bool HasContent => !string.IsNullOrWhiteSpace(Answer);

    partial void OnAnswerChanged(string value) => onAnswerChanged?.Invoke();
}

/// <summary>A practical case: statement, one editor per part, local draft and submission for grading.</summary>
public sealed partial class PracticalCaseViewModel(
    IOpoStudyApi api,
    INavigationService navigation,
    IDialogService dialogs,
    IPracticalCaseDraftStore drafts,
    OfflineState offlineState,
    TimeProvider timeProvider,
    ILogger<PracticalCaseViewModel> logger) : OfflineAwareViewModel(offlineState), IQueryReceiver
{
    private const string AlreadyPendingCode = "grading.already_pending";

    private const string QuotaExceededCode = "grading.quota_exceeded";

    private const string MissingCaseMessage = "No se ha podido abrir el supuesto. Vuelve atrás e inténtalo de nuevo.";

    private Guid _practicalCaseId;

    private bool _loaded;

    private bool _restoringDraft;

    /// <summary>The answers were sent (or the case no longer exists): the draft must not be written again.</summary>
    private bool _draftClosed;

    private bool _submitBlocked;

    private CancellationTokenSource? _saveCancellation;

    private GradingQuotaResponse? _quota;

    public ObservableCollection<PracticalCasePartItem> Parts { get; } = [];

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Header { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Statement { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsLoaded { get; set; }

    /// <summary>There is no connection and the case could not be loaded.</summary>
    [ObservableProperty]
    public partial bool NeedsConnection { get; set; }

    [ObservableProperty]
    public partial string? DraftNotice { get; set; }

    [ObservableProperty]
    public partial string? SavedNotice { get; set; }

    [ObservableProperty]
    public partial bool HasQuota { get; set; }

    [ObservableProperty]
    public partial string QuotaSummary { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? QuotaMessage { get; set; }

    /// <summary>The case could not be loaded and there is a message to show with "Reintentar".</summary>
    public bool HasLoadError => HasError && !IsLoaded;

    public bool HasDraftNotice => !string.IsNullOrEmpty(DraftNotice);

    public bool HasSavedNotice => !string.IsNullOrEmpty(SavedNotice);

    public bool HasQuotaMessage => !string.IsNullOrEmpty(QuotaMessage);

    public void ApplyQuery(IReadOnlyDictionary<string, object> query)
    {
        if (QueryValues.ReadGuid(query, QueryKeys.PracticalCaseId) is { } id)
        {
            _practicalCaseId = id;
        }
    }

    public override async Task OnAppearingAsync()
    {
        ListenToOfflineState();

        // Only the first time: coming back to the page neither reloads nor overwrites what was written.
        if (_loaded)
        {
            return;
        }

        if (OfflineState.IsOffline)
        {
            NeedsConnection = true;
            return;
        }

        if (LoadCommand.CanExecute(null))
        {
            await LoadCommand.ExecuteAsync(null);
        }
    }

    public override void OnDisappearing()
    {
        CancelPendingSave();
        SaveDraftNow();
        base.OnDisappearing();
    }

    partial void OnIsLoadedChanged(bool value) => OnPropertyChanged(nameof(HasLoadError));

    protected override void OnErrorMessageChanged() => OnPropertyChanged(nameof(HasLoadError));

    partial void OnDraftNoticeChanged(string? value) => OnPropertyChanged(nameof(HasDraftNotice));

    partial void OnSavedNoticeChanged(string? value) => OnPropertyChanged(nameof(HasSavedNotice));

    partial void OnQuotaMessageChanged(string? value) => OnPropertyChanged(nameof(HasQuotaMessage));

    protected override void OnBusyChanged()
    {
        LoadCommand.NotifyCanExecuteChanged();
        SubmitCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        if (_practicalCaseId == Guid.Empty)
        {
            ErrorMessage = MissingCaseMessage;
            return;
        }

        IsBusy = true;
        try
        {
            // The quota never throws: a failure only hides it.
            var quota = TryLoadQuotaAsync(cancellationToken);
            PracticalCaseResponse practicalCase;
            try
            {
                practicalCase = await api.GetPracticalCaseAsync(_practicalCaseId, cancellationToken);
            }
            finally
            {
                await quota;
            }

            ShowCase(practicalCase);
            ShowQuota(await quota);
            NeedsConnection = false;
            IsLoaded = true;
            _loaded = true;
        }
        catch (Exception ex) when (ConnectivityErrors.IsConnectivityError(ex, cancellationToken))
        {
            LogLoadFailed(ex, ex.GetType().FullName);
            NeedsConnection = true;
        }
        catch (OperationCanceledException)
        {
            // The page went away before the case arrived.
        }
        catch (Exception ex)
        {
            LogLoadFailed(ex, ex.GetType().FullName);
            NeedsConnection = false;
            ErrorMessage = ApiErrorMessages.From(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<GradingQuotaResponse?> TryLoadQuotaAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await api.GetGradingQuotaAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogQuotaFailed(ex, ex.GetType().FullName);
            return null;
        }
    }

    private void ShowCase(PracticalCaseResponse practicalCase)
    {
        Title = practicalCase.Title;
        Header = PracticalCaseTexts.CaseHeader(practicalCase.EstimatedMinutes, practicalCase.MaxPoints);
        Statement = practicalCase.Statement;

        _restoringDraft = true;
        try
        {
            Parts.Clear();
            foreach (var part in practicalCase.Parts.OrderBy(p => p.Order))
            {
                Parts.Add(new PracticalCasePartItem(part, OnAnswerChanged));
            }

            RestoreDraft();
        }
        finally
        {
            _restoringDraft = false;
        }

        SubmitCommand.NotifyCanExecuteChanged();
    }

    private void RestoreDraft()
    {
        var draft = drafts.Find(_practicalCaseId);
        if (draft is null)
        {
            return;
        }

        if (draft.Answers.Count != Parts.Count)
        {
            // The content of the case changed since the draft was written.
            drafts.Delete(_practicalCaseId);
            return;
        }

        for (var i = 0; i < Parts.Count; i++)
        {
            Parts[i].Answer = draft.Answers[i] ?? string.Empty;
        }

        DraftNotice = PracticalCaseTexts.DraftRecovered(draft.SavedAt);
    }

    private void ShowQuota(GradingQuotaResponse? quota)
    {
        _quota = quota;
        HasQuota = quota is not null;
        QuotaSummary = quota is null ? string.Empty : PracticalCaseTexts.QuotaShort(quota);
        QuotaMessage = quota is { Remaining: <= 0 } ? PracticalCaseTexts.Quota(quota) : null;
        SubmitCommand.NotifyCanExecuteChanged();
    }

    private void OnAnswerChanged()
    {
        SubmitCommand.NotifyCanExecuteChanged();
        if (!_restoringDraft)
        {
            ScheduleSave();
        }
    }

    /// <summary>Saves the draft once the user stops typing for <see cref="PracticalCaseLimits.DraftSaveDelay"/>.</summary>
    private void ScheduleSave()
    {
        CancelPendingSave();
        var cancellation = new CancellationTokenSource();
        _saveCancellation = cancellation;
        _ = SaveAfterDelayAsync(cancellation);
    }

    private async Task SaveAfterDelayAsync(CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(PracticalCaseLimits.DraftSaveDelay, timeProvider, cancellation.Token);
            SaveDraftNow();
        }
        catch (OperationCanceledException)
        {
            // A newer change (or leaving the page) replaced this save.
        }
        finally
        {
            if (ReferenceEquals(_saveCancellation, cancellation))
            {
                _saveCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private void CancelPendingSave()
    {
        var cancellation = _saveCancellation;
        _saveCancellation = null;
        cancellation?.Cancel();
    }

    /// <summary>Writes the draft, or deletes it when every answer is blank. It never throws.</summary>
    private void SaveDraftNow()
    {
        if (_draftClosed || Parts.Count == 0)
        {
            return;
        }

        try
        {
            if (Parts.All(p => !p.HasContent))
            {
                drafts.Delete(_practicalCaseId);
                SavedNotice = null;
                return;
            }

            drafts.Save(_practicalCaseId, new PracticalCaseDraft([.. Parts.Select(p => p.Answer)], timeProvider.GetUtcNow()));
            SavedNotice = PracticalCaseTexts.Saved;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogDraftFailed(ex, ex.GetType().FullName);
        }
    }

    private bool CanSubmit() =>
        IsNotBusy && !_submitBlocked && Parts.Any(p => p.HasContent) && _quota is not { Remaining: <= 0 };

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task SubmitAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var blankParts = Parts.Count(p => !p.HasContent);
            var confirmed = await dialogs.ConfirmAsync(
                "¿Enviar a corregir?",
                PracticalCaseTexts.ConfirmSubmitMessage(_quota, blankParts),
                "Enviar",
                "Seguir escribiendo");
            if (!confirmed)
            {
                return;
            }

            // Whatever happens next, the text is safe on the device.
            CancelPendingSave();
            SaveDraftNow();

            var request = new SubmitGradingRequest([.. Parts.Select(p => p.Answer)]);
            var grading = await api.SubmitGradingAsync(_practicalCaseId, request, cancellationToken);
            OfflineState.ReportSuccess();

            _draftClosed = true;
            CancelPendingSave();
            drafts.Delete(_practicalCaseId);

            // Replaces this page in the stack so "Back" from the status goes to the list.
            await navigation.GoToAsync(
                "../" + Routes.Grading,
                new Dictionary<string, object> { [QueryKeys.GradingId] = grading.Id });
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Conflict && ApiErrorMessages.ProblemCode(ex) == AlreadyPendingCode)
        {
            LogSubmitFailed(ex, ex.GetType().FullName);
            await OfferPendingGradingAsync(ex, cancellationToken);
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.UnprocessableEntity && ApiErrorMessages.ProblemCode(ex) == QuotaExceededCode)
        {
            LogSubmitFailed(ex, ex.GetType().FullName);
            ErrorMessage = ApiErrorMessages.From(ex);
            ShowQuota(await TryLoadQuotaAsync(cancellationToken));
        }
        catch (Exception ex) when (ConnectivityErrors.IsConnectivityError(ex, cancellationToken))
        {
            LogSubmitFailed(ex, ex.GetType().FullName);
            ErrorMessage = PracticalCaseTexts.SubmitOffline;
        }
        catch (OperationCanceledException)
        {
            // The page went away while sending; the draft is already saved.
        }
        catch (Exception ex)
        {
            LogSubmitFailed(ex, ex.GetType().FullName);
            ErrorMessage = ApiErrorMessages.From(ex);
            if (ex is ApiException { StatusCode: HttpStatusCode.NotFound })
            {
                _submitBlocked = true;
            }
        }
        finally
        {
            IsBusy = false;
            SubmitCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>A grading is already running: offers to open its status. Without that information only the server message is shown.</summary>
    private async Task OfferPendingGradingAsync(ApiException conflict, CancellationToken cancellationToken)
    {
        GradingSummaryResponse? pending = null;
        try
        {
            var latest = await api.GetGradingsAsync(0, 1, cancellationToken);
            pending = latest.FirstOrDefault(g => g.Status is GradingStatus.Pending or GradingStatus.Processing);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSubmitFailed(ex, ex.GetType().FullName);
        }

        if (pending is null)
        {
            ErrorMessage = ApiErrorMessages.From(conflict);
            return;
        }

        var open = await dialogs.ConfirmAsync(
            "Ya tienes una corrección en curso",
            "Espera a que termine para enviar otra.",
            "Ver estado",
            "Cerrar");
        if (open)
        {
            await navigation.GoToAsync(Routes.Grading, new Dictionary<string, object> { [QueryKeys.GradingId] = pending.Id });
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not load the practical case ({ExceptionType})")]
    private partial void LogLoadFailed(Exception exception, string? exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not load the grading quota ({ExceptionType})")]
    private partial void LogQuotaFailed(Exception exception, string? exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not submit the answers ({ExceptionType})")]
    private partial void LogSubmitFailed(Exception exception, string? exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not save the draft ({ExceptionType})")]
    private partial void LogDraftFailed(Exception exception, string? exceptionType);
}