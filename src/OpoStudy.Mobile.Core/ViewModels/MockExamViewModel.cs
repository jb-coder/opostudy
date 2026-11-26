using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Extensions.Logging;

using OpoStudy.Contracts.MockExams;
using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.MockExams;
using OpoStudy.Mobile.Core.Navigation;
using OpoStudy.Mobile.Core.Services;

using Refit;

namespace OpoStudy.Mobile.Core.ViewModels;

/// <summary>An answer option of a mock exam question. The chosen one is marked, but never graded until the exam is submitted.</summary>
public sealed partial class MockExamOptionItem(Guid id, string letter, string text) : ObservableObject
{
    public Guid Id { get; } = id;

    public string Letter { get; } = letter;

    public string Text { get; } = text;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateText))]
    [NotifyPropertyChangedFor(nameof(HasStateText))]
    [NotifyPropertyChangedFor(nameof(AccessibilityText))]
    public partial bool IsSelected { get; set; }

    public string StateText => IsSelected ? "Seleccionada" : string.Empty;

    public bool HasStateText => IsSelected;

    public string AccessibilityText => IsSelected ? $"Opción {Letter}: {Text}. Seleccionada" : $"Opción {Letter}: {Text}";
}

/// <summary>A question of a mock exam, with the option chosen so far.</summary>
public sealed partial class MockExamQuestionItem : ObservableObject
{
    public MockExamQuestionItem(MockExamQuestionResponse question, int number)
    {
        ArgumentNullException.ThrowIfNull(question);

        Id = question.QuestionId;
        Number = number;
        Statement = question.Statement;
        Options = [.. question.Options.OrderBy(o => o.Order).Select((o, i) => new MockExamOptionItem(o.Id, ((char)('A' + i)).ToString(), o.Text))];
    }

    public Guid Id { get; }

    public int Number { get; }

    public string Statement { get; }

    public IReadOnlyList<MockExamOptionItem> Options { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAnswered))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(AnsweredMark))]
    [NotifyPropertyChangedFor(nameof(IndexDescription))]
    public partial Guid? SelectedOptionId { get; set; }

    [ObservableProperty]
    public partial bool IsCurrent { get; set; }

    public bool IsAnswered => SelectedOptionId is not null;

    public string StatusText => IsAnswered ? "respondida" : "en blanco";

    /// <summary>Visible mark of an answered question in the index (the state is not only a color).</summary>
    public string AnsweredMark => IsAnswered ? "✓" : "–";

    public string NumberText => Number.ToString(CultureInfo.InvariantCulture);

    /// <summary>What the screen reader says for the index button: the state is text, not only a color.</summary>
    public string IndexDescription => $"Pregunta {Number}, {StatusText}";

    public void Select(Guid optionId)
    {
        SelectedOptionId = optionId;
        foreach (var option in Options)
        {
            option.IsSelected = option.Id == optionId;
        }
    }

    public void Clear()
    {
        SelectedOptionId = null;
        foreach (var option in Options)
        {
            option.IsSelected = false;
        }
    }
}

/// <summary>The correction of one question once the mock exam is submitted.</summary>
public sealed record MockExamResultLine(
    int Number,
    string Statement,
    string Outcome,
    string OutcomeText,
    string YourAnswerText,
    string CorrectAnswerText,
    bool ShowDetails,
    string Explanation,
    string ReferenceText)
{
    public bool IsCorrect => Outcome == "Correct";

    public bool IsIncorrect => Outcome == "Incorrect";

    public string NumberedStatement => $"{Number}. {Statement}";

    public string AccessibilityText => $"Pregunta {Number}: {OutcomeText}. {Statement}";
}

/// <summary>A timed mock exam: answer, change or leave questions blank, and submit once (or when the time is up).</summary>
public sealed partial class MockExamViewModel(
    IOpoStudyApi api,
    INavigationService navigation,
    IDialogService dialogs,
    IScreenReaderAnnouncer announcer,
    TimeProvider timeProvider,
    ILogger<MockExamViewModel> logger) : ViewModelBase, IQueryReceiver, IDisposable
{
    private const string MissingExerciseMessage = "No se ha podido abrir el ejercicio. Vuelve atrás e inténtalo de nuevo.";

    private static readonly TimeSpan LowTime = TimeSpan.FromMinutes(5);

    private static readonly int[] AnnouncementMinutes = [10, 5, 1];

    private readonly HashSet<int> _announced = [];

    private Guid _examId;

    private int _exerciseNumber;

    private decimal? _passScore;

    private Guid _mockExamId;

    private int _durationMinutes;

    private decimal _penaltyRatio;

    private DateTimeOffset _deadline;

    private ITimer? _timer;

    private bool _createStarted;

    private bool _isLeaving;

    private int _expired;

    public ObservableCollection<MockExamQuestionItem> Questions { get; } = [];

    [ObservableProperty]
    public partial string Title { get; set; } = MockExamTexts.ScreenTitle;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowLoadError))]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAnswer))]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAnswer))]
    public partial bool IsSubmitting { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowExam))]
    public partial bool IsFinished { get; set; }

    /// <summary>The creation, question and submission view; the result has its own scroll so it opens at the top.</summary>
    public bool ShowExam => !IsFinished;

    /// <summary>The time is up: the answers are blocked.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAnswer))]
    public partial bool IsLocked { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowLoadError))]
    public partial bool LoadFailed { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSubmitError))]
    public partial bool SubmitFailed { get; set; }

    public bool CanAnswer => IsRunning && !IsLocked && !IsSubmitting;

    public bool ShowLoadError => LoadFailed && !IsLoading;

    public bool ShowSubmitError => SubmitFailed && IsRunning;

    [ObservableProperty]
    public partial string RemainingText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RemainingDescription { get; set; } = string.Empty;

    /// <summary>Five minutes or less are left. The screen also shows <see cref="MockExamTexts.TimeLow"/>, so it does not rely on color.</summary>
    [ObservableProperty]
    public partial bool IsTimeLow { get; set; }

    public string TimeLowText => IsTimeLow ? MockExamTexts.TimeLow : string.Empty;

    [ObservableProperty]
    public partial MockExamQuestionItem? CurrentQuestion { get; set; }

    [ObservableProperty]
    public partial int CurrentIndex { get; set; }

    [ObservableProperty]
    public partial string ProgressText { get; set; } = string.Empty;

    public bool CanGoPrevious => CurrentIndex > 0;

    public bool CanGoNext => CurrentIndex < Questions.Count - 1;

    // ---- Result ----

    [ObservableProperty]
    public partial string ScoreText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsPassed { get; set; }

    [ObservableProperty]
    public partial string PassedText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PassScoreText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string BreakdownText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NetText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TimeText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsLate { get; set; }

    public string LateText => IsLate ? "Entregado fuera de tiempo" : string.Empty;

    [ObservableProperty]
    public partial string XpText { get; set; } = string.Empty;

    public bool HasXpText => XpText.Length > 0;

    [ObservableProperty]
    public partial IReadOnlyList<MockExamResultLine> ResultLines { get; set; } = [];

    public void ApplyQuery(IReadOnlyDictionary<string, object> query)
    {
        if (QueryValues.ReadGuid(query, QueryKeys.ExamId) is { } examId)
        {
            _examId = examId;
        }

        if (query.TryGetValue(QueryKeys.ExerciseNumber, out var number))
        {
            _exerciseNumber = number switch
            {
                int value => value,
                string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => _exerciseNumber,
            };
        }

        if (query.TryGetValue(QueryKeys.PassScore, out var score))
        {
            _passScore = score switch
            {
                decimal value => value,
                double value => (decimal)value,
                string text when decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => _passScore,
            };
        }
    }

    public override async Task OnAppearingAsync()
    {
        if (_createStarted)
        {
            // The page came back (e.g. after a dialog): the timer was stopped when it went away.
            if (IsRunning && !IsLocked && _timer is null)
            {
                StartTimer();
            }

            return;
        }

        _createStarted = true;
        await CreateAsync();
    }

    public override void OnDisappearing()
    {
        StopTimer();
        base.OnDisappearing();
    }

    public void Dispose()
    {
        StopTimer();
        GC.SuppressFinalize(this);
    }

    partial void OnIsRunningChanged(bool value) => RefreshCommands();

    partial void OnIsSubmittingChanged(bool value) => RefreshCommands();

    partial void OnIsLockedChanged(bool value) => RefreshCommands();

    partial void OnSubmitFailedChanged(bool value) => RefreshCommands();

    partial void OnCurrentIndexChanged(int value)
    {
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
        RefreshCommands();
    }

    partial void OnXpTextChanged(string value) => OnPropertyChanged(nameof(HasXpText));

    partial void OnIsTimeLowChanged(bool value) => OnPropertyChanged(nameof(TimeLowText));

    partial void OnIsLateChanged(bool value) => OnPropertyChanged(nameof(LateText));

    private void RefreshCommands()
    {
        SelectOptionCommand.NotifyCanExecuteChanged();
        ClearAnswerCommand.NotifyCanExecuteChanged();
        SubmitCommand.NotifyCanExecuteChanged();
        RetrySubmitCommand.NotifyCanExecuteChanged();
        PreviousCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
    }

    // ---- Creation ----

    private async Task CreateAsync()
    {
        ErrorMessage = null;
        LoadFailed = false;
        if (_examId == Guid.Empty || _exerciseNumber < 1)
        {
            ErrorMessage = MissingExerciseMessage;
            LoadFailed = true;
            return;
        }

        IsLoading = true;
        try
        {
            var exam = await api.CreateMockExamAsync(new CreateMockExamRequest(_examId, _exerciseNumber, _passScore), PageToken);
            Apply(exam);
            IsRunning = true;
            StartTimer();
        }
        catch (OperationCanceledException)
        {
            // The user left before the exam was created.
        }
        catch (Exception ex)
        {
            LogCreateFailed(ex, ex.GetType().FullName);
            ErrorMessage = ApiErrorMessages.From(ex);
            LoadFailed = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void Apply(MockExamResponse exam)
    {
        _mockExamId = exam.Id;
        _durationMinutes = exam.DurationMinutes;
        _penaltyRatio = exam.PenaltyRatio;
        Title = exam.Title;

        // The deadline is local: it does not depend on the server clock and keeps running in the background.
        _deadline = timeProvider.GetUtcNow() + TimeSpan.FromMinutes(exam.DurationMinutes);
        RemainingText = MockExamTexts.Clock(TimeSpan.FromMinutes(exam.DurationMinutes));
        RemainingDescription = MockExamTexts.RemainingDescription(TimeSpan.FromMinutes(exam.DurationMinutes));

        Questions.Clear();
        var number = 1;
        foreach (var question in exam.Questions.OrderBy(q => q.Order))
        {
            Questions.Add(new MockExamQuestionItem(question, number++));
        }

        GoTo(0);
    }

    // ---- Timer ----

    private void StartTimer()
    {
        StopTimer();

        // The timer fires on a thread-pool thread. Ticks are posted back to the context that started it
        // (the UI thread in the app), because they change bound properties, command states and collections.
        // Without a context (unit tests) they run inline.
        var context = SynchronizationContext.Current;
        _timer = timeProvider.CreateTimer(
            _ =>
            {
                if (context is null)
                {
                    Tick();
                }
                else
                {
                    context.Post(_ => Tick(), null);
                }
            },
            null,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1));
    }

    private void StopTimer()
    {
        var timer = _timer;
        _timer = null;
        timer?.Dispose();
    }

    private void Tick()
    {
        if (!IsRunning || IsFinished)
        {
            return;
        }

        var remaining = _deadline - timeProvider.GetUtcNow();
        if (remaining <= TimeSpan.Zero)
        {
            Expire();
            return;
        }

        RemainingText = MockExamTexts.Clock(remaining);
        RemainingDescription = MockExamTexts.RemainingDescription(remaining);
        IsTimeLow = remaining <= LowTime;
        AnnounceThresholds(remaining);
    }

    /// <summary>Announces 10, 5 and 1 minutes left, once each. If several are crossed at once, only the closest one is said.</summary>
    private void AnnounceThresholds(TimeSpan remaining)
    {
        int? toAnnounce = null;
        foreach (var minutes in AnnouncementMinutes)
        {
            if (remaining <= TimeSpan.FromMinutes(minutes) && minutes < _durationMinutes && _announced.Add(minutes))
            {
                toAnnounce = minutes;
            }
        }

        if (toAnnounce is { } value)
        {
            announcer.Announce(MockExamTexts.Announcement(value));
        }
    }

    private void Expire()
    {
        if (Interlocked.Exchange(ref _expired, 1) == 1)
        {
            return;
        }

        StopTimer();
        RemainingText = MockExamTexts.Clock(TimeSpan.Zero);
        RemainingDescription = MockExamTexts.RemainingDescription(TimeSpan.Zero);
        IsTimeLow = true;
        IsLocked = true;
        announcer.Announce(MockExamTexts.TimeUp);
        _ = SubmitCoreAsync();
    }

    // ---- Answering ----

    private bool CanSelect(MockExamOptionItem? option) => CanAnswer && option is not null;

    [RelayCommand(CanExecute = nameof(CanSelect))]
    private void SelectOption(MockExamOptionItem option)
    {
        ArgumentNullException.ThrowIfNull(option);

        CurrentQuestion?.Select(option.Id);
        UpdateProgress();
    }

    private bool CanClear() => CanAnswer && CurrentQuestion?.IsAnswered == true;

    [RelayCommand(CanExecute = nameof(CanClear))]
    private void ClearAnswer()
    {
        CurrentQuestion?.Clear();
        UpdateProgress();
    }

    private void UpdateProgress()
    {
        var answered = Questions.Count(q => q.IsAnswered);
        ProgressText = MockExamTexts.Progress(CurrentIndex + 1, Questions.Count, answered);
        ClearAnswerCommand.NotifyCanExecuteChanged();
    }

    private void GoTo(int index)
    {
        if (index < 0 || index >= Questions.Count)
        {
            return;
        }

        foreach (var question in Questions)
        {
            question.IsCurrent = false;
        }

        CurrentQuestion = Questions[index];
        CurrentQuestion.IsCurrent = true;
        CurrentIndex = index;
        UpdateProgress();
    }

    private bool CanPrevious() => IsRunning && CanGoPrevious;

    [RelayCommand(CanExecute = nameof(CanPrevious))]
    private void Previous() => GoTo(CurrentIndex - 1);

    private bool CanNext() => IsRunning && CanGoNext;

    [RelayCommand(CanExecute = nameof(CanNext))]
    private void Next() => GoTo(CurrentIndex + 1);

    [RelayCommand]
    private void GoToQuestion(MockExamQuestionItem question)
    {
        ArgumentNullException.ThrowIfNull(question);

        if (IsRunning)
        {
            GoTo(Questions.IndexOf(question));
        }
    }

    // ---- Submission ----

    private bool CanSubmit() => IsRunning && !IsSubmitting && !IsLocked;

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task SubmitAsync()
    {
        var blanks = Questions.Count(q => !q.IsAnswered);
        var confirmed = await dialogs.ConfirmAsync("Entregar", MockExamTexts.SubmitConfirmation(blanks), "Entregar", "Seguir");
        if (confirmed)
        {
            await SubmitCoreAsync();
        }
    }

    private bool CanRetrySubmit() => IsRunning && SubmitFailed && !IsSubmitting;

    [RelayCommand(CanExecute = nameof(CanRetrySubmit))]
    private Task RetrySubmitAsync() => SubmitCoreAsync();

    /// <summary>Sends the answered questions. The answers are kept if it fails, so it can be retried.</summary>
    private async Task SubmitCoreAsync()
    {
        if (IsSubmitting || IsFinished || !IsRunning)
        {
            return;
        }

        IsSubmitting = true;
        ErrorMessage = null;
        SubmitFailed = false;
        try
        {
            var answers = Questions
                .Where(q => q.SelectedOptionId is not null)
                .Select(q => new MockExamAnswer(q.Id, q.SelectedOptionId!.Value))
                .ToList();
            try
            {
                var response = await api.SubmitMockExamAsync(_mockExamId, new SubmitMockExamRequest(answers), CancellationToken.None);
                ShowResult(response.Result, response.XpGained);
            }
            catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                // Already submitted: the previous response was lost. Read the result instead.
                var result = await api.GetMockExamResultAsync(_mockExamId, CancellationToken.None);
                ShowResult(result, null);
            }
        }
        catch (Exception ex)
        {
            LogSubmitFailed(ex, ex.GetType().FullName);
            ErrorMessage = ApiErrorMessages.From(ex);
            SubmitFailed = true;
            announcer.Announce(ErrorMessage);
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    private void ShowResult(MockExamResultResponse result, int? xpGained)
    {
        StopTimer();
        IsTimeLow = false;
        ScoreText = MockExamTexts.ScoreLine(result.Score, result.MaxScore);
        IsPassed = result.Passed;
        PassedText = result.Passed ? "Apto" : "No apto";
        PassScoreText = $"Nota para aprobar: {MockExamTexts.Number(result.PassScore)}";
        BreakdownText = MockExamTexts.Breakdown(result.CorrectCount, result.IncorrectCount, result.BlankCount);
        NetText = MockExamTexts.NetCalculation(result.CorrectCount, result.IncorrectCount, result.PenaltyRatio);
        // Network latency can put FinishedAt a few seconds past the limit; only a late submission shows more than the limit.
        var duration = TimeSpan.FromMinutes(_durationMinutes);
        var used = TimeSpan.FromTicks(Math.Max(0, (result.FinishedAt - result.StartedAt).Ticks));
        if (!result.SubmittedLate && used > duration)
        {
            used = duration;
        }

        TimeText = MockExamTexts.TimeUsed(used, _durationMinutes);
        IsLate = result.SubmittedLate;
        XpText = xpGained is > 0 ? $"+{xpGained} XP" : string.Empty;
        ResultLines = [.. result.Items.OrderBy(i => i.Order).Select(ToLine)];
        Title = result.Title;

        IsRunning = false;
        IsFinished = true;
        announcer.Announce($"Simulacro entregado. Nota {ScoreText}. {PassedText}.");
    }

    private static MockExamResultLine ToLine(MockExamResultItem item)
    {
        var options = item.Options.OrderBy(o => o.Order).Select((o, i) => (o.Id, Letter: ((char)('A' + i)).ToString(), o.Text)).ToList();
        string Describe(Guid id)
        {
            var option = options.FirstOrDefault(o => o.Id == id);
            return option.Letter is null ? string.Empty : $"{option.Letter}. {option.Text}";
        }

        var yours = item.SelectedOptionId is { } selected ? $"Tu respuesta: {Describe(selected)}" : "Sin respuesta";
        return new MockExamResultLine(
            item.Order + 1,
            item.Statement,
            item.Outcome,
            MockExamTexts.Outcome(item.Outcome),
            yours,
            $"Respuesta correcta: {Describe(item.CorrectOptionId)}",
            item.Outcome != "Correct",
            item.Explanation,
            $"Fuente: {item.LegalReference}");
    }

    // ---- Leaving ----

    /// <summary>Back button (toolbar and Android): asks first while the mock exam is in progress.</summary>
    [RelayCommand]
    private async Task ExitAsync()
    {
        // A double tap must not open two dialogs.
        if (_isLeaving)
        {
            return;
        }

        _isLeaving = true;
        try
        {
            if (IsRunning)
            {
                var leave = await dialogs.ConfirmAsync(
                    "¿Salir del simulacro?",
                    "No se guardará y no contará.",
                    "Salir",
                    "Seguir");
                if (!leave)
                {
                    return;
                }
            }

            StopTimer();
            await navigation.GoToAsync(Routes.Back);
        }
        finally
        {
            _isLeaving = false;
        }
    }

    /// <summary>"Hacer otro simulacro": back to the list of exercises.</summary>
    [RelayCommand]
    private Task AnotherAsync() => navigation.GoToAsync(Routes.Back);

    [RelayCommand]
    private Task GoHomeAsync() => navigation.GoToAsync(Routes.Root(Routes.Home));

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not create the mock exam ({ExceptionType})")]
    private partial void LogCreateFailed(Exception exception, string? exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not submit the mock exam ({ExceptionType})")]
    private partial void LogSubmitFailed(Exception exception, string? exceptionType);
}