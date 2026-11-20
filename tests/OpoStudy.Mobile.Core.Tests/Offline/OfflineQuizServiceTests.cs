using System.Net;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using OpoStudy.Contracts.Offline;
using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.Auth;
using OpoStudy.Mobile.Core.Offline;

using Refit;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests.Offline;

public sealed class OfflineQuizServiceTests : IDisposable
{
    private static readonly Guid ExamId = OfflineTestData.ExamId;

    private readonly TempDatabase _temp = new();
    private readonly IAuthService _auth = Substitute.For<IAuthService>();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero));
    private readonly LocalQuizStore _store;
    private readonly OfflineContentStore _content;

    public OfflineQuizServiceTests()
    {
        _auth.UserId.Returns("user-a");
        _store = new LocalQuizStore(_temp.Database);
        var api = Substitute.For<IOpoStudyApi>();
        api.GetOfflineContentAsync(ExamId, Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(OfflineTestData.ContentResponse(HttpStatusCode.OK, OfflineTestData.Content()));
        _content = new OfflineContentStore(api, _temp.Database, _time, NullLogger<OfflineContentStore>.Instance);
        _content.RefreshAsync(ExamId, TestContext.Current.CancellationToken).GetAwaiter().GetResult();
    }

    public void Dispose() => _temp.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private OfflineQuizService CreateSut(Random? random = null) => new(_store, _content, _auth, _time, random ?? new Random(1));

    [Fact]
    public async Task CreateAsync_ByTopic_UsesOnlyQuestionsOfThatTopicWithoutFeedback()
    {
        var quiz = await CreateSut().CreateAsync(ExamId, OfflineTestData.TopicId(2), 2, Ct);

        quiz.Mode.ShouldBe("ByTopic");
        quiz.TopicId.ShouldBe(OfflineTestData.TopicId(2));
        quiz.StartedAt.ShouldBe(_time.GetUtcNow());
        quiz.FinishedAt.ShouldBeNull();
        quiz.Questions.Count.ShouldBe(2);
        quiz.Questions.Select(q => q.QuestionId).ShouldAllBe(id => Enumerable.Range(1, 3).Any(n => OfflineTestData.QuestionId(2, n) == id));
        quiz.Questions.Select(q => q.Order).ShouldBe([0, 1]);
        quiz.Questions.ShouldAllBe(q => q.Feedback == null && q.Options.Count == 3);
        quiz.Id.ToString()[14].ShouldBe('7');
    }

    [Fact]
    public async Task CreateAsync_Random_PicksDistinctQuestionsFromWholeExam()
    {
        var quiz = await CreateSut().CreateAsync(ExamId, null, 5, Ct);

        quiz.Mode.ShouldBe("Random");
        quiz.TopicId.ShouldBeNull();
        quiz.Questions.Select(q => q.QuestionId).Distinct().Count().ShouldBe(5);
    }

    [Fact]
    public async Task CreateAsync_MoreThanAvailable_CapsAtAvailable()
    {
        var byTopic = await CreateSut().CreateAsync(ExamId, OfflineTestData.TopicId(1), 50, Ct);
        var random = await CreateSut().CreateAsync(ExamId, null, 50, Ct);

        byTopic.Questions.Count.ShouldBe(3);
        random.Questions.Count.ShouldBe(6);
    }

    [Fact]
    public async Task CreateAsync_NeverShowsTheCorrectOption()
    {
        var quiz = await CreateSut().CreateAsync(ExamId, null, 2, Ct);

        quiz.Questions.SelectMany(q => q.Options).ShouldAllBe(o => o.Text.StartsWith("Opción", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateAsync_TopicWithoutQuestions_ThrowsWithTheApiMessage()
    {
        var sut = CreateSut();

        var exception = await Should.ThrowAsync<OfflineQuizException>(() => sut.CreateAsync(ExamId, Guid.NewGuid(), 5, Ct));

        exception.Message.ShouldBe("No hay preguntas disponibles para este test.");
    }

    [Fact]
    public async Task CreateAsync_ExamNotDownloaded_ThrowsNoContentMessage()
    {
        var sut = CreateSut();

        var exception = await Should.ThrowAsync<OfflineQuizException>(() => sut.CreateAsync(Guid.NewGuid(), null, 5, Ct));

        exception.Message.ShouldBe(OfflineQuizException.NoContentDownloaded);
    }

    [Fact]
    public async Task AnswerAsync_CorrectOption_CorrectsWithTheCacheAndQueuesTheAnswer()
    {
        var sut = CreateSut();
        var quiz = await sut.CreateAsync(ExamId, OfflineTestData.TopicId(1), 3, Ct);
        var (topic, number) = Locate(quiz.Questions[0].QuestionId);
        _time.Advance(TimeSpan.FromMinutes(5));

        var result = await sut.AnswerAsync(quiz.Id, quiz.Questions[0].QuestionId, OfflineTestData.CorrectOptionId(topic, number), Ct);

        result.Feedback.IsCorrect.ShouldBeTrue();
        result.Feedback.CorrectOptionId.ShouldBe(OfflineTestData.CorrectOptionId(topic, number));
        result.Feedback.Explanation.ShouldBe($"Aclaración {topic}.{number}");
        result.Feedback.LegalReference.ShouldBe($"Art. {number} de la ley {topic}");
        result.ShouldSatisfyAllConditions(
            r => r.CorrectCount.ShouldBe(1),
            r => r.IncorrectCount.ShouldBe(0),
            r => r.AnsweredCount.ShouldBe(1),
            r => r.QuestionCount.ShouldBe(3),
            r => r.QuizFinished.ShouldBeFalse());
        var pending = await _store.GetPendingAnswersAsync(quiz.Id, Ct);
        pending.ShouldHaveSingleItem().AnsweredAt.ShouldBe(_time.GetUtcNow());
        (await sut.PendingAnswerCountAsync(Ct)).ShouldBe(1);
    }

    [Fact]
    public async Task AnswerAsync_WrongOption_ReportsIncorrectWithTheRightOption()
    {
        var sut = CreateSut();
        var quiz = await sut.CreateAsync(ExamId, OfflineTestData.TopicId(1), 3, Ct);
        var (topic, number) = Locate(quiz.Questions[0].QuestionId);

        var result = await sut.AnswerAsync(quiz.Id, quiz.Questions[0].QuestionId, OfflineTestData.WrongOptionId(topic, number), Ct);

        result.Feedback.IsCorrect.ShouldBeFalse();
        result.Feedback.SelectedOptionId.ShouldBe(OfflineTestData.WrongOptionId(topic, number));
        result.Feedback.CorrectOptionId.ShouldBe(OfflineTestData.CorrectOptionId(topic, number));
        result.IncorrectCount.ShouldBe(1);
    }

    [Fact]
    public async Task AnswerAsync_LastQuestion_MarksQuizFinishedAndStopsOfferingItAsUnfinished()
    {
        var sut = CreateSut();
        var quiz = await sut.CreateAsync(ExamId, OfflineTestData.TopicId(1), 2, Ct);
        OfflineAnswerResult? last = null;
        foreach (var question in quiz.Questions)
        {
            var (topic, number) = Locate(question.QuestionId);
            last = await sut.AnswerAsync(quiz.Id, question.QuestionId, OfflineTestData.CorrectOptionId(topic, number), Ct);
        }

        last!.QuizFinished.ShouldBeTrue();
        last.AnsweredCount.ShouldBe(2);
        (await sut.GetLatestUnfinishedAsync(Ct)).ShouldBeNull();
        (await sut.GetAsync(quiz.Id, Ct))!.FinishedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task AnswerAsync_QuestionAnsweredTwice_Throws()
    {
        var sut = CreateSut();
        var quiz = await sut.CreateAsync(ExamId, OfflineTestData.TopicId(1), 3, Ct);
        var (topic, number) = Locate(quiz.Questions[0].QuestionId);
        await sut.AnswerAsync(quiz.Id, quiz.Questions[0].QuestionId, OfflineTestData.CorrectOptionId(topic, number), Ct);

        await Should.ThrowAsync<InvalidOperationException>(
            () => sut.AnswerAsync(quiz.Id, quiz.Questions[0].QuestionId, OfflineTestData.WrongOptionId(topic, number), Ct));
    }

    [Fact]
    public async Task AnswerAsync_QuestionMissingFromCache_Throws()
    {
        var orphanQuestion = Guid.NewGuid();
        var quizId = Guid.NewGuid();
        await _store.InsertAsync(
            new LocalQuiz(quizId, "user-a", ExamId, null, "Random", "Test", _time.GetUtcNow(), [orphanQuestion], false),
            [],
            Ct);

        await Should.ThrowAsync<InvalidOperationException>(
            () => CreateSut().AnswerAsync(quizId, orphanQuestion, Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task AnswerAsync_OptionOfAnotherQuestion_Throws()
    {
        var sut = CreateSut();
        var quiz = await sut.CreateAsync(ExamId, OfflineTestData.TopicId(1), 3, Ct);

        await Should.ThrowAsync<InvalidOperationException>(
            () => sut.AnswerAsync(quiz.Id, quiz.Questions[0].QuestionId, Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task GetAsync_AfterAnswering_ReturnsFeedbackOnlyForAnsweredQuestions()
    {
        var sut = CreateSut();
        var quiz = await sut.CreateAsync(ExamId, OfflineTestData.TopicId(1), 3, Ct);
        var (topic, number) = Locate(quiz.Questions[1].QuestionId);
        await sut.AnswerAsync(quiz.Id, quiz.Questions[1].QuestionId, OfflineTestData.WrongOptionId(topic, number), Ct);

        var reloaded = await sut.GetAsync(quiz.Id, Ct);

        reloaded.ShouldNotBeNull();
        reloaded.Questions.Select(q => q.QuestionId).ShouldBe(quiz.Questions.Select(q => q.QuestionId));
        reloaded.Questions[0].Feedback.ShouldBeNull();
        reloaded.Questions[1].Feedback.ShouldNotBeNull().IsCorrect.ShouldBeFalse();
        reloaded.CorrectCount.ShouldBe(0);
        reloaded.IncorrectCount.ShouldBe(1);
    }

    [Fact]
    public async Task GetAsync_UnknownQuiz_ReturnsNull() =>
        (await CreateSut().GetAsync(Guid.NewGuid(), Ct)).ShouldBeNull();

    [Fact]
    public async Task AdoptAsync_QuizWithAnsweredQuestions_StoresThemWithoutQueueingThem()
    {
        var sut = CreateSut();
        var online = OnlineQuiz(answeredCount: 2);

        await sut.AdoptAsync(online, "Tema 1", Ct);

        (await sut.PendingAnswerCountAsync(Ct)).ShouldBe(0);
        (await _store.GetPendingAnswersAsync(online.Id, Ct)).ShouldBeEmpty();
        var local = await sut.GetAsync(online.Id, Ct);
        local.ShouldNotBeNull();
        local.Questions.Select(q => q.QuestionId).ShouldBe(online.Questions.Select(q => q.QuestionId));
        local.Questions.Count(q => q.Feedback is not null).ShouldBe(2);
        local.CorrectCount.ShouldBe(2);
        (await sut.GetLatestUnfinishedAsync(Ct))!.Id.ShouldBe(online.Id);
    }

    [Fact]
    public async Task AdoptAsync_ThenAnsweringTheRest_QueuesOnlyTheNewAnswers()
    {
        var sut = CreateSut();
        var online = OnlineQuiz(answeredCount: 2);
        await sut.AdoptAsync(online, "Tema 1", Ct);
        var third = online.Questions[2].QuestionId;
        var (topic, number) = Locate(third);

        var result = await sut.AnswerAsync(online.Id, third, OfflineTestData.CorrectOptionId(topic, number), Ct);

        result.QuizFinished.ShouldBeTrue();
        result.AnsweredCount.ShouldBe(3);
        (await _store.GetPendingAnswersAsync(online.Id, Ct)).ShouldHaveSingleItem().QuestionId.ShouldBe(third);
        (await sut.PendingAnswerCountAsync(Ct)).ShouldBe(1);
    }

    [Fact]
    public async Task AdoptAsync_SameQuizTwice_DoesNotDuplicateOrFail()
    {
        var sut = CreateSut();
        var online = OnlineQuiz(answeredCount: 1);

        await sut.AdoptAsync(online, "Tema 1", Ct);
        await sut.AdoptAsync(online, "Tema 1", Ct);

        (await sut.GetAsync(online.Id, Ct))!.Questions.Count(q => q.Feedback is not null).ShouldBe(1);
    }

    [Fact]
    public async Task GetLatestUnfinishedAsync_SeveralQuizzes_ReturnsTheMostRecent()
    {
        var sut = CreateSut();
        await sut.CreateAsync(ExamId, OfflineTestData.TopicId(1), 2, Ct);
        _time.Advance(TimeSpan.FromHours(1));
        var newest = await sut.CreateAsync(ExamId, OfflineTestData.TopicId(2), 2, Ct);

        var latest = await sut.GetLatestUnfinishedAsync(Ct);

        latest.ShouldNotBeNull();
        latest.Id.ShouldBe(newest.Id);
        latest.Title.ShouldBe("Tema 2");
    }

    [Fact]
    public async Task CreateAsync_RandomQuiz_UsesTheRandomTitle()
    {
        var sut = CreateSut();
        await sut.CreateAsync(ExamId, null, 2, Ct);

        (await sut.GetLatestUnfinishedAsync(Ct))!.Title.ShouldBe("Test aleatorio");
    }

    [Fact]
    public async Task Quizzes_OfAnotherUser_AreInvisibleAndNotCounted()
    {
        var sut = CreateSut();
        var quiz = await sut.CreateAsync(ExamId, OfflineTestData.TopicId(1), 3, Ct);
        var (topic, number) = Locate(quiz.Questions[0].QuestionId);
        await sut.AnswerAsync(quiz.Id, quiz.Questions[0].QuestionId, OfflineTestData.CorrectOptionId(topic, number), Ct);

        _auth.UserId.Returns("user-b");

        (await sut.GetAsync(quiz.Id, Ct)).ShouldBeNull();
        (await sut.GetLatestUnfinishedAsync(Ct)).ShouldBeNull();
        (await sut.PendingAnswerCountAsync(Ct)).ShouldBe(0);
        await Should.ThrowAsync<InvalidOperationException>(
            () => sut.AnswerAsync(quiz.Id, quiz.Questions[1].QuestionId, OfflineTestData.CorrectOptionId(topic, number), Ct));

        _auth.UserId.Returns("user-a");
        (await sut.PendingAnswerCountAsync(Ct)).ShouldBe(1);
    }

    [Fact]
    public async Task Reads_WithoutSession_ReturnNothing()
    {
        var sut = CreateSut();
        var quiz = await sut.CreateAsync(ExamId, OfflineTestData.TopicId(1), 3, Ct);
        _auth.UserId.Returns((string?)null);

        (await sut.GetAsync(quiz.Id, Ct)).ShouldBeNull();
        (await sut.GetLatestUnfinishedAsync(Ct)).ShouldBeNull();
        (await sut.PendingAnswerCountAsync(Ct)).ShouldBe(0);
        await Should.ThrowAsync<InvalidOperationException>(() => sut.CreateAsync(ExamId, null, 2, Ct));
    }

    [Fact]
    public async Task DeleteUserDataAsync_RemovesOnlyThatUsersQuizzesAndAnswers()
    {
        var sut = CreateSut();
        var mine = await sut.CreateAsync(ExamId, OfflineTestData.TopicId(1), 3, Ct);
        var (topic, number) = Locate(mine.Questions[0].QuestionId);
        await sut.AnswerAsync(mine.Id, mine.Questions[0].QuestionId, OfflineTestData.CorrectOptionId(topic, number), Ct);
        _auth.UserId.Returns("user-b");
        var theirs = await sut.CreateAsync(ExamId, null, 2, Ct);

        await sut.DeleteUserDataAsync("user-a", Ct);

        (await sut.GetAsync(theirs.Id, Ct)).ShouldNotBeNull();
        _auth.UserId.Returns("user-a");
        (await sut.GetAsync(mine.Id, Ct)).ShouldBeNull();
        (await sut.PendingAnswerCountAsync(Ct)).ShouldBe(0);
    }

    private static (int Topic, int Number) Locate(Guid questionId)
    {
        for (var topic = 1; topic <= 2; topic++)
        {
            for (var number = 1; number <= 3; number++)
            {
                if (OfflineTestData.QuestionId(topic, number) == questionId)
                {
                    return (topic, number);
                }
            }
        }

        throw new InvalidOperationException("Unknown question.");
    }

    /// <summary>An online quiz over topic 1 whose first <paramref name="answeredCount"/> questions were answered correctly.</summary>
    private static OpoStudy.Contracts.Quizzes.QuizResponse OnlineQuiz(int answeredCount) => new(
        Guid.CreateVersion7(),
        ExamId,
        OfflineTestData.TopicId(1),
        "ByTopic",
        new DateTimeOffset(2026, 3, 1, 7, 0, 0, TimeSpan.Zero),
        null,
        answeredCount,
        0,
        [.. Enumerable.Range(1, 3).Select(n => new OpoStudy.Contracts.Quizzes.QuizQuestionResponse(
            OfflineTestData.QuestionId(1, n),
            n - 1,
            $"Pregunta 1.{n}",
            [.. Enumerable.Range(1, 3).Select(o => new OpoStudy.Contracts.Quizzes.QuizOptionResponse(OfflineTestData.OptionId(1, n, o), o, $"Opción {o}"))],
            n <= answeredCount
                ? new OpoStudy.Contracts.Quizzes.AnswerFeedback(
                    OfflineTestData.CorrectOptionId(1, n),
                    true,
                    OfflineTestData.CorrectOptionId(1, n),
                    $"Aclaración 1.{n}",
                    $"Art. {n} de la ley 1")
                : null))]);
}