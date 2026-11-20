using System.Net;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using OpoStudy.Contracts.Quizzes;
using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.Auth;
using OpoStudy.Mobile.Core.Offline;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests.Offline;

public sealed class QuizSyncServiceTests : IDisposable
{
    private static readonly Guid ExamId = OfflineTestData.ExamId;

    private readonly TempDatabase _temp = new();
    private readonly IOpoStudyApi _api = Substitute.For<IOpoStudyApi>();
    private readonly IAuthService _auth = Substitute.For<IAuthService>();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero));
    private readonly LocalQuizStore _store;
    private readonly QuizSyncService _sut;

    public QuizSyncServiceTests()
    {
        _auth.UserId.Returns("user-a");
        _store = new LocalQuizStore(_temp.Database);
        _sut = new QuizSyncService(_api, _store, _auth, _time, NullLogger<QuizSyncService>.Instance);
        _api.SyncQuizAsync(Arg.Any<SyncQuizRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => new SyncQuizResponse(call.Arg<SyncQuizRequest>().Id, call.Arg<SyncQuizRequest>().Answers.Count, 0, 0, 10, false));
    }

    public void Dispose()
    {
        _sut.Dispose();
        _temp.Dispose();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Inserts a local quiz with three questions of topic 1 and the given number of pending answers.</summary>
    private async Task<LocalQuiz> AddQuizAsync(int pending, string userId = "user-a", DateTimeOffset? startedAt = null, bool finished = false)
    {
        var quiz = new LocalQuiz(
            Guid.CreateVersion7(),
            userId,
            ExamId,
            OfflineTestData.TopicId(1),
            "ByTopic",
            "Tema 1",
            startedAt ?? _time.GetUtcNow(),
            [.. Enumerable.Range(1, 3).Select(n => OfflineTestData.QuestionId(1, n))],
            finished);
        var answers = Enumerable.Range(1, pending)
            .Select(n => new LocalAnswer(
                OfflineTestData.QuestionId(1, n),
                OfflineTestData.CorrectOptionId(1, n),
                _time.GetUtcNow().AddMinutes(n),
                true,
                Synced: false))
            .ToList();
        await _store.InsertAsync(quiz, answers, Ct);
        return quiz;
    }

    private async Task<bool> ExistsAsync(LocalQuiz quiz) => await _store.GetAsync(quiz.Id, quiz.UserId, Ct) is not null;

    [Fact]
    public async Task FlushAsync_Success_SendsTheWholeQuizAndDeletesAFinishedOne()
    {
        var quiz = await AddQuizAsync(3, finished: true);

        var synced = await _sut.FlushAsync(null, Ct);

        synced.ShouldBe(new SyncResult(3, 0));
        await _api.Received(1).SyncQuizAsync(
            Arg.Is<SyncQuizRequest>(r => r.Id == quiz.Id
                && r.ExamId == ExamId
                && r.TopicId == quiz.TopicId
                && r.Mode == "ByTopic"
                && r.StartedAt == quiz.StartedAt
                && r.QuestionIds.SequenceEqual(quiz.QuestionIds)
                && r.Answers.Count == 3
                && r.Answers[0].QuestionId == OfflineTestData.QuestionId(1, 1)
                && r.Answers[0].AnsweredAt == _time.GetUtcNow().AddMinutes(1)),
            Arg.Any<CancellationToken>());
        (await ExistsAsync(quiz)).ShouldBeFalse();
    }

    [Fact]
    public async Task FlushAsync_SuccessOnUnfinishedQuiz_EmptiesTheQueueButKeepsTheQuiz()
    {
        var quiz = await AddQuizAsync(2);

        await _sut.FlushAsync(null, Ct);

        (await _store.GetPendingAnswersAsync(quiz.Id, Ct)).ShouldBeEmpty();
        (await _store.GetAnswersAsync(quiz.Id, Ct)).Count.ShouldBe(2);
        (await ExistsAsync(quiz)).ShouldBeTrue();
        (await _store.CountPendingAnswersAsync("user-a", Ct)).ShouldBe(0);

        _api.ClearReceivedCalls();
        (await _sut.FlushAsync(null, Ct)).ShouldBe(new SyncResult(0, 0));
        await _api.DidNotReceive().SyncQuizAsync(Arg.Any<SyncQuizRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FlushAsync_SeveralQuizzes_SendsThemOldestFirst()
    {
        var newer = await AddQuizAsync(1, startedAt: _time.GetUtcNow());
        var older = await AddQuizAsync(1, startedAt: _time.GetUtcNow().AddHours(-2));
        var sent = new List<Guid>();
        _api.SyncQuizAsync(Arg.Do<SyncQuizRequest>(r => sent.Add(r.Id)), Arg.Any<CancellationToken>())
            .Returns(new SyncQuizResponse(Guid.Empty, 1, 0, 0, 0, false));

        await _sut.FlushAsync(null, Ct);

        sent.ShouldBe([older.Id, newer.Id]);
    }

    [Fact]
    public async Task FlushAsync_NoConnection_KeepsEverythingAndStops()
    {
        var first = await AddQuizAsync(1, startedAt: _time.GetUtcNow().AddHours(-2));
        var second = await AddQuizAsync(1);
        _api.SyncQuizAsync(Arg.Any<SyncQuizRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException("offline"));

        var synced = await _sut.FlushAsync(null, Ct);

        synced.ShouldBe(new SyncResult(0, 0));
        await _api.Received(1).SyncQuizAsync(Arg.Any<SyncQuizRequest>(), Arg.Any<CancellationToken>());
        (await _store.CountPendingAnswersAsync("user-a", Ct)).ShouldBe(2);
        (await ExistsAsync(first)).ShouldBeTrue();
        (await ExistsAsync(second)).ShouldBeTrue();
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, null)]
    [InlineData(HttpStatusCode.Conflict, "quiz.concurrent_update")]
    [InlineData(HttpStatusCode.Conflict, "concurrency.conflict")]
    [InlineData(HttpStatusCode.InternalServerError, null)]
    [InlineData(HttpStatusCode.ServiceUnavailable, null)]
    [InlineData(HttpStatusCode.TooManyRequests, null)]
    public async Task FlushAsync_RetryableStatus_KeepsTheQueueAndStopsTheLoop(HttpStatusCode status, string? code)
    {
        await AddQuizAsync(1, startedAt: _time.GetUtcNow().AddHours(-2));
        await AddQuizAsync(1);
        _api.SyncQuizAsync(Arg.Any<SyncQuizRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(await OfflineTestData.ApiExceptionAsync(status, code));

        var synced = await _sut.FlushAsync(null, Ct);

        synced.ShouldBe(new SyncResult(0, 0));
        await _api.Received(1).SyncQuizAsync(Arg.Any<SyncQuizRequest>(), Arg.Any<CancellationToken>());
        (await _store.CountPendingAnswersAsync("user-a", Ct)).ShouldBe(2);
    }

    [Theory]
    [InlineData(HttpStatusCode.UnprocessableEntity, "quiz.invalid_questions")]
    [InlineData(HttpStatusCode.Conflict, "quiz.sync_conflict")]
    [InlineData(HttpStatusCode.BadRequest, null)]
    [InlineData(HttpStatusCode.NotFound, "exam.not_found")]
    public async Task FlushAsync_UnfixableClientError_DiscardsTheLocalQuizAndContinues(HttpStatusCode status, string? code)
    {
        var rejected = await AddQuizAsync(2, startedAt: _time.GetUtcNow().AddHours(-2));
        var good = await AddQuizAsync(1, finished: true);
        _api.SyncQuizAsync(Arg.Is<SyncQuizRequest>(r => r.Id == rejected.Id), Arg.Any<CancellationToken>())
            .ThrowsAsync(await OfflineTestData.ApiExceptionAsync(status, code));

        var synced = await _sut.FlushAsync(null, Ct);

        synced.ShouldBe(new SyncResult(1, 1));
        (await ExistsAsync(rejected)).ShouldBeFalse();
        (await _store.GetAnswersAsync(rejected.Id, Ct)).ShouldBeEmpty();
        (await ExistsAsync(good)).ShouldBeFalse();
    }

    [Fact]
    public async Task FlushAsync_ExcludedQuiz_IsNotSent()
    {
        var playing = await AddQuizAsync(1, startedAt: _time.GetUtcNow().AddHours(-2));
        var other = await AddQuizAsync(1);

        await _sut.FlushAsync(playing.Id, Ct);

        await _api.DidNotReceive().SyncQuizAsync(Arg.Is<SyncQuizRequest>(r => r.Id == playing.Id), Arg.Any<CancellationToken>());
        await _api.Received(1).SyncQuizAsync(Arg.Is<SyncQuizRequest>(r => r.Id == other.Id), Arg.Any<CancellationToken>());
        (await _store.GetPendingAnswersAsync(playing.Id, Ct)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task FlushAsync_OtherUsersQuiz_IsNeverSent()
    {
        await AddQuizAsync(2, userId: "user-b");

        var synced = await _sut.FlushAsync(null, Ct);

        synced.ShouldBe(new SyncResult(0, 0));
        await _api.DidNotReceive().SyncQuizAsync(Arg.Any<SyncQuizRequest>(), Arg.Any<CancellationToken>());
        _auth.UserId.Returns("user-b");
        (await _sut.FlushAsync(null, Ct)).ShouldBe(new SyncResult(2, 0));
    }

    [Fact]
    public async Task FlushAsync_WithoutSession_DoesNothing()
    {
        await AddQuizAsync(1);
        _auth.UserId.Returns((string?)null);

        (await _sut.FlushAsync(null, Ct)).ShouldBe(new SyncResult(0, 0));

        await _api.DidNotReceive().SyncQuizAsync(Arg.Any<SyncQuizRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FlushAsync_CancelledByCaller_ThrowsInsteadOfBeingTreatedAsOffline()
    {
        await AddQuizAsync(1);
        using var cts = new CancellationTokenSource();
        _api.SyncQuizAsync(Arg.Any<SyncQuizRequest>(), Arg.Any<CancellationToken>()).Returns<SyncQuizResponse>(_ =>
        {
            cts.Cancel();
            throw new TaskCanceledException("cancelled", null, cts.Token);
        });

        await Should.ThrowAsync<OperationCanceledException>(() => _sut.FlushAsync(null, cts.Token));
    }

    [Fact]
    public async Task FlushAsync_TwoCallsAtTheSameTime_SendTheQuizOnlyOnce()
    {
        await AddQuizAsync(2, finished: true);
        var started = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        _api.SyncQuizAsync(Arg.Any<SyncQuizRequest>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            started.TrySetResult();
            await release.Task;
            return new SyncQuizResponse(call.Arg<SyncQuizRequest>().Id, 2, 0, 0, 0, true);
        });

        var first = _sut.FlushAsync(null, Ct);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        var second = _sut.FlushAsync(null, Ct);
        await Task.Delay(100, Ct);
        second.IsCompleted.ShouldBeFalse();
        release.SetResult();

        (await first).ShouldBe(new SyncResult(2, 0));
        (await second).ShouldBe(new SyncResult(0, 0));
        await _api.Received(1).SyncQuizAsync(Arg.Any<SyncQuizRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FlushAsync_Cleanup_DeletesFinishedAndOldQuizzesWithoutPendingAnswers()
    {
        var finishedNoPending = await AddQuizAsync(0, finished: true);
        var old = await AddQuizAsync(0, startedAt: _time.GetUtcNow().AddDays(-31));
        var recent = await AddQuizAsync(0, startedAt: _time.GetUtcNow().AddDays(-29));
        var oldWithPending = await AddQuizAsync(1, startedAt: _time.GetUtcNow().AddDays(-40));
        _api.SyncQuizAsync(Arg.Any<SyncQuizRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException("offline"));

        await _sut.FlushAsync(null, Ct);

        (await ExistsAsync(finishedNoPending)).ShouldBeFalse();
        (await ExistsAsync(old)).ShouldBeFalse();
        (await ExistsAsync(recent)).ShouldBeTrue();
        (await ExistsAsync(oldWithPending)).ShouldBeTrue();
    }
}