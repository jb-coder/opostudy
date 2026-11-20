using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Extensions.Logging;

using OpoStudy.Contracts.Exams;
using OpoStudy.Contracts.Quizzes;
using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.Navigation;

namespace OpoStudy.Mobile.Core.ViewModels;

/// <summary>A topic as shown in the list.</summary>
public sealed record TopicItem(TopicResponse Topic)
{
    public Guid Id => Topic.Id;

    /// <summary>
    /// The content title as is: it already starts with "Tema N." using the official (per-block) numbering,
    /// which differs from the global <see cref="TopicResponse.Number"/>.
    /// </summary>
    public string Title => Topic.Title;

    public string? Section => Topic.Section;

    public bool HasSection => !string.IsNullOrWhiteSpace(Topic.Section);

    public bool IsAvailable => Topic.QuestionCount > 0;

    public string QuestionCountText => Topic.QuestionCount switch
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
    private const string RandomQuizTitle = "Test aleatorio";

    private Guid _examId;

    private bool _loaded;

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
        if (_loaded)
        {
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
        StartRandomQuizCommand.NotifyCanExecuteChanged();
        StartTopicQuizCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var topics = await api.GetTopicsAsync(_examId, cancellationToken);
            Topics.Clear();
            foreach (var topic in topics)
            {
                Topics.Add(new TopicItem(topic));
            }

            _loaded = true;
            IsEmpty = Topics.Count == 0;
            HasAnyQuestions = Topics.Any(t => t.IsAvailable);
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

    [RelayCommand(CanExecute = nameof(CanStartTopicQuiz))]
    private Task StartTopicQuizAsync(TopicItem topic, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(topic);

        return topic.IsAvailable ? StartQuizAsync(topic.Id, topic.Title, cancellationToken) : Task.CompletedTask;
    }

    private bool CanStartTopicQuiz(TopicItem? topic) => IsNotBusy && topic is { IsAvailable: true };

    [RelayCommand(CanExecute = nameof(CanStartRandomQuiz))]
    private Task StartRandomQuizAsync(CancellationToken cancellationToken) =>
        StartQuizAsync(null, RandomQuizTitle, cancellationToken);

    private bool CanStartRandomQuiz() => IsNotBusy && HasAnyQuestions;

    private async Task StartQuizAsync(Guid? topicId, string title, CancellationToken cancellationToken)
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var quiz = await api.CreateQuizAsync(new CreateQuizRequest(_examId, topicId, SelectedQuestionCount), cancellationToken);
            await navigation.GoToAsync(
                Routes.Quiz,
                new Dictionary<string, object> { [QueryKeys.QuizId] = quiz.Id, [QueryKeys.QuizTitle] = title });
        }
        catch (Exception ex)
        {
            LogCreateFailed(ex, ex.GetType().FullName);
            ErrorMessage = ApiErrorMessages.From(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not load the topics ({ExceptionType})")]
    private partial void LogLoadFailed(Exception exception, string? exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not create the quiz ({ExceptionType})")]
    private partial void LogCreateFailed(Exception exception, string? exceptionType);
}