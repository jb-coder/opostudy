using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Extensions.Logging;

using OpoStudy.Contracts.Gradings;
using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.Navigation;
using OpoStudy.Mobile.Core.Offline;
using OpoStudy.Mobile.Core.PracticalCases;

namespace OpoStudy.Mobile.Core.ViewModels;

/// <summary>A grading of the history. The status is text plus icon, never only colour.</summary>
public sealed record GradingHistoryItem(GradingSummaryResponse Grading)
{
    public Guid Id => Grading.Id;

    public string CaseTitle => Grading.CaseTitle;

    public string? Section => string.IsNullOrWhiteSpace(Grading.Section) ? null : Grading.Section;

    public bool HasSection => Section is not null;

    public string DateText => PracticalCaseTexts.HistoryDate(Grading.SubmittedAt);

    public bool IsCompleted => Grading.Status == GradingStatus.Completed;

    public bool IsFailed => Grading.Status == GradingStatus.Failed;

    /// <summary>Pending, processing or any unknown status.</summary>
    public bool IsInProgress => !IsCompleted && !IsFailed;

    public string StatusText => Grading.Status switch
    {
        GradingStatus.Completed => PracticalCaseTexts.HistoryScore(Grading.Score, Grading.MaxPoints),
        GradingStatus.Failed => PracticalCaseTexts.HistoryFailed,
        _ => PracticalCaseTexts.HistoryInProgress,
    };

    public string StatusIcon => IsCompleted ? "✓" : IsFailed ? "✕" : "…";

    public string AccessibilityText =>
        $"{CaseTitle}. {(Section is null ? string.Empty : Section + ". ")}{DateText}. {StatusText}";
}

/// <summary>History of the user's corrections (newest first), paginated.</summary>
public sealed partial class GradingsViewModel(
    IOpoStudyApi api,
    INavigationService navigation,
    OfflineState offlineState,
    ILogger<GradingsViewModel> logger) : OfflineAwareViewModel(offlineState)
{
    internal const int PageSize = 20;

    public ObservableCollection<GradingHistoryItem> Items { get; } = [];

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    /// <summary>There is no connection: the history is not loaded and the "need connection" state is shown.</summary>
    [ObservableProperty]
    public partial bool NeedsConnection { get; set; }

    /// <summary>The last page was full, so there may be more.</summary>
    [ObservableProperty]
    public partial bool CanLoadMore { get; set; }

    public override async Task OnAppearingAsync()
    {
        ListenToOfflineState();

        // Reloaded every time: a grading in progress changes while the user is elsewhere.
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

    protected override void OnBusyChanged()
    {
        RefreshCommand.NotifyCanExecuteChanged();
        LoadMoreCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        if (OfflineState.IsOffline)
        {
            ShowNeedsConnection();
            return;
        }

        IsBusy = true;
        try
        {
            var page = await api.GetGradingsAsync(0, PageSize, cancellationToken);
            NeedsConnection = false;
            Items.Clear();
            Append(page);
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

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task LoadMoreAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            var page = await api.GetGradingsAsync(Items.Count, PageSize, cancellationToken);

            // A grading submitted between pages shifts the offset: ids already shown are skipped.
            var known = Items.Select(i => i.Id).ToHashSet();
            Append([.. page.Where(g => !known.Contains(g.Id))]);
            CanLoadMore = page.Count >= PageSize;
        }
        catch (OperationCanceledException)
        {
            // The page went away before the data arrived.
        }
        catch (Exception ex)
        {
            // What is already loaded stays on screen.
            LogLoadFailed(ex, ex.GetType().FullName);
            ErrorMessage = ApiErrorMessages.From(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task OpenGradingAsync(GradingHistoryItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return navigation.GoToAsync(Routes.Grading, new Dictionary<string, object> { [QueryKeys.GradingId] = item.Id });
    }

    private void Append(IReadOnlyList<GradingSummaryResponse> page)
    {
        foreach (var grading in page)
        {
            Items.Add(new GradingHistoryItem(grading));
        }

        CanLoadMore = page.Count >= PageSize;
        IsEmpty = Items.Count == 0;
    }

    private void ShowNeedsConnection()
    {
        Items.Clear();
        IsEmpty = false;
        CanLoadMore = false;
        NeedsConnection = true;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not load the grading history ({ExceptionType})")]
    private partial void LogLoadFailed(Exception exception, string? exceptionType);
}