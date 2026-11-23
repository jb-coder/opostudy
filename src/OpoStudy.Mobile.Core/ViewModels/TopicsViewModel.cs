using System.Collections.ObjectModel;
using System.Net;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Extensions.Logging;

using OpoStudy.Contracts.Exams;
using OpoStudy.Contracts.Offline;
using OpoStudy.Contracts.Quizzes;
using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.Navigation;
using OpoStudy.Mobile.Core.Offline;

namespace OpoStudy.Mobile.Core.ViewModels;

/// <summary>A topic as shown in the list. The mistake data changes in place, so the list is not rebuilt.</summary>
/// <param name="topic">The topic.</param>
public sealed partial class TopicItem(TopicResponse topic) : ObservableObject
{
    public TopicResponse Topic { get; } = topic;

    /// <summary>Questions of the topic the user currently has wrong.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAvailable))]
    [NotifyPropertyChangedFor(nameof(QuestionCountText))]
    public partial int MistakeCount { get; set; }

    /// <summary>Whether the list shows the mistakes to review instead of the questions available.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAvailable))]
    [NotifyPropertyChangedFor(nameof(QuestionCountText))]
    public partial bool MistakesMode { get; set; }

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
    IOfflineContentStore offlineContent,
    IOfflineQuizService offlineQuizzes,
    OfflineState offlineState,
    ILogger<TopicsViewModel> logger) : OfflineAwareViewModel(offlineState), IQueryReceiver
{
    private const string MistakesNeedConnectionMessage = "El repaso de falladas necesita conexión.";

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

    /// <summary>The mistakes could not be loaded because there is no connection (reviews need the server).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanReviewMistakes))]
    [NotifyPropertyChangedFor(nameof(MistakesHint))]
    [NotifyPropertyChangedFor(nameof(HasMistakesHint))]
    public partial bool MistakesNeedConnection { get; set; }

    /// <summary>Whether the "only mistakes" switch can be used.</summary>
    public bool CanReviewMistakes => !MistakesLoadFailed && !MistakesNeedConnection && MistakeTotal > 0;

    /// <summary>Why the switch is disabled; empty while it is usable.</summary>
    public string MistakesHint =>
        MistakesNeedConnection ? MistakesNeedConnectionMessage
        : MistakesLoadFailed ? "No se han podido cargar tus preguntas falladas."
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
        ListenToOfflineState();
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
            try
            {
                await LoadMistakesAsync(PageToken);
                UpdateTopics();
            }
            catch (OperationCanceledException)
            {
                // The page went away before the mistakes arrived.
            }
        }
    }

    partial void OnHasAnyQuestionsChanged(bool value) => OnPropertyChanged(nameof(IsRandomCardAvailable));

    partial void OnMistakesOnlyChanged(bool value) => UpdateTopics();

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

            OfflineState.ReportSuccess();
            _loaded = true;
            IsEmpty = _topics.Count == 0;
            HasAnyQuestions = _topics.Any(t => t.QuestionCount > 0);
            RebuildTopics();
            _ = DownloadContentAsync(_examId);
        }
        catch (Exception ex) when (ConnectivityErrors.IsConnectivityError(ex, cancellationToken))
        {
            LogLoadFailed(ex, ex.GetType().FullName);
            OfflineState.ReportConnectivityFailure();
            await ShowCachedTopicsAsync(cancellationToken);
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

    /// <summary>Refreshes the offline copy of the exam in the background. It never blocks or shows errors.</summary>
    private async Task DownloadContentAsync(Guid examId)
    {
        try
        {
            await offlineContent.RefreshAsync(examId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            LogDownloadFailed(ex, ex.GetType().FullName);
        }
    }

    /// <summary>Without connection: topics and counts from the offline copy. Not marked as loaded, so the next visit tries the server again.</summary>
    private async Task ShowCachedTopicsAsync(CancellationToken cancellationToken)
    {
        OfflineContentResponse? content = null;
        try
        {
            content = await offlineContent.GetAsync(_examId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogLoadFailed(ex, ex.GetType().FullName);
        }

        if (content is null)
        {
            IsEmpty = false;
            ErrorMessage = OfflineQuizException.NoContentDownloaded;
            return;
        }

        // Reviews need the server's history.
        _mistakesByTopic = [];
        MistakeTotal = 0;
        MistakesLoadFailed = false;
        MistakesNeedConnection = true;
        MistakesOnly = false;

        _topics = [.. content.Topics.Select(t => new TopicResponse(t.Id, t.Number, t.Title, t.Section, t.Questions.Count))];
        IsEmpty = _topics.Count == 0;
        HasAnyQuestions = _topics.Any(t => t.QuestionCount > 0);
        RebuildTopics();
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
            MistakesNeedConnection = false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogMistakesFailed(ex, ex.GetType().FullName);
            _mistakesByTopic = [];
            MistakeTotal = 0;
            var offline = ConnectivityErrors.IsConnectivityError(ex, cancellationToken);
            MistakesNeedConnection = offline;
            MistakesLoadFailed = !offline;
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
            Topics.Add(new TopicItem(topic));
        }

        UpdateTopics();
    }

    /// <summary>Updates the mistake data of the existing items in place.</summary>
    private void UpdateTopics()
    {
        foreach (var item in Topics)
        {
            item.MistakeCount = _mistakesByTopic.GetValueOrDefault(item.Id);
            item.MistakesMode = MistakesOnly;
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
            if (!MistakesOnly && OfflineState.IsOffline)
            {
                await StartOfflineQuizAsync(topicId, title, cancellationToken);
                return;
            }

            var quiz = await api.CreateQuizAsync(new CreateQuizRequest(_examId, topicId, SelectedQuestionCount, MistakesOnly), cancellationToken);
            OfflineState.ReportSuccess();
            await navigation.GoToAsync(
                Routes.Quiz,
                new Dictionary<string, object> { [QueryKeys.QuizId] = quiz.Id, [QueryKeys.QuizTitle] = title });
        }
        catch (Exception ex) when (!MistakesOnly && ConnectivityErrors.IsConnectivityError(ex, cancellationToken))
        {
            LogCreateFailed(ex, ex.GetType().FullName);
            OfflineState.ReportConnectivityFailure();
            await StartOfflineQuizAsync(topicId, title, cancellationToken);
        }
        catch (Exception ex)
        {
            LogCreateFailed(ex, ex.GetType().FullName);
            ErrorMessage = ApiErrorMessages.From(ex);
            if (MistakesOnly && ex is Refit.ApiException { StatusCode: HttpStatusCode.UnprocessableEntity }
                && ApiErrorMessages.ProblemCode(ex) == NoMistakesCode)
            {
                // The mistakes shown were out of date: refresh them.
                await LoadMistakesAsync(cancellationToken);
                UpdateTopics();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Creates the quiz from the offline copy and opens it. Failures are shown in <see cref="ViewModelBase.ErrorMessage"/>.</summary>
    private async Task StartOfflineQuizAsync(Guid? topicId, string title, CancellationToken cancellationToken)
    {
        try
        {
            var quiz = await offlineQuizzes.CreateAsync(_examId, topicId, SelectedQuestionCount, cancellationToken);
            await navigation.GoToAsync(
                Routes.Quiz,
                new Dictionary<string, object> { [QueryKeys.QuizId] = quiz.Id, [QueryKeys.QuizTitle] = title });
        }
        catch (OfflineQuizException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogCreateFailed(ex, ex.GetType().FullName);
            ErrorMessage = ApiErrorMessages.From(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not refresh the offline content ({ExceptionType})")]
    private partial void LogDownloadFailed(Exception exception, string? exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not load the topics ({ExceptionType})")]
    private partial void LogLoadFailed(Exception exception, string? exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not load the mistakes ({ExceptionType})")]
    private partial void LogMistakesFailed(Exception exception, string? exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not create the quiz ({ExceptionType})")]
    private partial void LogCreateFailed(Exception exception, string? exceptionType);
}