using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Extensions.Logging;

using OpoStudy.Contracts.Gradings;
using OpoStudy.Contracts.PracticalCases;
using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.Navigation;
using OpoStudy.Mobile.Core.Offline;
using OpoStudy.Mobile.Core.PracticalCases;

namespace OpoStudy.Mobile.Core.ViewModels;

/// <summary>A practical case as shown in the list.</summary>
/// <param name="Case">The case.</param>
/// <param name="HasDraft">Whether there is a local draft for it.</param>
public sealed record PracticalCaseItem(PracticalCaseSummaryResponse Case, bool HasDraft)
{
    public Guid Id => Case.Id;

    public string Title => Case.Title;

    public string Details => PracticalCaseTexts.CaseDetails(Case.PartCount, Case.MaxPoints, Case.EstimatedMinutes);

    public string AccessibilityText => HasDraft ? $"{Title}. {Details}. {PracticalCaseTexts.DraftMark}" : $"{Title}. {Details}";
}

/// <summary>The cases of a block (<see cref="PracticalCaseSummaryResponse.Section"/>).</summary>
public sealed class PracticalCaseGroup(string title, IEnumerable<PracticalCaseItem> items) : List<PracticalCaseItem>(items)
{
    public string Title { get; } = title;
}

/// <summary>List of the practical cases of an exam, with the monthly grading quota.</summary>
public sealed partial class PracticalCasesViewModel(
    IOpoStudyApi api,
    INavigationService navigation,
    IPracticalCaseDraftStore drafts,
    OfflineState offlineState,
    ILogger<PracticalCasesViewModel> logger) : OfflineAwareViewModel(offlineState), IQueryReceiver
{
    private const string MissingExamMessage = "No se ha podido abrir la oposición. Vuelve atrás e inténtalo de nuevo.";

    private Guid _examId;

    private Guid? _pendingGradingId;

    public ObservableCollection<PracticalCaseGroup> Groups { get; } = [];

    [ObservableProperty]
    public partial string ExamName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    /// <summary>There is no connection: the list is not loaded and the "need connection" state is shown.</summary>
    [ObservableProperty]
    public partial bool NeedsConnection { get; set; }

    [ObservableProperty]
    public partial bool HasQuota { get; set; }

    [ObservableProperty]
    public partial string QuotaText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string QuotaResetText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsQuotaExhausted { get; set; }

    [ObservableProperty]
    public partial bool HasPendingGrading { get; set; }

    [ObservableProperty]
    public partial string PendingGradingText { get; set; } = string.Empty;

    public void ApplyQuery(IReadOnlyDictionary<string, object> query)
    {
        if (QueryValues.ReadGuid(query, QueryKeys.ExamId) is { } id)
        {
            _examId = id;
        }

        if (query.TryGetValue(QueryKeys.ExamName, out var name) && name is string examName)
        {
            ExamName = examName;
        }
    }

    public override async Task OnAppearingAsync()
    {
        ListenToOfflineState();

        // Reloaded every time: the draft marks and the grading in progress change while the user is elsewhere.
        if (OfflineState.IsOffline)
        {
            ShowNeedsConnection();
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
        ErrorMessage = null;
        if (_examId == Guid.Empty)
        {
            ErrorMessage = MissingExamMessage;
            return;
        }

        IsBusy = true;
        try
        {
            // The three requests run in parallel; quota and grading never throw.
            var quota = TryLoadAsync(api.GetGradingQuotaAsync, cancellationToken);
            var latest = TryLoadAsync(token => api.GetGradingsAsync(0, 1, token), cancellationToken);
            IReadOnlyList<PracticalCaseSummaryResponse> cases;
            try
            {
                cases = await api.GetPracticalCasesAsync(_examId, cancellationToken);
            }
            finally
            {
                await Task.WhenAll(quota, latest);
            }

            NeedsConnection = false;
            Rebuild(cases);
            ShowQuota(await quota);
            ShowPendingGrading(await latest);
        }
        catch (Exception ex) when (ConnectivityErrors.IsConnectivityError(ex, cancellationToken))
        {
            LogLoadFailed(ex, ex.GetType().FullName);
            ShowNeedsConnection();
        }
        catch (OperationCanceledException)
        {
            // The page went away before the data arrived.
        }
        catch (Exception ex)
        {
            LogLoadFailed(ex, ex.GetType().FullName);
            IsEmpty = false;
            NeedsConnection = false;
            ErrorMessage = ApiErrorMessages.From(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task OpenCaseAsync(PracticalCaseItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return navigation.GoToAsync(Routes.PracticalCase, new Dictionary<string, object> { [QueryKeys.PracticalCaseId] = item.Id });
    }

    [RelayCommand]
    private Task OpenPendingGradingAsync() =>
        _pendingGradingId is { } id
            ? navigation.GoToAsync(Routes.Grading, new Dictionary<string, object> { [QueryKeys.GradingId] = id })
            : Task.CompletedTask;

    private async Task<T?> TryLoadAsync<T>(Func<CancellationToken, Task<T>> load, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            return await load(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSecondaryLoadFailed(ex, ex.GetType().FullName);
            return null;
        }
    }

    private void ShowNeedsConnection()
    {
        Groups.Clear();
        IsEmpty = false;
        HasQuota = false;
        HasPendingGrading = false;
        _pendingGradingId = null;
        NeedsConnection = true;
    }

    private void Rebuild(IReadOnlyList<PracticalCaseSummaryResponse> cases)
    {
        Groups.Clear();
        IsEmpty = cases.Count == 0;

        // Blocks keep the order of their first appearance in the API response; cases without block go last.
        var named = new List<(string Title, List<PracticalCaseItem> Items)>();
        var others = new List<PracticalCaseItem>();
        foreach (var practicalCase in cases)
        {
            var item = new PracticalCaseItem(practicalCase, drafts.Find(practicalCase.Id) is not null);
            if (string.IsNullOrWhiteSpace(practicalCase.Section))
            {
                others.Add(item);
                continue;
            }

            var group = named.Find(g => g.Title == practicalCase.Section);
            if (group.Items is null)
            {
                group = (practicalCase.Section, []);
                named.Add(group);
            }

            group.Items.Add(item);
        }

        foreach (var (title, items) in named)
        {
            Groups.Add(new PracticalCaseGroup(title, items));
        }

        if (others.Count > 0)
        {
            Groups.Add(new PracticalCaseGroup(PracticalCaseTexts.OtherSection, others));
        }
    }

    private void ShowQuota(GradingQuotaResponse? quota)
    {
        HasQuota = quota is not null;
        if (quota is null)
        {
            return;
        }

        QuotaText = PracticalCaseTexts.Quota(quota);
        QuotaResetText = PracticalCaseTexts.QuotaResets(quota);
        IsQuotaExhausted = quota.Remaining <= 0;
    }

    private void ShowPendingGrading(IReadOnlyList<GradingSummaryResponse>? latest)
    {
        var pending = latest?.Count > 0 && latest[0] is { Status: GradingStatus.Pending or GradingStatus.Processing } grading
            ? grading
            : null;
        _pendingGradingId = pending?.Id;
        PendingGradingText = pending is null ? string.Empty : PracticalCaseTexts.InProgressCard(pending.CaseTitle);
        HasPendingGrading = pending is not null;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not load the practical cases ({ExceptionType})")]
    private partial void LogLoadFailed(Exception exception, string? exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not load the grading quota or the latest grading ({ExceptionType})")]
    private partial void LogSecondaryLoadFailed(Exception exception, string? exceptionType);
}