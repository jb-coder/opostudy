using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Extensions.Logging;

using OpoStudy.Contracts.Progress;
using OpoStudy.Contracts.Quizzes;
using OpoStudy.Contracts.Stats;
using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.Navigation;
using OpoStudy.Mobile.Core.Offline;
using OpoStudy.Mobile.Core.Stats;

namespace OpoStudy.Mobile.Core.ViewModels;

/// <summary>
/// Statistics screen: quiz indicators, weakest topics, month calendar and practical case scores. Online only: nothing is
/// stored on the device. A response that arrives after a newer request (exam or month change) is discarded.
/// </summary>
public sealed partial class StatsViewModel(
    IOpoStudyApi api,
    INavigationService navigation,
    TimeProvider time,
    OfflineState offlineState,
    ILogger<StatsViewModel> logger) : OfflineAwareViewModel(offlineState)
{
    private readonly List<TopicStatItem> _allTopics = [];

    private Guid _examId;

    private string _examName = string.Empty;

    /// <summary>Set while the exam picker is filled from code, so it does not trigger a second load.</summary>
    private bool _suppressExamChange;

    private int _busyCount;

    /// <summary>Bumped by every stats load: only the response of the latest one is applied.</summary>
    private int _statsVersion;

    /// <summary>Bumped by every activity load: only the response of the latest one is applied.</summary>
    private int _activityVersion;

    private DateOnly _month = ActivityCalendar.MonthOf(DateOnly.FromDateTime(time.GetLocalNow().DateTime));

    private DateOnly _today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);

    /// <summary>Whether <c>Today</c> came from the server (it is the user's time zone, not the device's).</summary>
    private bool _todayKnown;

    private bool _userNavigated;

    /// <summary>Month the loaded activity belongs to; days of any other month are never shown.</summary>
    private DateOnly? _activityMonth;

    private IReadOnlyList<DayActivityResponse> _activityDays = [];

    private DateOnly? _selectedDate;

    private bool _showAllTopics;

    public ObservableCollection<ExamItem> Exams { get; } = [];

    /// <summary>Topics shown in the list: the weakest ones or all of them.</summary>
    public ObservableCollection<TopicStatItem> VisibleTopics { get; } = [];

    public bool ShowExamPicker => Exams.Count > 1;

    [ObservableProperty]
    public partial ExamItem? SelectedExam { get; set; }

    /// <summary>The stats of the current exam were loaded.</summary>
    [ObservableProperty]
    public partial bool HasStats { get; set; }

    /// <summary>No answers and no gradings at all.</summary>
    [ObservableProperty]
    public partial bool IsEmptyStats { get; set; }

    [ObservableProperty]
    public partial bool NoExams { get; set; }

    /// <summary>There is no connection and nothing was loaded: only the "needs connection" state is shown.</summary>
    [ObservableProperty]
    public partial bool NeedsConnection { get; set; }

    [ObservableProperty]
    public partial string AnsweredText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AccuracyText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string QuizzesText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TopicsText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasTopics { get; set; }

    [ObservableProperty]
    public partial bool CanToggleTopics { get; set; }

    [ObservableProperty]
    public partial string ToggleTopicsText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string GradingsSummaryText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasScores { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<ScoreBarItem> ScoreBars { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<BestScoreItem> BestScores { get; set; } = [];

    [ObservableProperty]
    public partial string MonthTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<CalendarCell> Cells { get; set; } = [];

    [ObservableProperty]
    public partial string ActivitySummaryText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasActivitySummary { get; set; }

    [ObservableProperty]
    public partial bool IsActivityLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActivityError))]
    public partial string? ActivityError { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedDay))]
    public partial string? SelectedDayText { get; set; }

    public bool HasActivityError => !string.IsNullOrEmpty(ActivityError);

    public bool HasSelectedDay => !string.IsNullOrEmpty(SelectedDayText);

    /// <summary>Whether the next month can be shown (never beyond the current one).</summary>
    public bool CanGoNext => ActivityCalendar.CanGoNext(_month, _today);

    /// <summary>First load, with nothing to show yet.</summary>
    public bool ShowInitialLoading => IsBusy && !HasStats && !NeedsConnection;

    /// <summary>The indicators, topics and gradings blocks (the calendar has its own rule).</summary>
    public bool ShowStatsBlocks => HasStats && !IsEmptyStats;

    public bool ShowNoAnswers => ShowStatsBlocks && !HasTopics;

    public bool ShowNoGradings => ShowStatsBlocks && !HasScores;

    /// <summary>The calendar is independent of the exam, so it stays unless the whole screen needs connection.</summary>
    public bool ShowActivity => !NeedsConnection;

    public override bool ShowOfflineBanner =>
        OfflineState.IsOffline ? HasStats : OfflineState.SyncingAnswers > 0;

    public override string OfflineBannerText =>
        OfflineState.IsOffline ? StatsTexts.OfflineWithData : base.OfflineBannerText;

    public override async Task OnAppearingAsync()
    {
        ListenToOfflineState();
        RebuildCalendar();

        // Reloaded every time: the user may be back from a quiz.
        if (RefreshCommand.CanExecute(null))
        {
            await RefreshCommand.ExecuteAsync(null);
        }
    }

    protected override void OnBusyChanged()
    {
        RefreshCommand.NotifyCanExecuteChanged();
        PracticeTopicCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ShowInitialLoading));
    }

    partial void OnHasStatsChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowInitialLoading));
        OnPropertyChanged(nameof(ShowStatsBlocks));
        OnPropertyChanged(nameof(ShowNoAnswers));
        OnPropertyChanged(nameof(ShowNoGradings));
        OnPropertyChanged(nameof(ShowOfflineBanner));
    }

    partial void OnIsEmptyStatsChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowStatsBlocks));
        OnPropertyChanged(nameof(ShowNoAnswers));
        OnPropertyChanged(nameof(ShowNoGradings));
    }

    partial void OnHasTopicsChanged(bool value) => OnPropertyChanged(nameof(ShowNoAnswers));

    partial void OnHasScoresChanged(bool value) => OnPropertyChanged(nameof(ShowNoGradings));

    partial void OnNeedsConnectionChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowInitialLoading));
        OnPropertyChanged(nameof(ShowActivity));
    }

    partial void OnSelectedExamChanged(ExamItem? value)
    {
        // The picker writes null when its items change: the chosen exam is _examId, not what the picker holds.
        if (_suppressExamChange || value is null || value.Exam.Id == _examId)
        {
            return;
        }

        ChangeExamCommand.Execute(value);
    }

    private void BeginBusy()
    {
        _busyCount++;
        IsBusy = true;
    }

    private void EndBusy()
    {
        _busyCount--;
        IsBusy = _busyCount > 0;
    }

    private CancellationTokenSource LinkToPage(CancellationToken cancellationToken) =>
        CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, PageToken);

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        using var linked = LinkToPage(cancellationToken);
        var token = linked.Token;
        BeginBusy();
        try
        {
            ErrorMessage = null;
            var activity = LoadActivityAsync(token);
            if (await EnsureExamsAsync(token) && _examId != Guid.Empty)
            {
                await LoadStatsAsync(_examId, token);
            }

            await activity;
        }
        finally
        {
            EndBusy();
        }
    }

    /// <summary>Loads the exams once and picks the first. False when they could not be loaded (the state is already set).</summary>
    private async Task<bool> EnsureExamsAsync(CancellationToken cancellationToken)
    {
        if (Exams.Count > 0)
        {
            return true;
        }

        if (!OfflineState.HasInternet)
        {
            NeedsConnection = !HasStats;
            return false;
        }

        try
        {
            var exams = await api.GetExamsAsync(cancellationToken);
            OfflineState.ReportSuccess();
            NeedsConnection = false;
            NoExams = exams.Count == 0;
            if (exams.Count == 0)
            {
                return true;
            }

            _suppressExamChange = true;
            try
            {
                foreach (var exam in exams)
                {
                    Exams.Add(new ExamItem(exam));
                }

                _examId = exams[0].Id;
                _examName = exams[0].Name;
                SelectedExam = Exams[0];
            }
            finally
            {
                _suppressExamChange = false;
            }

            OnPropertyChanged(nameof(ShowExamPicker));
            return true;
        }
        catch (Exception ex) when (ConnectivityErrors.IsConnectivityError(ex, cancellationToken))
        {
            LogLoadFailed(ex, ex.GetType().FullName);
            OfflineState.ReportConnectivityFailure();
            NeedsConnection = !HasStats;
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            LogLoadFailed(ex, ex.GetType().FullName);
            NeedsConnection = false;
            ErrorMessage = ApiErrorMessages.From(ex);
            return false;
        }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task ChangeExamAsync(ExamItem exam, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(exam);

        using var linked = LinkToPage(cancellationToken);
        _examId = exam.Exam.Id;
        _examName = exam.Name;

        // Another exam's numbers must never be shown under this one.
        ClearStats();
        ErrorMessage = null;
        _showAllTopics = false;
        BeginBusy();
        try
        {
            await LoadStatsAsync(exam.Exam.Id, linked.Token);
        }
        finally
        {
            EndBusy();
        }
    }

    /// <summary>Loads the stats of an exam. It never throws; a newer load makes this one's response be dropped.</summary>
    private async Task LoadStatsAsync(Guid examId, CancellationToken cancellationToken)
    {
        var version = ++_statsVersion;
        if (!OfflineState.HasInternet)
        {
            NeedsConnection = !HasStats;
            return;
        }

        try
        {
            var stats = await api.GetStatsAsync(examId, cancellationToken);
            OfflineState.ReportSuccess();
            if (version != _statsVersion)
            {
                return;
            }

            ErrorMessage = null;
            NeedsConnection = false;
            ApplyStats(stats);
        }
        catch (Exception ex) when (ConnectivityErrors.IsConnectivityError(ex, cancellationToken))
        {
            LogLoadFailed(ex, ex.GetType().FullName);
            if (version == _statsVersion)
            {
                OfflineState.ReportConnectivityFailure();
                ErrorMessage = null;
                NeedsConnection = !HasStats;
            }
        }
        catch (OperationCanceledException)
        {
            // The page went away, or a newer load took over.
        }
        catch (Exception ex)
        {
            LogLoadFailed(ex, ex.GetType().FullName);
            if (version == _statsVersion)
            {
                NeedsConnection = false;
                ErrorMessage = ApiErrorMessages.From(ex);
            }
        }
    }

    private void ApplyStats(StatsResponse stats)
    {
        var quizzes = stats.Quizzes;
        var gradings = stats.PracticalCases;
        var zone = time.LocalTimeZone;

        AnsweredText = quizzes.AnsweredCount.ToString(StatsTexts.Culture);
        AccuracyText = StatsTexts.Percent(quizzes.CorrectCount, quizzes.AnsweredCount);
        QuizzesText = quizzes.FinishedQuizCount.ToString(StatsTexts.Culture);
        TopicsText = StatsTexts.TopicsPracticed(quizzes.Topics.Count, quizzes.AvailableTopicCount);

        _allTopics.Clear();
        _allTopics.AddRange(quizzes.Topics.Select(t => new TopicStatItem(t, zone)));
        HasTopics = _allTopics.Count > 0;
        RebuildTopics();

        GradingsSummaryText = StatsTexts.GradingsSummary(gradings);
        HasScores = gradings.CompletedCount > 0;
        ScoreBars = [.. gradings.RecentScores.Select(s => new ScoreBarItem(s, zone))];
        BestScores = [.. gradings.BestScores.Select(b => new BestScoreItem(b))];

        IsEmptyStats = quizzes.AnsweredCount == 0 && gradings.CompletedCount == 0;
        HasStats = true;
        OnPropertyChanged(nameof(ShowStatsBlocks));
    }

    private void ClearStats()
    {
        _allTopics.Clear();
        VisibleTopics.Clear();
        ScoreBars = [];
        BestScores = [];
        HasTopics = false;
        HasScores = false;
        IsEmptyStats = false;
        CanToggleTopics = false;
        NeedsConnection = false;
        HasStats = false;
    }

    private void RebuildTopics()
    {
        var shown = _showAllTopics ? _allTopics : _allTopics.Take(StatsTexts.CollapsedTopicCount).ToList();
        VisibleTopics.Clear();
        foreach (var topic in shown)
        {
            VisibleTopics.Add(topic);
        }

        CanToggleTopics = _allTopics.Count > StatsTexts.CollapsedTopicCount;
        ToggleTopicsText = _showAllTopics ? StatsTexts.ShowFewer : StatsTexts.ShowAllTopics(_allTopics.Count);
    }

    [RelayCommand]
    private void ToggleTopics()
    {
        _showAllTopics = !_showAllTopics;
        RebuildTopics();
    }

    [RelayCommand(CanExecute = nameof(CanPracticeTopic))]
    private async Task PracticeTopicAsync(TopicStatItem topic, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(topic);

        using var linked = LinkToPage(cancellationToken);
        foreach (var item in _allTopics)
        {
            item.Message = null;
        }

        if (!OfflineState.HasInternet)
        {
            topic.Message = StatsTexts.PracticeOffline;
            return;
        }

        BeginBusy();
        try
        {
            var quiz = await api.CreateQuizAsync(
                new CreateQuizRequest(_examId, topic.Id, StatsTexts.PracticeQuestionCount),
                linked.Token);
            OfflineState.ReportSuccess();
            await navigation.GoToAsync(
                Routes.Quiz,
                new Dictionary<string, object>
                {
                    [QueryKeys.QuizId] = quiz.Id,
                    [QueryKeys.QuizTitle] = QuizTitles.ForTopic(topic.Title, false),
                });
        }
        catch (Exception ex) when (ConnectivityErrors.IsConnectivityError(ex, linked.Token))
        {
            LogPracticeFailed(ex, ex.GetType().FullName);
            OfflineState.ReportConnectivityFailure();
            topic.Message = StatsTexts.PracticeOffline;
        }
        catch (OperationCanceledException)
        {
            // The page went away before the quiz was created.
        }
        catch (Exception ex)
        {
            LogPracticeFailed(ex, ex.GetType().FullName);
            topic.Message = ApiErrorMessages.From(ex);
        }
        finally
        {
            EndBusy();
        }
    }

    private bool CanPracticeTopic(TopicStatItem? topic) => IsNotBusy && topic is { HasQuestions: true };

    /// <summary>Empty state: opens the topics of the exam to make a first quiz.</summary>
    [RelayCommand]
    private Task StartQuizAsync() =>
        navigation.GoToAsync(
            Routes.Topics,
            new Dictionary<string, object> { [QueryKeys.ExamId] = _examId, [QueryKeys.ExamName] = _examName });

    [RelayCommand]
    private Task OpenGradingAsync(BestScoreItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return navigation.GoToAsync(Routes.Grading, new Dictionary<string, object> { [QueryKeys.GradingId] = item.GradingId });
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task PreviousMonthAsync(CancellationToken cancellationToken) => ShowMonthAsync(ActivityCalendar.PreviousMonth(_month), cancellationToken);

    [RelayCommand(CanExecute = nameof(CanGoNext), AllowConcurrentExecutions = true)]
    private Task NextMonthAsync(CancellationToken cancellationToken) => ShowMonthAsync(ActivityCalendar.NextMonth(_month), cancellationToken);

    private async Task ShowMonthAsync(DateOnly month, CancellationToken cancellationToken)
    {
        using var linked = LinkToPage(cancellationToken);
        _month = month;
        _userNavigated = true;
        _selectedDate = null;
        SelectedDayText = null;
        ActivityError = null;
        RebuildCalendar();
        await LoadActivityAsync(linked.Token);
    }

    /// <summary>Retries the activity of the month shown, from its own "Reintentar".</summary>
    [RelayCommand]
    private async Task RetryActivityAsync(CancellationToken cancellationToken)
    {
        using var linked = LinkToPage(cancellationToken);
        await LoadActivityAsync(linked.Token);
    }

    [RelayCommand]
    private void SelectDay(CalendarCell cell)
    {
        ArgumentNullException.ThrowIfNull(cell);

        // Without the month's data a day would wrongly read "sin actividad".
        if (!cell.IsSelectable || _activityMonth != _month)
        {
            return;
        }

        _selectedDate = cell.Date;
        SelectedDayText = cell.Description;
    }

    /// <summary>Loads the activity of the shown month. It never throws; a newer load makes this one's response be dropped.</summary>
    private async Task LoadActivityAsync(CancellationToken cancellationToken)
    {
        var month = _month;
        var version = ++_activityVersion;
        IsActivityLoading = true;
        ActivityError = null;
        var retarget = false;
        try
        {
            if (!OfflineState.HasInternet)
            {
                ActivityError = StatsTexts.ActivityFailed;
                return;
            }

            var activity = await api.GetActivityAsync(month, ActivityCalendar.LastDayOf(month), cancellationToken);
            OfflineState.ReportSuccess();
            if (version != _activityVersion)
            {
                return;
            }

            // The first answer says what day it is for the user; the device may be in another time zone.
            retarget = !_todayKnown && !_userNavigated && ActivityCalendar.MonthOf(activity.Today) != month;
            _today = activity.Today;
            _todayKnown = true;
            _activityMonth = month;
            _activityDays = activity.Days;
            if (retarget)
            {
                _month = ActivityCalendar.MonthOf(activity.Today);
            }

            RebuildCalendar();
        }
        catch (Exception ex) when (ConnectivityErrors.IsConnectivityError(ex, cancellationToken))
        {
            LogActivityFailed(ex, ex.GetType().FullName);
            if (version == _activityVersion)
            {
                OfflineState.ReportConnectivityFailure();
                ActivityError = StatsTexts.ActivityFailed;
            }
        }
        catch (OperationCanceledException)
        {
            // The page went away, or a newer load took over.
        }
        catch (Exception ex)
        {
            LogActivityFailed(ex, ex.GetType().FullName);
            if (version == _activityVersion)
            {
                ActivityError = StatsTexts.ActivityFailed;
            }
        }
        finally
        {
            if (version == _activityVersion)
            {
                IsActivityLoading = false;
            }
        }

        if (retarget && version == _activityVersion && !cancellationToken.IsCancellationRequested)
        {
            await LoadActivityAsync(cancellationToken);
        }
    }

    /// <summary>Builds the grid of the shown month; only data that belongs to that month is painted.</summary>
    private void RebuildCalendar()
    {
        var hasData = _activityMonth == _month;
        IReadOnlyList<DayActivityResponse> days = hasData ? _activityDays : [];
        Cells = ActivityCalendar.BuildCells(_month, _today, days);
        MonthTitle = StatsTexts.MonthTitle(_month);
        HasActivitySummary = hasData;
        ActivitySummaryText = hasData ? ActivityCalendar.Summary(_month, days) : string.Empty;
        SelectedDayText = hasData && _selectedDate is { } selected
            ? Cells.FirstOrDefault(c => c.Date == selected)?.Description
            : null;
        OnPropertyChanged(nameof(CanGoNext));
        NextMonthCommand.NotifyCanExecuteChanged();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not load the statistics ({ExceptionType})")]
    private partial void LogLoadFailed(Exception exception, string? exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not load the activity ({ExceptionType})")]
    private partial void LogActivityFailed(Exception exception, string? exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not create the quiz of a topic ({ExceptionType})")]
    private partial void LogPracticeFailed(Exception exception, string? exceptionType);
}