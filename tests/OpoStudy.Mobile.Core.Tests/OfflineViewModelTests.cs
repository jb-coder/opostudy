using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using OpoStudy.Contracts.Exams;
using OpoStudy.Contracts.Offline;
using OpoStudy.Contracts.Progress;
using OpoStudy.Contracts.Quizzes;
using OpoStudy.Contracts.SystemInfo;
using OpoStudy.Contracts.Users;
using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.Auth;
using OpoStudy.Mobile.Core.Navigation;
using OpoStudy.Mobile.Core.Offline;
using OpoStudy.Mobile.Core.Reminders;
using OpoStudy.Mobile.Core.Services;
using OpoStudy.Mobile.Core.ViewModels;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests;

public sealed class ExamsOfflineViewModelTests
{
    private readonly IOpoStudyApi _api = Substitute.For<IOpoStudyApi>();

    private readonly OfflineDoubles _offline = new();

    private ExamsViewModel CreateSut() =>
        new(_api, Substitute.For<INavigationService>(), _offline.Content, _offline.State, NullLogger<ExamsViewModel>.Instance);

    private static ExamResponse Exam() => new(Guid.NewGuid(), "c1", "Programador C1", "Diputación de Málaga", "C1");

    [Fact]
    public async Task OnAppearingAsync_ApiUnreachable_ListsDownloadedExamsWithBanner()
    {
        _api.GetExamsAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException());
        _offline.Content.ListExamsAsync(Arg.Any<CancellationToken>()).Returns([Exam()]);
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.Exams.Count.ShouldBe(1);
        sut.HasError.ShouldBeFalse();
        sut.ShowOfflineBanner.ShouldBeTrue();
        sut.OfflineBannerText.ShouldBe("Sin conexión. Tus respuestas se guardan y se sincronizarán al recuperarla.");
    }

    [Fact]
    public async Task OnAppearingAsync_ApiUnreachableAndNothingDownloaded_ShowsNoContentMessage()
    {
        _api.GetExamsAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException());
        _offline.Content.ListExamsAsync(Arg.Any<CancellationToken>()).Returns([]);
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.Exams.ShouldBeEmpty();
        sut.ErrorMessage.ShouldBe("Sin conexión y sin contenido descargado. Abre una oposición con conexión para poder estudiar sin ella.");
    }

    [Fact]
    public async Task RefreshCommand_ApiRecovers_HidesBanner()
    {
        _api.GetExamsAsync(Arg.Any<CancellationToken>()).Returns(
            _ => Task.FromException<IReadOnlyList<ExamResponse>>(new HttpRequestException()),
            _ => Task.FromResult<IReadOnlyList<ExamResponse>>([Exam()]));
        _offline.Content.ListExamsAsync(Arg.Any<CancellationToken>()).Returns([]);
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        sut.ShowOfflineBanner.ShouldBeTrue();

        await sut.RefreshCommand.ExecuteAsync(null);

        sut.ShowOfflineBanner.ShouldBeFalse();
        sut.Exams.Count.ShouldBe(1);
    }

    [Fact]
    public async Task OnAppearingAsync_NoInternet_ShowsBannerEvenWhenApiAnswers()
    {
        _offline.Network.HasInternet.Returns(false);
        _api.GetExamsAsync(Arg.Any<CancellationToken>()).Returns([Exam()]);
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.ShowOfflineBanner.ShouldBeTrue();
    }
}

public sealed class TopicsOfflineViewModelTests
{
    private readonly IOpoStudyApi _api = Substitute.For<IOpoStudyApi>();

    private readonly OfflineDoubles _offline = new();

    private readonly INavigationService _navigation = Substitute.For<INavigationService>();

    private readonly Guid _examId = Guid.NewGuid();

    private readonly OfflineTopicResponse _topic = new(Guid.NewGuid(), 1, "Tema 1. Constitución", null, [Question(), Question()]);

    private static OfflineQuestionResponse Question() =>
        new(Guid.NewGuid(), "Statement", "Explanation", "Art. 1", [new OfflineOptionResponse(Guid.NewGuid(), 1, "A", true)]);

    private TopicsViewModel CreateSut()
    {
        var sut = new TopicsViewModel(_api, _navigation, _offline.Content, _offline.Quizzes, _offline.State, NullLogger<TopicsViewModel>.Instance);
        sut.ApplyQuery(new Dictionary<string, object> { [QueryKeys.ExamId] = _examId, [QueryKeys.ExamName] = "Programador C1" });
        return sut;
    }

    private void GivenApiDown()
    {
        _api.GetTopicsAsync(_examId, Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException());
        _api.GetMistakesAsync(_examId, Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException());
    }

    private void GivenCachedContent() =>
        _offline.Content.GetAsync(_examId, Arg.Any<CancellationToken>())
            .Returns(new OfflineContentResponse(_examId, "v1", new ExamResponse(_examId, "c1", "Programador C1", "Diputación", "C1"), [_topic]));

    [Fact]
    public async Task OnAppearingAsync_Online_RefreshesOfflineContentInBackground()
    {
        _api.GetTopicsAsync(_examId, Arg.Any<CancellationToken>()).Returns([new TopicResponse(_topic.Id, 1, _topic.Title, null, 2)]);
        _api.GetMistakesAsync(_examId, Arg.Any<CancellationToken>()).Returns(new MistakeSummaryResponse(0, []));
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        await _offline.Content.Received(1).RefreshAsync(_examId, Arg.Any<CancellationToken>());
        sut.ShowOfflineBanner.ShouldBeFalse();
    }

    [Fact]
    public async Task OnAppearingAsync_ApiDown_ShowsCachedTopicsAndDisablesMistakes()
    {
        GivenApiDown();
        GivenCachedContent();
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.Topics.Count.ShouldBe(1);
        sut.Topics[0].QuestionCountText.ShouldBe("2 preguntas");
        sut.HasAnyQuestions.ShouldBeTrue();
        sut.CanReviewMistakes.ShouldBeFalse();
        sut.MistakesHint.ShouldBe("El repaso de falladas necesita conexión.");
        sut.ShowOfflineBanner.ShouldBeTrue();
        sut.HasError.ShouldBeFalse();
    }

    [Fact]
    public async Task OnAppearingAsync_ApiDownAndNoCache_ShowsNoContentMessage()
    {
        GivenApiDown();
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.Topics.ShouldBeEmpty();
        sut.ErrorMessage.ShouldBe(OfflineQuizException.NoContentDownloaded);
    }

    [Fact]
    public async Task StartTopicQuizCommand_BannerActive_CreatesQuizLocally()
    {
        GivenApiDown();
        GivenCachedContent();
        var quizId = Guid.NewGuid();
        _offline.Quizzes.CreateAsync(_examId, _topic.Id, 10, Arg.Any<CancellationToken>())
            .Returns(new QuizResponse(quizId, _examId, _topic.Id, "ByTopic", DateTimeOffset.UtcNow, null, 0, 0, []));
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await sut.StartTopicQuizCommand.ExecuteAsync(sut.Topics[0]);

        await _api.DidNotReceiveWithAnyArgs().CreateQuizAsync(default!, TestContext.Current.CancellationToken);
        await _navigation.Received(1).GoToAsync(
            Routes.Quiz,
            Arg.Is<IReadOnlyDictionary<string, object>?>(q => (Guid)q![QueryKeys.QuizId] == quizId));
    }

    [Fact]
    public async Task StartRandomQuizCommand_CreateFailsForConnectivity_FallsBackToLocalQuiz()
    {
        _api.GetTopicsAsync(_examId, Arg.Any<CancellationToken>()).Returns([new TopicResponse(_topic.Id, 1, _topic.Title, null, 2)]);
        _api.GetMistakesAsync(_examId, Arg.Any<CancellationToken>()).Returns(new MistakeSummaryResponse(0, []));
        _api.CreateQuizAsync(Arg.Any<CreateQuizRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException());
        var quizId = Guid.NewGuid();
        _offline.Quizzes.CreateAsync(_examId, null, 10, Arg.Any<CancellationToken>())
            .Returns(new QuizResponse(quizId, _examId, null, "Random", DateTimeOffset.UtcNow, null, 0, 0, []));
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await sut.StartRandomQuizCommand.ExecuteAsync(null);

        await _navigation.Received(1).GoToAsync(
            Routes.Quiz,
            Arg.Is<IReadOnlyDictionary<string, object>?>(q => (Guid)q![QueryKeys.QuizId] == quizId));
        sut.ShowOfflineBanner.ShouldBeTrue();
    }

    [Fact]
    public async Task StartRandomQuizCommand_OfflineWithoutQuestions_ShowsServiceMessage()
    {
        GivenApiDown();
        GivenCachedContent();
        _offline.Quizzes.CreateAsync(_examId, null, 10, Arg.Any<CancellationToken>()).ThrowsAsync(new OfflineQuizException());
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await sut.StartRandomQuizCommand.ExecuteAsync(null);

        sut.ErrorMessage.ShouldBe(OfflineQuizException.NoQuestionsAvailable);
        await _navigation.DidNotReceiveWithAnyArgs().GoToAsync(default!, default);
    }
}

public sealed class QuizOfflineViewModelTests
{
    private readonly IOpoStudyApi _api = Substitute.For<IOpoStudyApi>();

    private readonly OfflineDoubles _offline = new();

    private readonly Guid _quizId = Guid.NewGuid();

    private readonly Guid _examId = Guid.NewGuid();

    private QuizViewModel CreateSut()
    {
        var sut = new QuizViewModel(
            _api,
            Substitute.For<INavigationService>(),
            Substitute.For<IDialogService>(),
            ReminderTestSupport.CreateService(),
            _offline.Quizzes,
            _offline.Synchronizer,
            _offline.State,
            NullLogger<QuizViewModel>.Instance);
        sut.ApplyQuery(new Dictionary<string, object> { [QueryKeys.QuizId] = _quizId, [QueryKeys.QuizTitle] = "Test aleatorio" });
        return sut;
    }

    private static QuizQuestionResponse Question(int order, AnswerFeedback? feedback = null) =>
        new(
            Guid.NewGuid(),
            order,
            $"Statement {order}",
            [.. Enumerable.Range(1, 3).Select(i => new QuizOptionResponse(Guid.NewGuid(), i, $"Option {i}"))],
            feedback);

    private QuizResponse Quiz(params QuizQuestionResponse[] questions) =>
        new(_quizId, _examId, null, "Random", DateTimeOffset.UtcNow, null, 0, 0, questions);

    private static AnswerFeedback Feedback(QuizQuestionResponse question, int selected = 0) =>
        new(question.Options[selected].Id, selected == 0, question.Options[0].Id, "Explanation", "Art. 1 CE");

    private void GivenOnlineAnswer(QuizQuestionResponse question) =>
        _api.SubmitAnswerAsync(_quizId, Arg.Is<SubmitAnswerRequest>(r => r.QuestionId == question.QuestionId), Arg.Any<CancellationToken>())
            .Returns(new AnswerFeedbackResponse(
                question.QuestionId,
                Feedback(question),
                false,
                1,
                0,
                1,
                3,
                new DailyProgressResponse(1, 10, false, 0),
                new XpGainResponse(10, 0, 10, 1, false)));

    private void GivenOfflineAnswer(QuizQuestionResponse question, bool finished, int ok, int ko)
    {
        var answered = ok + ko;
        _offline.Quizzes.AnswerAsync(_quizId, question.QuestionId, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new OfflineAnswerResult(Feedback(question), finished, ok, ko, answered, 3));
    }

    [Fact]
    public async Task AnswerCommand_ConnectionDropsMidQuiz_AdoptsOnceAndAnswersLocally()
    {
        var q1 = Question(1);
        var q2 = Question(2);
        var q3 = Question(3);
        _api.GetQuizAsync(_quizId, Arg.Any<CancellationToken>()).Returns(Quiz(q1, q2, q3));
        GivenOnlineAnswer(q1);
        _api.SubmitAnswerAsync(_quizId, Arg.Is<SubmitAnswerRequest>(r => r.QuestionId == q2.QuestionId), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException());
        GivenOfflineAnswer(q2, finished: false, ok: 2, ko: 0);
        GivenOfflineAnswer(q3, finished: true, ok: 3, ko: 0);
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await sut.AnswerCommand.ExecuteAsync(sut.Options[0]);
        await sut.NextCommand.ExecuteAsync(null);
        await sut.AnswerCommand.ExecuteAsync(sut.Options[0]);
        await sut.NextCommand.ExecuteAsync(null);
        await sut.AnswerCommand.ExecuteAsync(sut.Options[0]);

        sut.IsOffline.ShouldBeTrue();
        await _offline.Quizzes.Received(1).AdoptAsync(
            Arg.Is<QuizResponse>(q => q.Questions.Count(x => x.Feedback != null) == 1 && q.CorrectCount == 1),
            "Test aleatorio",
            Arg.Any<CancellationToken>());
        await _api.Received(2).SubmitAnswerAsync(_quizId, Arg.Any<SubmitAnswerRequest>(), Arg.Any<CancellationToken>());
        await _offline.Quizzes.Received(1).AnswerAsync(_quizId, q2.QuestionId, Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _offline.Quizzes.Received(1).AnswerAsync(_quizId, q3.QuestionId, Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        sut.ShowOfflineBanner.ShouldBeTrue();
    }

    [Fact]
    public async Task AnswerCommand_Offline_ShowsFeedbackWithPendingXpAndNoGoalNotice()
    {
        var q1 = Question(1);
        _api.GetQuizAsync(_quizId, Arg.Any<CancellationToken>()).Returns(Quiz(q1));
        _api.SubmitAnswerAsync(_quizId, Arg.Any<SubmitAnswerRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException());
        GivenOfflineAnswer(q1, finished: true, ok: 1, ko: 0);
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await sut.AnswerCommand.ExecuteAsync(sut.Options[0]);

        sut.IsAnswered.ShouldBeTrue();
        sut.IsCorrect.ShouldBeTrue();
        sut.Explanation.ShouldBe("Explanation");
        sut.LegalReferenceText.ShouldBe("Referencia: Art. 1 CE");
        sut.XpText.ShouldBe("XP pendiente de sincronizar");
        sut.HasGoalNotice.ShouldBeFalse();
        sut.ScoreText.ShouldBe("1 acierto · 0 fallos");
    }

    [Fact]
    public async Task NextCommand_OfflineQuizFinished_ShowsSyncMessageInsteadOfXp()
    {
        var q1 = Question(1);
        _api.GetQuizAsync(_quizId, Arg.Any<CancellationToken>()).Returns(Quiz(q1));
        _api.SubmitAnswerAsync(_quizId, Arg.Any<SubmitAnswerRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException());
        GivenOfflineAnswer(q1, finished: true, ok: 1, ko: 0);
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        await sut.AnswerCommand.ExecuteAsync(sut.Options[0]);

        await sut.NextCommand.ExecuteAsync(null);

        sut.IsFinished.ShouldBeTrue();
        sut.QuizXpText.ShouldBe("Tus respuestas se sincronizarán cuando haya conexión.");
        sut.HasQuizXpText.ShouldBeTrue();
        sut.ResultText.ShouldBe("1 de 1 correctas");
    }

    [Fact]
    public async Task AnswerCommand_OfflineQuizFinishedWithInternet_FlushesTheQueue()
    {
        var q1 = Question(1);
        _api.GetQuizAsync(_quizId, Arg.Any<CancellationToken>()).Returns(Quiz(q1));
        _api.SubmitAnswerAsync(_quizId, Arg.Any<SubmitAnswerRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException());
        GivenOfflineAnswer(q1, finished: true, ok: 1, ko: 0);
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await sut.AnswerCommand.ExecuteAsync(sut.Options[0]);

        await _offline.Synchronizer.Received(1).SyncAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnswerCommand_OfflineQuizFinishedWithoutInternet_DoesNotFlush()
    {
        _offline.Network.HasInternet.Returns(false);
        var q1 = Question(1);
        _api.GetQuizAsync(_quizId, Arg.Any<CancellationToken>()).Returns(Quiz(q1));
        _api.SubmitAnswerAsync(_quizId, Arg.Any<SubmitAnswerRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException());
        GivenOfflineAnswer(q1, finished: true, ok: 1, ko: 0);
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await sut.AnswerCommand.ExecuteAsync(sut.Options[0]);

        await _offline.Synchronizer.DidNotReceiveWithAnyArgs().SyncAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AnswerCommand_OfflineCannotGrade_ShowsErrorAndStaysOnQuestion()
    {
        var q1 = Question(1);
        _api.GetQuizAsync(_quizId, Arg.Any<CancellationToken>()).Returns(Quiz(q1));
        _api.SubmitAnswerAsync(_quizId, Arg.Any<SubmitAnswerRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException());
        _offline.Quizzes.AnswerAsync(_quizId, q1.QuestionId, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("not cached"));
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await sut.AnswerCommand.ExecuteAsync(sut.Options[0]);

        sut.HasError.ShouldBeTrue();
        sut.IsAnswered.ShouldBeFalse();
        sut.IsOffline.ShouldBeFalse();
    }

    [Fact]
    public async Task OnAppearingAsync_LocalQuiz_LoadsItWithoutCallingTheApi()
    {
        var q1 = Question(1);
        _offline.Quizzes.GetAsync(_quizId, Arg.Any<CancellationToken>()).Returns(Quiz(q1));
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.IsLoaded.ShouldBeTrue();
        sut.IsOffline.ShouldBeTrue();
        sut.ShowOfflineBanner.ShouldBeTrue();
        await _api.DidNotReceiveWithAnyArgs().GetQuizAsync(default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AnswerCommand_LocalQuiz_AnswersLocallyWithoutCallingTheApi()
    {
        var q1 = Question(1);
        _offline.Quizzes.GetAsync(_quizId, Arg.Any<CancellationToken>()).Returns(Quiz(q1));
        GivenOfflineAnswer(q1, finished: true, ok: 1, ko: 0);
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await sut.AnswerCommand.ExecuteAsync(sut.Options[0]);

        await _api.DidNotReceiveWithAnyArgs().SubmitAnswerAsync(default, default!, TestContext.Current.CancellationToken);
        await _offline.Quizzes.DidNotReceiveWithAnyArgs().AdoptAsync(default!, default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task OnAppearingAsync_OnlineQuiz_ChecksTheLocalCopyFirstAndUsesTheServer()
    {
        _api.GetQuizAsync(_quizId, Arg.Any<CancellationToken>()).Returns(Quiz(Question(1)));
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.IsOffline.ShouldBeFalse();
        sut.ShowOfflineBanner.ShouldBeFalse();
    }
}

public sealed class HomeOfflineViewModelTests
{
    private readonly IOpoStudyApi _api = Substitute.For<IOpoStudyApi>();

    private readonly OfflineDoubles _offline = new();

    private readonly IAuthService _auth = Substitute.For<IAuthService>();

    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();

    private readonly INavigationService _navigation = Substitute.For<INavigationService>();

    private readonly FakeTimeProvider _time = new();

    private static readonly DateTimeOffset Now = new(2026, 11, 20, 10, 0, 0, TimeSpan.Zero);

    public HomeOfflineViewModelTests()
    {
        _api.GetVersionAsync(Arg.Any<CancellationToken>()).Returns(new VersionResponse("0.1.0"));
        _api.GetMeAsync(Arg.Any<CancellationToken>())
            .Returns(new UserProfileResponse(Guid.NewGuid(), Now, null, 10, _time.LocalTimeZone.Id));
        _api.GetStreakAsync(Arg.Any<CancellationToken>()).Returns(Streak());
        _api.GetLevelAsync(Arg.Any<CancellationToken>()).Returns(new LevelResponse(1, 0, 0, 100, 0, 0));
        _api.GetQuizzesAsync(0, 1, Arg.Any<CancellationToken>()).Returns([]);
    }

    private static StreakResponse Streak() => new(10, new DateOnly(2026, 11, 20), 0, false, 0, 0, [], 0, 2, 200, 0);

    private HomeViewModel CreateSut() =>
        new(_api, _navigation, new AppEnvironment(false), _auth, _dialogs, _time, ReminderTestSupport.CreateService(), _offline.Quizzes, _offline.Synchronizer, _offline.State, NullLogger<HomeViewModel>.Instance);

    private static QuizSummaryResponse ServerQuiz(DateTimeOffset startedAt) =>
        new(Guid.NewGuid(), Guid.NewGuid(), null, null, "Random", startedAt, null, 10, 3, 1);

    private LocalQuiz GivenLocalQuiz(DateTimeOffset startedAt, int answered = 2)
    {
        var quiz = new LocalQuiz(Guid.NewGuid(), "uid", Guid.NewGuid(), null, "Random", "Test aleatorio", startedAt, [.. Enumerable.Range(0, 5).Select(_ => Guid.NewGuid())], false);
        _offline.Quizzes.GetLatestUnfinishedAsync(Arg.Any<CancellationToken>()).Returns(quiz);
        _offline.Quizzes.GetAsync(quiz.Id, Arg.Any<CancellationToken>())
            .Returns(new QuizResponse(quiz.Id, quiz.ExamId, null, "Random", startedAt, null, answered, 0, []));
        return quiz;
    }

    [Fact]
    public async Task RefreshCommand_Online_SyncsBeforeLoadingStreakAndLevel()
    {
        var sut = CreateSut();

        await sut.RefreshCommand.ExecuteAsync(null);

        Received.InOrder(() =>
        {
            _offline.Synchronizer.SyncAsync(Arg.Any<CancellationToken>());
            _api.GetStreakAsync(Arg.Any<CancellationToken>());
            _api.GetLevelAsync(Arg.Any<CancellationToken>());
        });
        sut.HasStreak.ShouldBeTrue();
    }

    [Fact]
    public async Task OnAppearingAsync_AlreadyLoaded_SyncsBeforeReloadingTheStreak()
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        _offline.Synchronizer.ClearReceivedCalls();
        _api.ClearReceivedCalls();

        await sut.OnAppearingAsync();

        Received.InOrder(() =>
        {
            _offline.Synchronizer.SyncAsync(Arg.Any<CancellationToken>());
            _api.GetStreakAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task ReconnectSynced_HomeVisible_ReloadsStreakAndLevel()
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        _api.ClearReceivedCalls();

        _offline.Synchronizer.ReconnectSynced += Raise.Event();

        await _api.Received(1).GetStreakAsync(Arg.Any<CancellationToken>());
        await _api.Received(1).GetLevelAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReconnectSynced_HomeDisappeared_DoesNotReload()
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        sut.OnDisappearing();
        _api.ClearReceivedCalls();

        _offline.Synchronizer.ReconnectSynced += Raise.Event();

        await _api.DidNotReceiveWithAnyArgs().GetStreakAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RefreshCommand_SyncReturnsNothing_StillLoadsStreak()
    {
        _offline.Synchronizer.SyncAsync(Arg.Any<CancellationToken>()).Returns(0);
        var sut = CreateSut();

        await sut.RefreshCommand.ExecuteAsync(null);

        sut.HasStreak.ShouldBeTrue();
        sut.HasError.ShouldBeFalse();
    }

    [Fact]
    public async Task RefreshCommand_ApiUnreachable_ShowsBannerAndLocalUnfinishedQuiz()
    {
        _api.GetVersionAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException());
        _api.GetQuizzesAsync(0, 1, Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException());
        var local = GivenLocalQuiz(Now);
        var sut = CreateSut();

        await sut.RefreshCommand.ExecuteAsync(null);

        sut.ShowOfflineBanner.ShouldBeTrue();
        sut.HasUnfinishedQuiz.ShouldBeTrue();
        sut.UnfinishedQuizTitle.ShouldBe("Test aleatorio");
        sut.UnfinishedQuizProgressText.ShouldBe("2 de 5 respondidas");
        await sut.ContinueQuizCommand.ExecuteAsync(null);
        await _navigation.Received(1).GoToAsync(
            Routes.Quiz,
            Arg.Is<IReadOnlyDictionary<string, object>?>(q => (Guid)q![QueryKeys.QuizId] == local.Id));
    }

    [Fact]
    public async Task RefreshCommand_LocalQuizMoreRecentThanServer_ShowsLocalOne()
    {
        _api.GetQuizzesAsync(0, 1, Arg.Any<CancellationToken>()).Returns([ServerQuiz(Now.AddHours(-2))]);
        var local = GivenLocalQuiz(Now);
        var sut = CreateSut();

        await sut.RefreshCommand.ExecuteAsync(null);

        sut.HasUnfinishedQuiz.ShouldBeTrue();
        sut.UnfinishedQuizProgressText.ShouldBe("2 de 5 respondidas");
        await sut.ContinueQuizCommand.ExecuteAsync(null);
        await _navigation.Received(1).GoToAsync(
            Routes.Quiz,
            Arg.Is<IReadOnlyDictionary<string, object>?>(q => (Guid)q![QueryKeys.QuizId] == local.Id));
    }

    [Fact]
    public async Task RefreshCommand_ServerQuizMoreRecentThanLocal_ShowsServerOne()
    {
        var server = ServerQuiz(Now);
        _api.GetQuizzesAsync(0, 1, Arg.Any<CancellationToken>()).Returns([server]);
        GivenLocalQuiz(Now.AddHours(-2));
        var sut = CreateSut();

        await sut.RefreshCommand.ExecuteAsync(null);

        sut.UnfinishedQuizProgressText.ShouldBe("4 de 10 respondidas");
        await sut.ContinueQuizCommand.ExecuteAsync(null);
        await _navigation.Received(1).GoToAsync(
            Routes.Quiz,
            Arg.Is<IReadOnlyDictionary<string, object>?>(q => (Guid)q![QueryKeys.QuizId] == server.Id));
    }

    [Fact]
    public async Task RefreshCommand_LocalQuizWithoutAnswers_HidesTheCard()
    {
        GivenLocalQuiz(Now, answered: 0);
        var sut = CreateSut();

        await sut.RefreshCommand.ExecuteAsync(null);

        sut.HasUnfinishedQuiz.ShouldBeFalse();
    }

    [Fact]
    public async Task RefreshCommand_Syncing_ShowsSyncingTextInTheBanner()
    {
        _offline.Synchronizer.SyncAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            _offline.State.BeginSync(3);
            return Task.FromResult(3);
        });
        var sut = CreateSut();
        string? bannerWhileSyncing = null;
        _api.GetStreakAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            bannerWhileSyncing = sut.OfflineBannerText;
            return Task.FromResult(Streak());
        });

        await sut.RefreshCommand.ExecuteAsync(null);

        bannerWhileSyncing.ShouldBe("Sincronizando 3 respuestas…");
    }

    [Fact]
    public async Task DeleteAccountCommand_Success_DeletesTheLocalDataOfTheUserReadBeforehand()
    {
        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _auth.UserId.Returns("uid-1");
        _auth.DeleteFirebaseAccountAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            _auth.UserId.Returns((string?)null);
            return Task.CompletedTask;
        });

        await CreateSut().DeleteAccountCommand.ExecuteAsync(null);

        await _offline.Quizzes.Received(1).DeleteUserDataAsync("uid-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAccountCommand_ApiFails_KeepsTheLocalData()
    {
        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _auth.UserId.Returns("uid-1");
        _api.DeleteMeAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException());

        await CreateSut().DeleteAccountCommand.ExecuteAsync(null);

        await _offline.Quizzes.DidNotReceiveWithAnyArgs().DeleteUserDataAsync(default!, TestContext.Current.CancellationToken);
    }
}

public sealed class OfflineStateTests
{
    private readonly INetworkStatus _network = Substitute.For<INetworkStatus>();

    private OfflineState CreateSut()
    {
        _network.HasInternet.Returns(true);
        return new OfflineState(_network);
    }

    [Fact]
    public void ReportConnectivityFailure_Online_BecomesOfflineUntilASuccess()
    {
        var sut = CreateSut();

        sut.ReportConnectivityFailure();
        sut.IsOffline.ShouldBeTrue();
        sut.ReportSuccess();

        sut.IsOffline.ShouldBeFalse();
    }

    [Fact]
    public void IsOffline_NoInternet_IsTrueEvenAfterASuccess()
    {
        var sut = CreateSut();
        _network.HasInternet.Returns(false);

        sut.ReportSuccess();

        sut.IsOffline.ShouldBeTrue();
    }

    [Fact]
    public void Changed_NetworkChanges_IsForwarded()
    {
        var sut = CreateSut();
        var raised = 0;
        sut.Changed += (_, _) => raised++;

        _network.Changed += Raise.Event();

        raised.ShouldBe(1);
    }

    [Fact]
    public void BeginSync_RaisesChangedAndExposesTheCount()
    {
        var sut = CreateSut();
        var raised = 0;
        sut.Changed += (_, _) => raised++;

        sut.BeginSync(4);
        sut.EndSync();

        raised.ShouldBe(2);
        sut.SyncingAnswers.ShouldBe(0);
    }
}

public sealed class OfflineSynchronizerTests : IDisposable
{
    private readonly INetworkStatus _network = Substitute.For<INetworkStatus>();

    private readonly IOfflineQuizService _quizzes = Substitute.For<IOfflineQuizService>();

    private readonly IQuizSyncService _sync = Substitute.For<IQuizSyncService>();

    private readonly OfflineState _state;

    private readonly OfflineSynchronizer _sut;

    public OfflineSynchronizerTests()
    {
        _network.HasInternet.Returns(true);
        _state = new OfflineState(_network);
        _sut = new OfflineSynchronizer(_quizzes, _sync, _state, _network, NullLogger<OfflineSynchronizer>.Instance);
    }

    public void Dispose()
    {
        _sut.Dispose();
        _state.Dispose();
    }

    [Fact]
    public async Task SyncAsync_PendingAnswers_FlushesAndShowsProgressMeanwhile()
    {
        _quizzes.PendingAnswerCountAsync(Arg.Any<CancellationToken>()).Returns(3);
        var during = 0;
        _sync.FlushAsync(null, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            during = _state.SyncingAnswers;
            return Task.FromResult(3);
        });

        var sent = await _sut.SyncAsync(TestContext.Current.CancellationToken);

        sent.ShouldBe(3);
        during.ShouldBe(3);
        _state.SyncingAnswers.ShouldBe(0);
    }

    [Fact]
    public async Task SyncAsync_FlushThrows_ReturnsZeroWithoutThrowing()
    {
        _sync.FlushAsync(null, Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException());

        (await _sut.SyncAsync(TestContext.Current.CancellationToken)).ShouldBe(0);
    }

    [Fact]
    public async Task Start_InternetComesBack_Flushes()
    {
        _sut.Start();

        _network.Changed += Raise.Event();

        await _sync.Received(1).FlushAsync(null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Start_InternetComesBack_RaisesReconnectSynced()
    {
        var raised = new TaskCompletionSource();
        _sut.ReconnectSynced += (_, _) => raised.TrySetResult();
        _sut.Start();

        _network.Changed += Raise.Event();

        await raised.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await _sync.Received(1).FlushAsync(null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Start_InternetLost_DoesNotFlush()
    {
        _sut.Start();
        _network.HasInternet.Returns(false);

        _network.Changed += Raise.Event();

        await _sync.DidNotReceiveWithAnyArgs().FlushAsync(null, TestContext.Current.CancellationToken);
    }
}