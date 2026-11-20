using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Extensions.Logging;

using OpoStudy.Contracts.Progress;
using OpoStudy.Contracts.Quizzes;
using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.Auth;
using OpoStudy.Mobile.Core.Navigation;
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
    ILogger<HomeViewModel> logger) : ViewModelBase
{
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
        if (ReminderEnabled)
        {
            if (!await reminders.EnableAsync(reminders.Settings.Time))
            {
                SetReminderEnabled(false);
                ReminderNotice = ReminderDeniedNotice;
            }
        }
        else
        {
            reminders.Disable();
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

        if (ApiVersion is not null)
        {
            // Already loaded: only the daily goal card can change while away (e.g. after a quiz).
            if (!IsBusy)
            {
                await LoadStreakAsync(CancellationToken.None);
                await LoadLevelAsync(CancellationToken.None);
                await LoadUnfinishedQuizAsync(CancellationToken.None);
            }

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

            var me = await api.GetMeAsync(cancellationToken);
            MemberSince = TimeZoneInfo.ConvertTime(me.CreatedAt, time.LocalTimeZone).ToString("d 'de' MMMM 'de' yyyy", SpanishCulture);

            await SyncTimeZoneAsync(me.TimeZone, cancellationToken);
            await LoadStreakAsync(cancellationToken);
            await LoadLevelAsync(cancellationToken);
            await LoadUnfinishedQuizAsync(cancellationToken);
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

            // A newer load or a goal change finished first: this response is stale.
            if (version == _streakVersion)
            {
                ApplyStreak(streak);
                reminders.EnsureScheduled();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogStreakFailed(ex, ex.GetType().FullName);
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

    /// <summary>Loads the most recent quiz and shows it only if it is started and not finished. Failures hide the card.</summary>
    private async Task LoadUnfinishedQuizAsync(CancellationToken cancellationToken)
    {
        var version = ++_quizVersion;
        QuizSummaryResponse? unfinished = null;
        try
        {
            var quizzes = await api.GetQuizzesAsync(0, 1, cancellationToken);
            var latest = quizzes.Count > 0 ? quizzes[0] : null;
            if (latest is { FinishedAt: null } && latest.CorrectCount + latest.IncorrectCount > 0)
            {
                unfinished = latest;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogQuizzesFailed(ex, ex.GetType().FullName);
        }

        if (version != _quizVersion)
        {
            return;
        }

        if (unfinished is null)
        {
            HasUnfinishedQuiz = false;
            return;
        }

        _unfinishedQuizId = unfinished.Id;
        UnfinishedQuizTitle = QuizTitles.For(unfinished);
        UnfinishedQuizProgressText = string.Create(
            CultureInfo.InvariantCulture,
            $"{unfinished.CorrectCount + unfinished.IncorrectCount} de {unfinished.QuestionCount} respondidas");
        HasUnfinishedQuiz = true;
    }

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

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            // API data first, while the ID token is still valid; then the Firebase account.
            await api.DeleteMeAsync(cancellationToken);
            await auth.DeleteFirebaseAccountAsync(cancellationToken);
            reminders.OnSignedOut();
            await navigation.GoToAsync(Routes.Root(Routes.Login));
        }
        catch (FirebaseAuthException ex) when (ex.Code == "CREDENTIAL_TOO_OLD_LOGIN_AGAIN")
        {
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

    [RelayCommand]
    private Task StartQuizAsync() => navigation.GoToAsync(Routes.Exams);

    [RelayCommand]
    private Task OpenStyleGuideAsync() => navigation.GoToAsync(Routes.StyleGuide);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not sync the time zone ({ExceptionType})")]
    private partial void LogTimeZoneSyncFailed(Exception exception, string? exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not load the streak ({ExceptionType})")]
    private partial void LogStreakFailed(Exception exception, string? exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not load the last quiz ({ExceptionType})")]
    private partial void LogQuizzesFailed(Exception exception, string? exceptionType);

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