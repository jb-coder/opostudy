using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Extensions.Logging;

using OpoStudy.Contracts.Quizzes;
using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.Navigation;

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
    ILogger<QuizViewModel> logger) : ViewModelBase, IQueryReceiver
{
    private const string MissingQuizMessage = "No se ha podido abrir el test. Vuelve atrás e inténtalo de nuevo.";

    private Guid _quizId;

    private List<QuestionState> _questions = [];

    private int _index;

    private int _correctCount;

    private int _incorrectCount;

    public ObservableCollection<QuizOptionItem> Options { get; } = [];

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

    [ObservableProperty]
    public partial string NextButtonText { get; set; } = "Siguiente pregunta";

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
            ApplyQuiz(await api.GetQuizAsync(_quizId, cancellationToken));
            ShowFirstPending();
            IsLoaded = true;
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
        try
        {
            var response = await api.SubmitAnswerAsync(_quizId, new SubmitAnswerRequest(question.QuestionId, option.Id), cancellationToken);
            question.Feedback = response.Feedback;
            _correctCount = response.CorrectCount;
            _incorrectCount = response.IncorrectCount;
            ShowFeedback(response.Feedback);
            UpdateProgress();
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            await ResyncAsync(question.QuestionId, cancellationToken);
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

    private bool CanAnswer(QuizOptionItem? option) => IsNotBusy && !IsAnswered;

    [RelayCommand]
    private void Next()
    {
        ErrorMessage = null;
        var next = FindNextPending(_index);
        if (next < 0)
        {
            ShowSummary();
            return;
        }

        ShowQuestion(next);
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
        _questions = [.. quiz.Questions.OrderBy(q => q.Order).Select(q => new QuestionState(q))];
        _correctCount = quiz.CorrectCount;
        _incorrectCount = quiz.IncorrectCount;
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
        Progress = 1;
        ScoreText = FormatScore(_correctCount, _incorrectCount);
        IsFinished = true;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not load the quiz ({ExceptionType})")]
    private partial void LogLoadFailed(Exception exception, string? exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not submit the answer ({ExceptionType})")]
    private partial void LogAnswerFailed(Exception exception, string? exceptionType);

    private sealed class QuestionState(QuizQuestionResponse source)
    {
        public Guid QuestionId { get; } = source.QuestionId;

        public string Statement { get; } = source.Statement;

        public IReadOnlyList<QuizOptionResponse> Options { get; } = source.Options;

        public AnswerFeedback? Feedback { get; set; } = source.Feedback;
    }
}