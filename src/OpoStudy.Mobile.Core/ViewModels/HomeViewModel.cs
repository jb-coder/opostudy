using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Extensions.Logging;

using OpoStudy.Contracts.Progress;
using OpoStudy.Contracts.Quizzes;
using OpoStudy.Contracts.Reviews;
using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.Auth;
using OpoStudy.Mobile.Core.Navigation;
using OpoStudy.Mobile.Core.Offline;
using OpoStudy.Mobile.Core.Reminders;
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
    ReminderService reminders,
    IOfflineQuizService offlineQuizzes,
    IOfflineSynchronizer synchronizer,
    OfflineState offlineState,
    ILogger<HomeViewModel> logger) : OfflineAwareViewModel(offlineState)
{
    private const string NoDueReviewsCode = "quiz.no_due_reviews";
    private const string ReminderDeniedNotice = "Activa las notificaciones de OpoStudy en los ajustes de Android para recibir el recordatorio.";

    /// <summary>Set while the switch is changed from code, so it does not trigger the toggle command.</summary>
    private bool _isSyncingReminder;

    /// <summary>Whether the reminder card is shown (only on platforms with a scheduler).</summary>
    public bool IsReminderSupported => reminders.IsSupported;

    [ObservableProperty]
    public partial bool ReminderEnabled { get; set; }

    [ObservableProperty]
    public partial string ReminderTimeText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReminderNotice))]
    public partial string ReminderNotice { get; set; } = string.Empty;

    public bool HasReminderNotice => ReminderNotice.Length > 0;

    /// <summary>False while the toggle command runs, so the switch cannot be flipped again meanwhile.</summary>
    [ObservableProperty]
    public partial bool IsReminderToggleEnabled { get; set; } = true;

    partial void OnReminderEnabledChanged(bool value)
    {
        if (!_isSyncingReminder)
        {
            ToggleReminderCommand.Execute(null);
        }
    }

    private void LoadReminder()
    {
        var settings = reminders.Settings;
        SetReminderEnabled(settings.Enabled);
        ReminderTimeText = ReminderTimeLabel(settings.Time);
    }

    private void SetReminderEnabled(bool value)
    {
        _isSyncingReminder = true;
        try
        {
            ReminderEnabled = value;
        }
        finally
        {
            _isSyncingReminder = false;
        }
    }

    private static string ReminderTimeLabel(TimeOnly time) =>
        $"Todos los días a las {time.ToString("HH:mm", CultureInfo.InvariantCulture)} si no has cumplido el objetivo";

    [RelayCommand]
    private async Task ToggleReminderAsync()
    {
        ReminderNotice = string.Empty;
        IsReminderToggleEnabled = false;
        try
        {
            if (ReminderEnabled)
            {
                if (!await reminders.EnableAsync(reminders.Settings.Time))
                {
                    ReminderNotice = ReminderDeniedNotice;
                }
            }
            else
            {
                reminders.Disable();
            }
        }
        finally
        {
            // The switch always shows the saved state.
            SetReminderEnabled(reminders.Settings.Enabled);
            IsReminderToggleEnabled = true;
        }
    }

    [RelayCommand]
    private async Task ChangeReminderTimeAsync()
    {
        var choice = await dialogs.ChooseAsync(
            "Hora del recordatorio",
            "Cancelar",
            [.. ReminderSettings.AllowedTimes.Select(t => t.ToString("HH:mm", CultureInfo.InvariantCulture))]);
        if (choice is null || !TimeOnly.TryParseExact(choice, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var at))
        {
            return;
        }

        reminders.ChangeTime(at);
        ReminderTimeText = ReminderTimeLabel(at);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectionText))]
    public partial string? ApiVersion { get; set; }

    [ObservableProperty]
    public partial bool IsConnected { get; set; }

    [ObservableProperty]
    public partial string? AccountEmail { get; set; }

    /// <summary>Non-blocking notice shown when a quiz made offline was discarded by the server; it goes away on the next refresh.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDiscardedNotice))]
    public partial string? DiscardedNotice { get; set; }

    public bool HasDiscardedNotice => DiscardedNotice is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MemberSinceText))]
    [NotifyPropertyChangedFor(nameof(HasMemberSince))]
    public partial string? MemberSince { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStreak))]
    public partial StreakResponse? Streak { get; set; }

    [ObservableProperty]
    public partial string GoalProgressText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double GoalProgress { get; set; }

    [ObservableProperty]
    public partial string StreakText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LongestStreakText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasKeepStreakText))]
    public partial string KeepStreakText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FreezesText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string BuyFreezeText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFreezesNotice))]
    public partial string FreezesNotice { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSpendableXpText))]
    public partial string SpendableXpText { get; set; } = string.Empty;

    public bool HasFreezesNotice => FreezesNotice.Length > 0;

    public bool HasSpendableXpText => SpendableXpText.Length > 0;

    [ObservableProperty]
    public partial IReadOnlyList<DayDot> Dots { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLevel))]
    public partial LevelResponse? Level { get; set; }

    [ObservableProperty]
    public partial string LevelTitleText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double LevelProgress { get; set; }

    [ObservableProperty]
    public partial string LevelDetailText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string XpTodayText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLevelError))]
    public partial string? LevelErrorMessage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBuyFreezeError))]
    public partial string? BuyFreezeErrorMessage { get; set; }

    /// <summary>Whether the daily goal card shows a streak freeze purchase error.</summary>
    public bool HasBuyFreezeError => BuyFreezeErrorMessage is not null;

    /// <summary>Whether the level card has data to show.</summary>
    public bool HasLevel => Level is not null;

    public bool HasLevelError => LevelErrorMessage is not null;

    /// <summary>Whether the daily goal card has data to show.</summary>
    public bool HasStreak => Streak is not null;

    public bool HasKeepStreakText => KeepStreakText.Length > 0;

    private static readonly int[] GoalChoices = [10, 20, 30, 50];

    public bool HasMemberSince => MemberSince is not null;

    public string MemberSinceText => MemberSince is null ? string.Empty : $"Miembro desde {MemberSince}";

    private static readonly CultureInfo SpanishCulture = CultureInfo.GetCultureInfo("es-ES");

    /// <summary>Whether the style catalog button is shown (debug builds only).</summary>
    public bool IsStyleGuideVisible { get; } = environment.IsDebug;

    public string ConnectionText => ApiVersion is null ? string.Empty : $"Conectado · API {StripBuildMetadata(ApiVersion)}";

    public override async Task OnAppearingAsync()
    {
        LoadReminder();
        ListenToOfflineState();
        synchronizer.ReconnectSynced -= OnReconnectSynced;
        synchronizer.ReconnectSynced += OnReconnectSynced;

        if (ApiVersion is not null)
        {
            // Already loaded: only the daily goal card can change while away (e.g. after a quiz).
            if (!IsBusy)
            {
                // Answers given offline must reach the server before the streak and the level are read.
                await synchronizer.SyncAsync(CancellationToken.None);
                ShowDiscardedNotice();
                await LoadStreakAsync(CancellationToken.None);
                await LoadLevelAsync(CancellationToken.None);
                await LoadUnfinishedQuizAsync(CancellationToken.None);
                await LoadDueReviewsAsync(CancellationToken.None);
            }

            return;
        }

        if (RefreshCommand.CanExecute(null))
        {
            await RefreshCommand.ExecuteAsync(null);
        }
    }

    public override void OnDisappearing()
    {
        synchronizer.ReconnectSynced -= OnReconnectSynced;
        base.OnDisappearing();
    }

    /// <summary>The connection came back and the queue was flushed: reload the goal, streak and level the server recomputed.</summary>
    private void OnReconnectSynced(object? sender, EventArgs e)
    {
        _ = ReloadAfterReconnectAsync();
    }

    /// <summary>Fire-and-forget from an event: it must never throw, or the exception would go unobserved.</summary>
    private async Task ReloadAfterReconnectAsync()
    {
        try
        {
            if (RefreshCommand.CanExecute(null))
            {
                await RefreshCommand.ExecuteAsync(null);
            }
        }
        catch (Exception ex)
        {
            LogLoadFailed(ex, ex.GetType().FullName);
        }
    }

    /// <summary>Shows the quizzes discarded since the last refresh (none clears the notice).</summary>
    private void ShowDiscardedNotice() => DiscardedNotice = OfflineState.DiscardedNotice(OfflineState.TakeDiscardedQuizzes());

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
        ChangeDailyGoalCommand.NotifyCanExecuteChanged();
        BuyStreakFreezeCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        ErrorMessage = null;
        BuyFreezeErrorMessage = null;
        AccountEmail = auth.Email;
        try
        {
            var response = await api.GetVersionAsync(cancellationToken);
            ApiVersion = response.Version;
            IsConnected = true;
            OfflineState.ReportSuccess();

            var me = await api.GetMeAsync(cancellationToken);
            MemberSince = TimeZoneInfo.ConvertTime(me.CreatedAt, time.LocalTimeZone).ToString("d 'de' MMMM 'de' yyyy", SpanishCulture);

            await SyncTimeZoneAsync(me.TimeZone, cancellationToken);
            await synchronizer.SyncAsync(cancellationToken);
            ShowDiscardedNotice();
            await LoadStreakAsync(cancellationToken);
            await LoadLevelAsync(cancellationToken);
            await LoadUnfinishedQuizAsync(cancellationToken);
            await LoadDueReviewsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            LogLoadFailed(ex, ex.GetType().FullName);
            IsConnected = false;
            ErrorMessage = ApiErrorMessages.From(ex);
            if (ConnectivityErrors.IsConnectivityError(ex, cancellationToken))
            {
                OfflineState.ReportConnectivityFailure();

                // The "unfinished quiz" card can still come from the local database.
                await LoadUnfinishedQuizAsync(cancellationToken);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Sends the device time zone when it differs from the profile one. Failures are only logged.</summary>
    private async Task SyncTimeZoneAsync(string profileTimeZone, CancellationToken cancellationToken)
    {
        var deviceTimeZone = time.LocalTimeZone.Id;
        if (string.Equals(profileTimeZone, deviceTimeZone, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            await api.UpdateTimeZoneAsync(new UpdateTimeZoneRequest(deviceTimeZone), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogTimeZoneSyncFailed(ex, ex.GetType().FullName);
        }
    }

    /// <summary>Bumped by every streak load or goal change, so a slower, older response never overwrites a newer one.</summary>
    private int _streakVersion;

    /// <summary>Loads the daily goal card. Failures are only logged: the card stays hidden or stale.</summary>
    private async Task LoadStreakAsync(CancellationToken cancellationToken)
    {
        var version = ++_streakVersion;
        try
        {
            var streak = await api.GetStreakAsync(cancellationToken);
            OfflineState.ReportSuccess();

            // A newer load or a goal change finished first: this response is stale.
            if (version == _streakVersion)
            {
                ApplyStreak(streak);
                reminders.OnSignedIn();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogStreakFailed(ex, ex.GetType().FullName);
            ReportIfConnectivity(ex, cancellationToken);
        }
    }

    /// <summary>Bumped by every level load, so a slower, older response never overwrites a newer one.</summary>
    private int _levelVersion;

    /// <summary>Loads the level card. A failure only shows an error on that card; the daily goal card is not affected.</summary>
    private async Task LoadLevelAsync(CancellationToken cancellationToken)
    {
        var version = ++_levelVersion;
        try
        {
            var level = await api.GetLevelAsync(cancellationToken);
            OfflineState.ReportSuccess();
            if (version == _levelVersion)
            {
                Level = level;
                LevelTitleText = LevelCard.TitleText(level);
                LevelProgress = LevelCard.ProgressFraction(level);
                LevelDetailText = LevelCard.DetailText(level);
                XpTodayText = LevelCard.TodayText(level);
                SpendableXpText = LevelCard.SpendableText(level);
                RefreshBuyFreeze();
                LevelErrorMessage = null;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogLevelFailed(ex, ex.GetType().FullName);
            ReportIfConnectivity(ex, cancellationToken);
            if (version == _levelVersion)
            {
                LevelErrorMessage = ApiErrorMessages.From(ex);
            }
        }
    }

    /// <summary>Bumped by every load, so a slower, older response never overwrites a newer one.</summary>
    private int _quizVersion;

    private Guid _unfinishedQuizId;

    /// <summary>Whether the "unfinished quiz" card is shown.</summary>
    [ObservableProperty]
    public partial bool HasUnfinishedQuiz { get; set; }

    [ObservableProperty]
    public partial string UnfinishedQuizTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string UnfinishedQuizProgressText { get; set; } = string.Empty;

    /// <summary>
    /// Loads the most recent quiz and shows it only if it is started and not finished. The local quiz is used when the
    /// server cannot be reached or when it is more recent. Failures hide the card.
    /// </summary>
    private async Task LoadUnfinishedQuizAsync(CancellationToken cancellationToken)
    {
        var version = ++_quizVersion;
        QuizSummaryResponse? latest = null;
        var serverUnreachable = false;
        try
        {
            var quizzes = await api.GetQuizzesAsync(0, 1, cancellationToken);
            latest = quizzes.Count > 0 ? quizzes[0] : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogQuizzesFailed(ex, ex.GetType().FullName);
            serverUnreachable = ConnectivityErrors.IsConnectivityError(ex, cancellationToken);
            ReportIfConnectivity(ex, cancellationToken);
        }

        var local = await FindLocalUnfinishedQuizAsync(cancellationToken);
        if (version != _quizVersion)
        {
            return;
        }

        if (local is { } found && (serverUnreachable || latest is null || found.Quiz.StartedAt > latest.StartedAt))
        {
            _unfinishedQuizId = found.Quiz.Id;
            UnfinishedQuizTitle = found.Quiz.Title;
            UnfinishedQuizProgressText = AnsweredText(found.Answered, found.Quiz.QuestionIds.Count);
            HasUnfinishedQuiz = true;
            return;
        }

        if (latest is not { FinishedAt: null } || latest.CorrectCount + latest.IncorrectCount == 0)
        {
            HasUnfinishedQuiz = false;
            return;
        }

        _unfinishedQuizId = latest.Id;
        UnfinishedQuizTitle = QuizTitles.For(latest);
        UnfinishedQuizProgressText = AnsweredText(latest.CorrectCount + latest.IncorrectCount, latest.QuestionCount);
        HasUnfinishedQuiz = true;
    }

    private static string AnsweredText(int answered, int total) =>
        string.Create(CultureInfo.InvariantCulture, $"{answered} de {total} respondidas");

    /// <summary>The latest unfinished local quiz, only if it already has answers. Failures give null.</summary>
    private async Task<(LocalQuiz Quiz, int Answered)?> FindLocalUnfinishedQuizAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (await offlineQuizzes.GetLatestUnfinishedAsync(cancellationToken) is not { } quiz
                || await offlineQuizzes.GetAsync(quiz.Id, cancellationToken) is not { } full)
            {
                return null;
            }

            var answered = full.CorrectCount + full.IncorrectCount;
            return answered > 0 ? (quiz, answered) : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogLocalQuizFailed(ex, ex.GetType().FullName);
            return null;
        }
    }

    /// <summary>Bumped by every load, so a slower, older response never overwrites a newer one.</summary>
    private int _dueReviewsVersion;

    private IReadOnlyList<ExamDueReviewsResponse> _dueExams = [];

    /// <summary>Whether the "Repaso de hoy" card is shown: only when some question is due.</summary>
    [ObservableProperty]
    public partial bool HasDueReviews { get; set; }

    [ObservableProperty]
    public partial string DueReviewsText { get; set; } = string.Empty;

    /// <summary>False while the review quiz is being created, so the button cannot be pressed twice.</summary>
    [ObservableProperty]
    public partial bool IsDueReviewEnabled { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDueReviewError))]
    public partial string? DueReviewErrorMessage { get; set; }

    public bool HasDueReviewError => DueReviewErrorMessage is not null;

    /// <summary>Loads the spaced review card. Failures hide it, without a message.</summary>
    private async Task LoadDueReviewsAsync(CancellationToken cancellationToken)
    {
        var version = ++_dueReviewsVersion;
        try
        {
            var due = await api.GetDueReviewsAsync(cancellationToken);
            OfflineState.ReportSuccess();
            if (version == _dueReviewsVersion)
            {
                ApplyDueReviews(due);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogDueReviewsFailed(ex, ex.GetType().FullName);
            ReportIfConnectivity(ex, cancellationToken);
            if (version == _dueReviewsVersion)
            {
                ApplyDueReviews(null);
            }
        }
    }

    private void ApplyDueReviews(DueReviewSummaryResponse? due)
    {
        _dueExams = due?.Exams ?? [];
        DueReviewsText = due is null ? string.Empty : DueReviewCard.CountText(due.Total);
        HasDueReviews = due is { Total: > 0 };
        if (!HasDueReviews)
        {
            DueReviewErrorMessage = null;
        }
    }

    [RelayCommand(CanExecute = nameof(IsDueReviewEnabled))]
    private async Task StartDueReviewAsync(CancellationToken cancellationToken)
    {
        if (_dueExams.Count == 0)
        {
            return;
        }

        var exam = _dueExams[0];
        if (_dueExams.Count > 1)
        {
            var choice = await dialogs.ChooseAsync("¿Qué oposición quieres repasar?", "Cancelar", [.. _dueExams.Select(e => e.ExamName)]);
            if (choice is null || _dueExams.FirstOrDefault(e => e.ExamName == choice) is not { } chosen)
            {
                return;
            }

            exam = chosen;
        }

        DueReviewErrorMessage = null;
        IsDueReviewEnabled = false;
        try
        {
            var quiz = await api.CreateQuizAsync(
                new CreateQuizRequest(exam.ExamId, null, DueReviewCard.QuestionCount(exam.Count), DueOnly: true),
                cancellationToken);
            OfflineState.ReportSuccess();
            await navigation.GoToAsync(
                Routes.Quiz,
                new Dictionary<string, object> { [QueryKeys.QuizId] = quiz.Id, [QueryKeys.QuizTitle] = DueReviewCard.QuizTitle });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogDueReviewFailed(ex, ex.GetType().FullName);
            var message = ApiErrorMessages.From(ex);
            ReportIfConnectivity(ex, cancellationToken);
            if (ApiErrorMessages.ProblemCode(ex) == NoDueReviewsCode)
            {
                // The count was stale: reload it (this may hide the card).
                await LoadDueReviewsAsync(cancellationToken);
            }

            DueReviewErrorMessage = message;
        }
        finally
        {
            IsDueReviewEnabled = true;
        }
    }

    partial void OnIsDueReviewEnabledChanged(bool value) => StartDueReviewCommand.NotifyCanExecuteChanged();

    private void ReportIfConnectivity(Exception exception, CancellationToken cancellationToken)
    {
        if (ConnectivityErrors.IsConnectivityError(exception, cancellationToken))
        {
            OfflineState.ReportConnectivityFailure();
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read the local quiz ({ExceptionType})")]
    private partial void LogLocalQuizFailed(Exception exception, string? exceptionType);

    [RelayCommand]
    private Task ContinueQuizAsync() =>
        navigation.GoToAsync(
            Routes.Quiz,
            new Dictionary<string, object> { [QueryKeys.QuizId] = _unfinishedQuizId, [QueryKeys.QuizTitle] = UnfinishedQuizTitle });

    private void ApplyStreak(StreakResponse streak)
    {
        Streak = streak;
        reminders.UpdateFromStreak(streak);
        GoalProgressText = DailyGoalCard.ProgressText(streak);
        GoalProgress = DailyGoalCard.ProgressFraction(streak);
        StreakText = DailyGoalCard.StreakText(streak.CurrentStreak);
        LongestStreakText = DailyGoalCard.LongestText(streak.LongestStreak);
        KeepStreakText = DailyGoalCard.KeepStreakText(streak);
        Dots = DailyGoalCard.Dots(streak);
        FreezesText = DailyGoalCard.FreezesText(streak);
        FreezesNotice = DailyGoalCard.FreezesUsedNotice(streak);
        RefreshBuyFreeze();
    }

    private void RefreshBuyFreeze()
    {
        if (Streak is { } streak)
        {
            BuyFreezeText = DailyGoalCard.BuyFreezeButtonText(streak, Level);
        }

        BuyStreakFreezeCommand.NotifyCanExecuteChanged();
    }

    private bool CanBuyStreakFreeze() => !IsBusy && Streak is { } streak && DailyGoalCard.CanBuyFreeze(streak, Level);

    [RelayCommand(CanExecute = nameof(CanBuyStreakFreeze))]
    private async Task BuyStreakFreezeAsync(CancellationToken cancellationToken)
    {
        if (Streak is not { } current)
        {
            return;
        }

        var confirmed = await dialogs.ConfirmAsync(
            "Comprar protector",
            DailyGoalCard.BuyFreezeConfirmText(current),
            "Comprar",
            "Cancelar");
        if (!confirmed)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        BuyFreezeErrorMessage = null;
        try
        {
            var streak = await api.BuyStreakFreezeAsync(cancellationToken);
            _streakVersion++;
            ApplyStreak(streak);
            await LoadLevelAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            LogBuyFreezeFailed(ex, ex.GetType().FullName);
            BuyFreezeErrorMessage = ApiErrorMessages.From(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task ChangeDailyGoalAsync(CancellationToken cancellationToken)
    {
        var choice = await dialogs.ChooseAsync(
            "Objetivo diario",
            "Cancelar",
            [.. GoalChoices.Select(g => g.ToString(CultureInfo.InvariantCulture))]);
        if (choice is null || !int.TryParse(choice, CultureInfo.InvariantCulture, out var goal))
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var streak = await api.UpdateDailyGoalAsync(new UpdateDailyGoalRequest(goal), cancellationToken);
            _streakVersion++;
            ApplyStreak(streak);
            await LoadLevelAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            LogGoalChangeFailed(ex, ex.GetType().FullName);
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
            try
            {
                await auth.SignOutAsync();
            }
            finally
            {
                // Even if sign-out fails, no reminder may stay scheduled with this account's data.
                reminders.OnSignedOut();
            }

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

        // The uid is read now: after deleting the Firebase account there is no session to read it from.
        var userId = auth.UserId;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            // API data first, while the ID token is still valid; then the Firebase account.
            await api.DeleteMeAsync(cancellationToken);
            await auth.DeleteFirebaseAccountAsync(cancellationToken);
            await DeleteLocalDataAsync(userId);
            reminders.OnSignedOut();
            await navigation.GoToAsync(Routes.Root(Routes.Login));
        }
        catch (FirebaseAuthException ex) when (ex.Code == "CREDENTIAL_TOO_OLD_LOGIN_AGAIN")
        {
            await DeleteLocalDataAsync(userId);
            reminders.OnSignedOut();
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

    /// <summary>Deletes the offline queue of the deleted account. A failure is only logged: the account is already gone.</summary>
    private async Task DeleteLocalDataAsync(string? userId)
    {
        if (userId is null)
        {
            return;
        }

        try
        {
            await offlineQuizzes.DeleteUserDataAsync(userId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogLocalQuizFailed(ex, ex.GetType().FullName);
        }
    }

    [RelayCommand]
    private Task StartQuizAsync() => navigation.GoToAsync(Routes.Exams);

    [RelayCommand]
    private Task OpenStatsAsync() => navigation.GoToAsync(Routes.Stats);

    [RelayCommand]
    private Task OpenStyleGuideAsync() => navigation.GoToAsync(Routes.StyleGuide);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not sync the time zone ({ExceptionType})")]
    private partial void LogTimeZoneSyncFailed(Exception exception, string? exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not load the streak ({ExceptionType})")]
    private partial void LogStreakFailed(Exception exception, string? exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not load the last quiz ({ExceptionType})")]
    private partial void LogQuizzesFailed(Exception exception, string? exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not load the due reviews ({ExceptionType})")]
    private partial void LogDueReviewsFailed(Exception exception, string? exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not create the review quiz ({ExceptionType})")]
    private partial void LogDueReviewFailed(Exception exception, string? exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not load the level ({ExceptionType})")]
    private partial void LogLevelFailed(Exception exception, string? exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not change the daily goal ({ExceptionType})")]
    private partial void LogGoalChangeFailed(Exception exception, string? exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not buy a streak freeze ({ExceptionType})")]
    private partial void LogBuyFreezeFailed(Exception exception, string? exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not sign out ({ExceptionType})")]
    private partial void LogSignOutFailed(Exception exception, string? exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not delete the account ({ExceptionType})")]
    private partial void LogDeleteFailed(Exception exception, string? exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not load the API version ({ExceptionType})")]
    private partial void LogLoadFailed(Exception exception, string? exceptionType);
}