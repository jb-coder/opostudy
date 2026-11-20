using System.Collections.ObjectModel;
using System.Net;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Extensions.Logging;

using OpoStudy.Contracts.Exams;
using OpoStudy.Contracts.Quizzes;
using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.Navigation;

namespace OpoStudy.Mobile.Core.ViewModels;

/// <summary>A topic as shown in the list.</summary>
/// <param name="Topic">The topic.</param>
/// <param name="MistakeCount">Questions of the topic the user currently has wrong.</param>
/// <param name="MistakesMode">Whether the list shows the mistakes to review instead of the questions available.</param>
public sealed record TopicItem(TopicResponse Topic, int MistakeCount = 0, bool MistakesMode = false)
{
    public Guid Id => Topic.Id;

    /// <summary>
    /// The content title as is: it already starts with "Tema N." using the official (per-block) numbering,
    /// which differs from the global <see cref="TopicResponse.Number"/>.
    /// </summary>
    public string Title => Topic.Title;

    public string? Section => Topic.Section;

    public bool HasSection => !string.IsNullOrWhiteSpace(Topic.Section);

    public bool IsAvailable => MistakesMode ? MistakeCount > 0 : Topic.QuestionCount > 0;

    public string QuestionCountText => MistakesMode
        ? (MistakeCount == 0 ? "Sin falladas" : QuizTitles.TopicMistakeText(MistakeCount))
        : Topic.QuestionCount switch
        {
            0 => "Sin preguntas todavía",
            1 => "1 pregunta",
            var count => $"{count} preguntas",
        };
}

/// <summary>Topic selection (or random quiz) for an exam.</summary>
public sealed partial class TopicsViewModel(
    IOpoStudyApi api,
    INavigationService navigation,
    ILogger<TopicsViewModel> logger) : ViewModelBase, IQueryReceiver
{
    private const string MissingExamMessage = "No se ha podido abrir la oposición. Vuelve atrás e inténtalo de nuevo.";

    private const string NoMistakesCode = "quiz.no_mistakes";

    private Guid _examId;

    private bool _loaded;

    private IReadOnlyList<TopicResponse> _topics = [];

    private Dictionary<Guid, int> _mistakesByTopic = [];

    public IReadOnlyList<int> QuestionCountOptions { get; } = [10, 20, 30];

    public ObservableCollection<TopicItem> Topics { get; } = [];

    [ObservableProperty]
    public partial int SelectedQuestionCount { get; set; } = 10;

    [ObservableProperty]
    public partial string ExamName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartRandomQuizCommand))]
    public partial bool HasAnyQuestions { get; set; }

    /// <summary>Whether the user wants to review only the questions they have wrong.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RandomCardTitle))]
    [NotifyPropertyChangedFor(nameof(RandomCardSubtitle))]
    [NotifyPropertyChangedFor(nameof(RandomCardDescription))]
    [NotifyPropertyChangedFor(nameof(IsRandomCardAvailable))]
    [NotifyCanExecuteChangedFor(nameof(StartRandomQuizCommand))]
    public partial bool MistakesOnly { get; set; }

    /// <summary>Questions the user currently has wrong in the exam.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanReviewMistakes))]
    [NotifyPropertyChangedFor(nameof(MistakesHint))]
    [NotifyPropertyChangedFor(nameof(HasMistakesHint))]
    [NotifyPropertyChangedFor(nameof(RandomCardSubtitle))]
    [NotifyPropertyChangedFor(nameof(RandomCardDescription))]
    [NotifyPropertyChangedFor(nameof(IsRandomCardAvailable))]
    public partial int MistakeTotal { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanReviewMistakes))]
    [NotifyPropertyChangedFor(nameof(MistakesHint))]
    [NotifyPropertyChangedFor(nameof(HasMistakesHint))]
    public partial bool MistakesLoadFailed { get; set; }

    /// <summary>Whether the "only mistakes" switch can be used.</summary>
    public bool CanReviewMistakes => !MistakesLoadFailed && MistakeTotal > 0;

    /// <summary>Why the switch is disabled; empty while it is usable.</summary>
    public string MistakesHint =>
        MistakesLoadFailed ? "No se han podido cargar tus preguntas falladas."
        : MistakeTotal == 0 ? "Aún no tienes preguntas falladas."
        : string.Empty;

    public bool HasMistakesHint => MistakesHint.Length > 0;

    public string RandomCardTitle => MistakesOnly ? "Repasar todas las falladas" : QuizTitles.RandomQuiz;

    public string RandomCardSubtitle => MistakesOnly ? QuizTitles.MistakeTotalText(MistakeTotal) : "Preguntas de todos los temas";

    public string RandomCardDescription => $"{RandomCardTitle}. {RandomCardSubtitle}";

    public bool IsRandomCardAvailable => MistakesOnly ? MistakeTotal > 0 : HasAnyQuestions;

    public void ApplyQuery(IReadOnlyDictionary<string, object> query)
    {
        if (query.TryGetValue(QueryKeys.ExamId, out var id))
        {
            _examId = id switch
            {
                Guid guid => guid,
                string text when Guid.TryParse(text, out var parsed) => parsed,
                _ => _examId,
            };
        }

        if (query.TryGetValue(QueryKeys.ExamName, out var name) && name is string examName)
        {
            ExamName = examName;
        }
    }

    public override async Task OnAppearingAsync()
    {
        if (!_loaded)
        {
            if (RefreshCommand.CanExecute(null))
            {
                await RefreshCommand.ExecuteAsync(null);
            }

            return;
        }

        // The topics do not change, but the mistakes do after every quiz.
        if (!IsBusy && _examId != Guid.Empty)
        {
            await LoadMistakesAsync(CancellationToken.None);
            RebuildTopics();
        }
    }

    partial void OnHasAnyQuestionsChanged(bool value) => OnPropertyChanged(nameof(IsRandomCardAvailable));

    partial void OnMistakesOnlyChanged(bool value) => RebuildTopics();

    protected override void OnBusyChanged()
    {
        RefreshCommand.NotifyCanExecuteChanged();
        StartRandomQuizCommand.NotifyCanExecuteChanged();
        StartTopicQuizCommand.NotifyCanExecuteChanged();
    }

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
            // Both requests run in parallel; the mistakes one never throws.
            var mistakes = LoadMistakesAsync(cancellationToken);
            try
            {
                _topics = await api.GetTopicsAsync(_examId, cancellationToken);
            }
            finally
            {
                await mistakes;
            }

            _loaded = true;
            IsEmpty = _topics.Count == 0;
            HasAnyQuestions = _topics.Any(t => t.QuestionCount > 0);
            RebuildTopics();
        }
        catch (Exception ex)
        {
            LogLoadFailed(ex, ex.GetType().FullName);
            IsEmpty = false;
            ErrorMessage = ApiErrorMessages.From(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Loads the mistakes of the exam. A failure only disables the switch; it never throws.</summary>
    private async Task LoadMistakesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var summary = await api.GetMistakesAsync(_examId, cancellationToken);
            _mistakesByTopic = summary.Topics.ToDictionary(t => t.TopicId, t => t.Count);
            MistakeTotal = summary.Total;
            MistakesLoadFailed = false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogMistakesFailed(ex, ex.GetType().FullName);
            _mistakesByTopic = [];
            MistakeTotal = 0;
            MistakesLoadFailed = true;
        }

        if (MistakeTotal == 0)
        {
            MistakesOnly = false;
        }
    }

    private void RebuildTopics()
    {
        Topics.Clear();
        foreach (var topic in _topics)
        {
            Topics.Add(new TopicItem(topic, _mistakesByTopic.GetValueOrDefault(topic.Id), MistakesOnly));
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartTopicQuiz))]
    private Task StartTopicQuizAsync(TopicItem topic, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(topic);

        return topic.IsAvailable ? StartQuizAsync(topic.Id, QuizTitles.ForTopic(topic.Title, MistakesOnly), cancellationToken) : Task.CompletedTask;
    }

    private bool CanStartTopicQuiz(TopicItem? topic) => IsNotBusy && topic is { IsAvailable: true };

    [RelayCommand(CanExecute = nameof(CanStartRandomQuiz))]
    private Task StartRandomQuizAsync(CancellationToken cancellationToken) =>
        StartQuizAsync(null, QuizTitles.ForExam(MistakesOnly), cancellationToken);

    private bool CanStartRandomQuiz() => IsNotBusy && IsRandomCardAvailable;

    private async Task StartQuizAsync(Guid? topicId, string title, CancellationToken cancellationToken)
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var quiz = await api.CreateQuizAsync(new CreateQuizRequest(_examId, topicId, SelectedQuestionCount, MistakesOnly), cancellationToken);
            await navigation.GoToAsync(
                Routes.Quiz,
                new Dictionary<string, object> { [QueryKeys.QuizId] = quiz.Id, [QueryKeys.QuizTitle] = title });
        }
        catch (Exception ex)
        {
            LogCreateFailed(ex, ex.GetType().FullName);
            ErrorMessage = ApiErrorMessages.From(ex);
            if (MistakesOnly && ex is Refit.ApiException { StatusCode: HttpStatusCode.UnprocessableEntity }
                && ApiErrorMessages.ProblemCode(ex) == NoMistakesCode)
            {
                // The mistakes shown were out of date: refresh them.
                await LoadMistakesAsync(CancellationToken.None);
                RebuildTopics();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not load the topics ({ExceptionType})")]
    private partial void LogLoadFailed(Exception exception, string? exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not load the mistakes ({ExceptionType})")]
    private partial void LogMistakesFailed(Exception exception, string? exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not create the quiz ({ExceptionType})")]
    private partial void LogCreateFailed(Exception exception, string? exceptionType);
}