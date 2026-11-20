using OpoStudy.Contracts.Offline;
using OpoStudy.Contracts.Quizzes;
using OpoStudy.Mobile.Core.Auth;
using OpoStudy.Mobile.Core.ViewModels;

namespace OpoStudy.Mobile.Core.Offline;

/// <param name="random">Source of the random question choice; tests pass a seeded one.</param>
public sealed class OfflineQuizService(
    LocalQuizStore store,
    IOfflineContentStore content,
    IAuthService auth,
    TimeProvider time,
    Random? random = null) : IOfflineQuizService
{
    private const string RandomMode = "Random";
    private const string ByTopicMode = "ByTopic";

    private readonly Random _random = random ?? Random.Shared;

    public async Task<QuizResponse> CreateAsync(Guid examId, Guid? topicId, int questionCount, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(questionCount);
        var userId = RequireUser();
        var offline = await content.GetAsync(examId, cancellationToken)
            ?? throw new OfflineQuizException(OfflineQuizException.NoContentDownloaded);

        var topics = topicId is { } id ? offline.Topics.Where(t => t.Id == id) : offline.Topics;
        var pool = topics.SelectMany(t => t.Questions).ToArray();
        if (pool.Length == 0)
        {
            throw new OfflineQuizException(OfflineQuizException.NoQuestionsAvailable);
        }

        _random.Shuffle(pool);
        var chosen = pool.Take(questionCount).Select(q => q.Id).ToArray();
        var title = topicId is null ? QuizTitles.RandomQuiz : QuizTitles.ForTopic(topics.First().Title, mistakesOnly: false);
        var quiz = new LocalQuiz(
            Guid.CreateVersion7(),
            userId,
            examId,
            topicId,
            topicId is null ? RandomMode : ByTopicMode,
            title,
            time.GetUtcNow(),
            chosen,
            Finished: false);
        await store.InsertAsync(quiz, [], cancellationToken);
        return OfflineQuizBuilder.Build(quiz, [], offline)
            ?? throw new InvalidOperationException("A quiz built from the cache must be buildable.");
    }

    public async Task AdoptAsync(QuizResponse quiz, string title, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(quiz);
        ArgumentNullException.ThrowIfNull(title);

        var userId = RequireUser();
        var now = time.GetUtcNow();
        var ordered = quiz.Questions.OrderBy(q => q.Order).ToList();
        var answers = ordered
            .Where(q => q.Feedback is not null)
            .Select(q => new LocalAnswer(q.QuestionId, q.Feedback!.SelectedOptionId, now, q.Feedback.IsCorrect, Synced: true))
            .ToList();
        var local = new LocalQuiz(
            quiz.Id,
            userId,
            quiz.ExamId,
            quiz.TopicId,
            quiz.Mode,
            title,
            quiz.StartedAt,
            [.. ordered.Select(q => q.QuestionId)],
            Finished: answers.Count == ordered.Count);
        await store.InsertAsync(local, answers, cancellationToken);
    }

    public async Task<QuizResponse?> GetAsync(Guid quizId, CancellationToken cancellationToken = default)
    {
        if (auth.UserId is not { } userId
            || await store.GetAsync(quizId, userId, cancellationToken) is not { } quiz
            || await content.GetAsync(quiz.ExamId, cancellationToken) is not { } offline)
        {
            return null;
        }

        return OfflineQuizBuilder.Build(quiz, await store.GetAnswersAsync(quizId, cancellationToken), offline);
    }

    public async Task<OfflineAnswerResult> AnswerAsync(Guid quizId, Guid questionId, Guid optionId, CancellationToken cancellationToken = default)
    {
        var userId = RequireUser();
        var quiz = await store.GetAsync(quizId, userId, cancellationToken)
            ?? throw new InvalidOperationException("The local quiz does not exist.");
        if (!quiz.QuestionIds.Contains(questionId))
        {
            throw new InvalidOperationException("The question is not part of the quiz.");
        }

        var offline = await content.GetAsync(quiz.ExamId, cancellationToken);
        if (offline is null || !OfflineQuizBuilder.IndexQuestions(offline).TryGetValue(questionId, out var question))
        {
            throw new InvalidOperationException("The question is not in the offline content.");
        }

        if (question.Options.All(o => o.Id != optionId))
        {
            throw new InvalidOperationException("The option does not belong to the question.");
        }

        var feedback = OfflineQuizBuilder.Feedback(question, optionId);
        var finished = await store.AddAnswerAsync(
            quizId,
            new LocalAnswer(questionId, optionId, time.GetUtcNow(), feedback.IsCorrect, Synced: false),
            quiz.QuestionIds.Count,
            cancellationToken);

        var answers = await store.GetAnswersAsync(quizId, cancellationToken);
        var correct = answers.Count(a => a.IsCorrect);
        return new OfflineAnswerResult(feedback, finished, correct, answers.Count - correct, answers.Count, quiz.QuestionIds.Count);
    }

    public async Task<LocalQuiz?> GetLatestUnfinishedAsync(CancellationToken cancellationToken = default) =>
        auth.UserId is { } userId ? await store.GetLatestUnfinishedAsync(userId, cancellationToken) : null;

    public async Task<int> PendingAnswerCountAsync(CancellationToken cancellationToken = default) =>
        auth.UserId is { } userId ? await store.CountPendingAnswersAsync(userId, cancellationToken) : 0;

    public Task DeleteUserDataAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return store.DeleteUserDataAsync(userId, cancellationToken);
    }

    private string RequireUser() =>
        auth.UserId ?? throw new InvalidOperationException("There is no signed-in user.");
}