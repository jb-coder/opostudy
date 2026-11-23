using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Extensions.Logging;

using OpoStudy.Contracts.Quizzes;
using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.Navigation;
using OpoStudy.Mobile.Core.Offline;
using OpoStudy.Mobile.Core.Reminders;
using OpoStudy.Mobile.Core.Services;

using Refit;

namespace OpoStudy.Mobile.Core.ViewModels;

/// <summary>Visual state of an answer option.</summary>
public enum OptionState
{
    Neutral,
    Correct,
    Incorrect,
    Dimmed,
}

/// <summary>An answer option of the current question.</summary>
public sealed partial class QuizOptionItem(Guid id, string letter, string text) : ObservableObject
{
    public Guid Id { get; } = id;

    public string Letter { get; } = letter;

    public string Text { get; } = text;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateText))]
    [NotifyPropertyChangedFor(nameof(HasStateText))]
    public partial OptionState State { get; set; }

    public string StateText => State switch
    {
        OptionState.Correct => "Correcta",
        OptionState.Incorrect => "Tu respuesta: incorrecta",
        _ => string.Empty,
    };

    public bool HasStateText => StateText.Length > 0;

    public string AccessibilityText => $"Opción {Letter}: {Text}";
}

/// <summary>A line of the final summary.</summary>
public sealed record SummaryItem(
    int Number,
    string Statement,
    bool IsCorrect,
    string Explanation,
    string LegalReference)
{
    public string ResultText => IsCorrect ? "Acertada" : "Fallada";

    public string NumberedStatement => $"{Number}. {Statement}";

    public string ReferenceText => $"Referencia: {LegalReference}";

    /// <summary>Failed questions show their explanation and legal reference.</summary>
    public bool ShowDetails => !IsCorrect;
}

/// <summary>Quiz screen: one question at a time with immediate feedback, then a summary.</summary>
public sealed partial class QuizViewModel(
    IOpoStudyApi api,
    INavigationService navigation,
    IDialogService dialogs,
    ReminderService reminders,
    IOfflineQuizService offlineQuizzes,
    IOfflineSynchronizer synchronizer,
    OfflineState offlineState,
    ILogger<QuizViewModel> logger) : OfflineAwareViewModel(offlineState), IQueryReceiver
{
    /// <summary>Shown instead of the XP while the answer waits to be synchronized.</summary>
    public const string PendingXpText = "XP pendiente de sincronizar";

    /// <summary>Shown in the summary instead of the XP total of an offline quiz.</summary>
    public const string PendingSyncText = "Tus respuestas se sincronizarán cuando haya conexión.";

    private const string MissingQuizMessage = "No se ha podido abrir el test. Vuelve atrás e inténtalo de nuevo.";

    private const string AnotherQuizTitle = "Hacer otro test";

    private Guid _quizId;

    private Guid _examId;

    private Guid? _topicId;

    private bool _isReview;

    private bool _isLeaving;

    private List<QuestionState> _questions = [];

    private int _index;

    private int _correctCount;
    private int _quizXp;

    private int _incorrectCount;

    /// <summary>The quiz as the server sent it, needed to adopt it locally if the connection drops.</summary>
    private QuizResponse? _quiz;

    public ObservableCollection<QuizOptionItem> Options { get; } = [];

    /// <summary>
    /// Whether the quiz is corrected with the local copy. Once true it stays true until the quiz is left,
    /// so the answers of one quiz are never split between the server and the queue.
    /// </summary>
    [ObservableProperty]
    public partial bool IsOffline { get; set; }

    public override bool ShowOfflineBanner => IsOffline || base.ShowOfflineBanner;

    public override string OfflineBannerText => IsOffline ? OfflineState.BannerText : base.OfflineBannerText;

    partial void OnIsOfflineChanged(bool value) => RaiseOfflineChanged();

    [ObservableProperty]
    public partial string QuizTitle { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsQuestionVisible))]
    [NotifyPropertyChangedFor(nameof(ShowLoadError))]
    public partial bool IsLoaded { get; set; }

    /// <summary>Whether the quiz itself is being loaded (not while an answer is sent).</summary>
    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsQuestionVisible))]
    public partial bool IsFinished { get; set; }

    [ObservableProperty]
    public partial string ProgressText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double Progress { get; set; }

    [ObservableProperty]
    public partial string ScoreText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CurrentStatement { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AnswerCommand))]
    public partial bool IsAnswered { get; set; }

    [ObservableProperty]
    public partial bool IsCorrect { get; set; }

    [ObservableProperty]
    public partial string FeedbackTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Explanation { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LegalReferenceText { get; set; } = string.Empty;

    /// <summary>Non-blocking notice shown when the answer just sent met the daily goal.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGoalNotice))]
    public partial string GoalNotice { get; set; } = string.Empty;

    public bool HasGoalNotice => GoalNotice.Length > 0;

    /// <summary>XP label of the answer shown (e.g. "+10 XP"); empty when the XP is unknown (answer from an earlier session).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasXpText))]
    public partial string XpText { get; set; } = string.Empty;

    public bool HasXpText => XpText.Length > 0;

    /// <summary>XP earned by the answers given on this screen, shown in the summary.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasQuizXpText))]
    public partial string QuizXpText { get; set; } = string.Empty;

    /// <summary>False when no XP was earned on this screen (e.g. a resumed quiz with no new answers).</summary>
    public bool HasQuizXpText => QuizXpText.Length > 0;

    [ObservableProperty]
    public partial string NextButtonText { get; set; } = "Siguiente pregunta";

    /// <summary>Text of the first summary button: "Seguir repasando" when a review still has mistakes left.</summary>
    [ObservableProperty]
    public partial string AnotherQuizText { get; set; } = AnotherQuizTitle;

    /// <summary>Mistakes left after a review (summary only); empty when unknown.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMistakesLeftText))]
    public partial string MistakesLeftText { get; set; } = string.Empty;

    public bool HasMistakesLeftText => MistakesLeftText.Length > 0;

    [ObservableProperty]
    public partial string ResultText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PercentageText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<SummaryItem> SummaryItems { get; set; } = [];

    /// <summary>Whether the question view (as opposed to the loading state or the summary) is shown.</summary>
    public bool IsQuestionVisible => IsLoaded && !IsFinished;

    /// <summary>Whether the quiz could not be loaded (error with retry).</summary>
    public bool ShowLoadError => HasError && !IsLoaded;

    public void ApplyQuery(IReadOnlyDictionary<string, object> query)
    {
        if (query.TryGetValue(QueryKeys.QuizId, out var id))
        {
            _quizId = id switch
            {
                Guid guid => guid,
                string text when Guid.TryParse(text, out var parsed) => parsed,
                _ => _quizId,
            };
        }

        if (query.TryGetValue(QueryKeys.QuizTitle, out var title) && title is string quizTitle)
        {
            QuizTitle = quizTitle;
        }
    }

    public override async Task OnAppearingAsync()
    {
        ListenToOfflineState();
        if (IsLoaded)
        {
            return;
        }

        if (LoadCommand.CanExecute(null))
        {
            await LoadCommand.ExecuteAsync(null);
        }
    }

    protected override void OnErrorMessageChanged() => OnPropertyChanged(nameof(ShowLoadError));

    protected override void OnBusyChanged()
    {
        LoadCommand.NotifyCanExecuteChanged();
        AnswerCommand.NotifyCanExecuteChanged();
        AnotherQuizCommand.NotifyCanExecuteChanged();
        GoHomeCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        if (_quizId == Guid.Empty)
        {
            ErrorMessage = MissingQuizMessage;
            return;
        }

        IsBusy = true;
        IsLoading = true;
        try
        {
            // A quiz created or adopted offline is always opened locally: the server does not know it (or not all of it).
            var local = await FindLocalQuizAsync(cancellationToken);
            if (local is not null)
            {
                IsOffline = true;
                ApplyQuiz(local);
            }
            else
            {
                try
                {
                    ApplyQuiz(await api.GetQuizAsync(_quizId, cancellationToken));
                    OfflineState.ReportSuccess();
                }
                catch (Exception ex) when (ConnectivityErrors.IsConnectivityError(ex, cancellationToken))
                {
                    OfflineState.ReportConnectivityFailure();
                    throw;
                }
            }

            ShowFirstPending();
            IsLoaded = true;
            if (IsFinished)
            {
                await LoadMistakesLeftAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            LogLoadFailed(ex, ex.GetType().FullName);
            ErrorMessage = ApiErrorMessages.From(ex);
        }
        finally
        {
            IsLoading = false;
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanAnswer))]
    private async Task AnswerAsync(QuizOptionItem option, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(option);

        var question = _questions[_index];
        IsBusy = true;
        ErrorMessage = null;
        GoalNotice = string.Empty;
        try
        {
            if (IsOffline)
            {
                await AnswerOfflineAsync(question, option, cancellationToken);
                return;
            }

            var response = await api.SubmitAnswerAsync(_quizId, new SubmitAnswerRequest(question.QuestionId, option.Id), cancellationToken);
            OfflineState.ReportSuccess();
            question.Feedback = response.Feedback;
            question.XpText = LevelCard.AnswerXpText(response.Xp);
            XpText = question.XpText;
            _quizXp += response.Xp.Gained;
            _correctCount = response.CorrectCount;
            _incorrectCount = response.IncorrectCount;
            ShowFeedback(response.Feedback);
            UpdateProgress();
            reminders.UpdateFromProgress(response.DailyProgress);
            GoalNotice = LevelCard.Notice(response.DailyProgress, response.Xp);
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            await ResyncAsync(question.QuestionId, cancellationToken);
        }
        catch (Exception ex) when (!IsOffline && ConnectivityErrors.IsConnectivityError(ex, cancellationToken))
        {
            // The connection dropped in the middle of the quiz: from here on it is corrected locally.
            LogAnswerFailed(ex, ex.GetType().FullName);
            OfflineState.ReportConnectivityFailure();
            try
            {
                await AdoptCurrentQuizAsync(cancellationToken);
                await AnswerOfflineAsync(question, option, cancellationToken);
            }
            catch (Exception inner) when (inner is not OperationCanceledException)
            {
                LogAnswerFailed(inner, inner.GetType().FullName);
                ErrorMessage = ApiErrorMessages.From(inner);
            }
        }
        catch (Exception ex)
        {
            LogAnswerFailed(ex, ex.GetType().FullName);
            ErrorMessage = ApiErrorMessages.From(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>The local copy of the quiz, or null when it is not stored (a normal online quiz) or cannot be read.</summary>
    private async Task<QuizResponse?> FindLocalQuizAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await offlineQuizzes.GetAsync(_quizId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogLoadFailed(ex, ex.GetType().FullName);
            return null;
        }
    }

    /// <summary>Stores the quiz, with the answers given so far, in the local database. Adopting twice does nothing.</summary>
    private async Task AdoptCurrentQuizAsync(CancellationToken cancellationToken)
    {
        if (_quiz is not { } quiz)
        {
            throw new InvalidOperationException("The quiz has not been loaded.");
        }

        var current = quiz with
        {
            CorrectCount = _correctCount,
            IncorrectCount = _incorrectCount,
            Questions = [.. _questions.Select(q => q.ToResponse())],
        };
        await offlineQuizzes.AdoptAsync(current, QuizTitle, cancellationToken);
    }

    private async Task AnswerOfflineAsync(QuestionState question, QuizOptionItem option, CancellationToken cancellationToken)
    {
        var result = await offlineQuizzes.AnswerAsync(_quizId, question.QuestionId, option.Id, cancellationToken);
        IsOffline = true;
        question.Feedback = result.Feedback;
        question.XpText = PendingXpText;
        XpText = PendingXpText;
        _correctCount = result.CorrectCount;
        _incorrectCount = result.IncorrectCount;
        ShowFeedback(result.Feedback);
        UpdateProgress();
        if (result.QuizFinished && OfflineState.HasInternet)
        {
            // Not awaited: the user should not wait for the server to see the result.
            _ = synchronizer.SyncAsync(CancellationToken.None);
        }
    }

    private bool CanAnswer(QuizOptionItem? option) => IsNotBusy && !IsAnswered;

    [RelayCommand]
    private async Task NextAsync()
    {
        ErrorMessage = null;
        var next = FindNextPending(_index);
        if (next < 0)
        {
            ShowSummary();
            try
            {
                await LoadMistakesLeftAsync(PageToken);
            }
            catch (OperationCanceledException)
            {
                // The page went away before the mistakes arrived.
            }

            return;
        }

        ShowQuestion(next);
    }

    /// <summary>Back button (toolbar and Android): asks first when the quiz is started and not finished.</summary>
    [RelayCommand]
    private async Task BackAsync()
    {
        // A double tap must not open two dialogs (ExecuteAsync ignores AllowConcurrentExecutions).
        if (_isLeaving)
        {
            return;
        }

        _isLeaving = true;
        try
        {
            var started = !IsFinished && _questions.Any(q => q.Feedback is not null);
            if (started)
            {
                var leave = await dialogs.ConfirmAsync(
                    "¿Salir del test?",
                    "Tus respuestas se guardan y podrás continuarlo desde el inicio.",
                    "Salir",
                    "Seguir con el test");
                if (!leave)
                {
                    return;
                }
            }

            await navigation.GoToAsync(Routes.Back);
        }
        finally
        {
            _isLeaving = false;
        }
    }

    /// <summary>After a review, shows how many mistakes are left. A failure only omits the line.</summary>
    private async Task LoadMistakesLeftAsync(CancellationToken cancellationToken)
    {
        MistakesLeftText = string.Empty;
        AnotherQuizText = AnotherQuizTitle;
        if (!_isReview || IsOffline)
        {
            return;
        }

        try
        {
            var summary = await api.GetMistakesAsync(_examId, cancellationToken);
            var left = _topicId is { } topicId
                ? summary.Topics.FirstOrDefault(t => t.TopicId == topicId)?.Count ?? 0
                : summary.Total;
            MistakesLeftText = QuizTitles.MistakesLeftText(left);
            AnotherQuizText = left > 0 ? "Seguir repasando" : AnotherQuizTitle;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogMistakesFailed(ex, ex.GetType().FullName);
        }
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private Task AnotherQuizAsync() => NavigateAsync(Routes.Back);

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private Task GoHomeAsync() => NavigateAsync(Routes.Root(Routes.Home));

    /// <summary>Navigates while busy, so a quick tap on the other summary button cannot navigate twice.</summary>
    private async Task NavigateAsync(string route)
    {
        IsBusy = true;
        try
        {
            await navigation.GoToAsync(route);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Reloads the quiz after a conflict (the question was already answered) and keeps going from there.</summary>
    private async Task ResyncAsync(Guid questionId, CancellationToken cancellationToken)
    {
        try
        {
            ApplyQuiz(await api.GetQuizAsync(_quizId, cancellationToken));
            var answeredIndex = _questions.FindIndex(q => q.QuestionId == questionId && q.Feedback is not null);
            if (answeredIndex >= 0)
            {
                ShowQuestion(answeredIndex);
            }
            else
            {
                ShowFirstPending();
                if (IsFinished)
                {
                    await LoadMistakesLeftAsync(cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            LogAnswerFailed(ex, ex.GetType().FullName);
            ErrorMessage = ApiErrorMessages.From(ex);
        }
    }

    private void ApplyQuiz(QuizResponse quiz)
    {
        _quiz = quiz;
        _questions = [.. quiz.Questions.OrderBy(q => q.Order).Select(q => new QuestionState(q))];
        _correctCount = quiz.CorrectCount;
        _incorrectCount = quiz.IncorrectCount;
        _examId = quiz.ExamId;
        _topicId = quiz.TopicId;
        _isReview = quiz.Mode == QuizTitles.ReviewMode;
    }

    private void ShowFirstPending()
    {
        var pending = FindPendingFrom(0);
        if (pending < 0)
        {
            ShowSummary();
            return;
        }

        IsFinished = false;
        ShowQuestion(pending);
    }

    /// <summary>Index of the next unanswered question after <paramref name="index"/>, wrapping around; -1 if none is left.</summary>
    private int FindNextPending(int index)
    {
        var next = FindPendingFrom(index + 1);
        if (next >= 0)
        {
            return next;
        }

        var first = FindPendingFrom(0);
        return first == index ? -1 : first;
    }

    private int FindPendingFrom(int start)
    {
        for (var i = Math.Max(start, 0); i < _questions.Count; i++)
        {
            if (_questions[i].Feedback is null)
            {
                return i;
            }
        }

        return -1;
    }

    private void ShowQuestion(int index)
    {
        GoalNotice = string.Empty;
        _index = index;
        var question = _questions[index];

        CurrentStatement = question.Statement;
        Options.Clear();
        var letter = 'A';
        foreach (var option in question.Options.OrderBy(o => o.Order))
        {
            Options.Add(new QuizOptionItem(option.Id, letter.ToString(), option.Text));
            letter++;
        }

        IsAnswered = false;
        IsCorrect = false;
        FeedbackTitle = string.Empty;
        Explanation = string.Empty;
        LegalReferenceText = string.Empty;
        XpText = question.XpText;
        NextButtonText = FindNextPending(index) < 0 ? "Ver resultado" : "Siguiente pregunta";

        if (question.Feedback is { } feedback)
        {
            ShowFeedback(feedback);
        }

        UpdateProgress();
    }

    private void ShowFeedback(AnswerFeedback feedback)
    {
        foreach (var option in Options)
        {
            option.State = option.Id == feedback.CorrectOptionId
                ? OptionState.Correct
                : option.Id == feedback.SelectedOptionId ? OptionState.Incorrect : OptionState.Dimmed;
        }

        IsCorrect = feedback.IsCorrect;
        FeedbackTitle = feedback.IsCorrect ? "¡Correcto!" : "Incorrecto";
        Explanation = feedback.Explanation;
        LegalReferenceText = $"Referencia: {feedback.LegalReference}";
        IsAnswered = true;
    }

    private void UpdateProgress()
    {
        var total = _questions.Count;
        ProgressText = $"Pregunta {_index + 1} de {total}";
        var answered = _questions.Count(q => q.Feedback is not null);
        Progress = total == 0 ? 0 : (double)answered / total;
        ScoreText = FormatScore(_correctCount, _incorrectCount);
    }

    private static string FormatScore(int correct, int incorrect) =>
        $"{correct} {(correct == 1 ? "acierto" : "aciertos")} · {incorrect} {(incorrect == 1 ? "fallo" : "fallos")}";

    private void ShowSummary()
    {
        var total = _questions.Count;
        var correct = _questions.Count(q => q.Feedback is { IsCorrect: true });
        ResultText = $"{correct} de {total} correctas";
        var percentage = total == 0 ? 0 : (int)Math.Round(correct * 100.0 / total, MidpointRounding.AwayFromZero);
        PercentageText = string.Create(CultureInfo.InvariantCulture, $"{percentage} %");
        SummaryItems =
        [
            .. _questions.Select((q, i) => new SummaryItem(
                i + 1,
                q.Statement,
                q.Feedback?.IsCorrect ?? false,
                q.Feedback?.Explanation ?? string.Empty,
                q.Feedback?.LegalReference ?? string.Empty)),
        ];
        QuizXpText = IsOffline ? PendingSyncText : _quizXp > 0 ? LevelCard.QuizTotalText(_quizXp) : string.Empty;
        Progress = 1;
        ScoreText = FormatScore(_correctCount, _incorrectCount);
        IsFinished = true;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not load the quiz ({ExceptionType})")]
    private partial void LogLoadFailed(Exception exception, string? exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not load the mistakes left ({ExceptionType})")]
    private partial void LogMistakesFailed(Exception exception, string? exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not submit the answer ({ExceptionType})")]
    private partial void LogAnswerFailed(Exception exception, string? exceptionType);

    private sealed class QuestionState(QuizQuestionResponse source)
    {
        public Guid QuestionId { get; } = source.QuestionId;

        public string Statement { get; } = source.Statement;

        public IReadOnlyList<QuizOptionResponse> Options { get; } = source.Options;

        public AnswerFeedback? Feedback { get; set; } = source.Feedback;

        public string XpText { get; set; } = string.Empty;

        /// <summary>The question as the API sends it, with the feedback known now.</summary>
        public QuizQuestionResponse ToResponse() => source with { Feedback = Feedback };
    }
}