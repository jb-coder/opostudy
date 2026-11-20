using System.Net;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using OpoStudy.Contracts.Exams;
using OpoStudy.Contracts.Progress;
using OpoStudy.Contracts.Quizzes;
using OpoStudy.Contracts.SystemInfo;
using OpoStudy.Contracts.Users;
using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.Auth;
using OpoStudy.Mobile.Core.Navigation;
using OpoStudy.Mobile.Core.Services;
using OpoStudy.Mobile.Core.ViewModels;

using Refit;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests;

public sealed class TopicsMistakesViewModelTests
{
    private readonly IOpoStudyApi _api = Substitute.For<IOpoStudyApi>();

    private readonly INavigationService _navigation = Substitute.For<INavigationService>();

    private readonly Guid _examId = Guid.NewGuid();

    private readonly TopicResponse _topic1 = new(Guid.NewGuid(), 1, "Tema 1. A", null, 10);

    private readonly TopicResponse _topic2 = new(Guid.NewGuid(), 2, "Tema 2. B", null, 5);

    private TopicsViewModel CreateSut()
    {
        _api.GetTopicsAsync(_examId, Arg.Any<CancellationToken>()).Returns([_topic1, _topic2]);
        var sut = new TopicsViewModel(_api, _navigation, NullLogger<TopicsViewModel>.Instance);
        sut.ApplyQuery(new Dictionary<string, object> { [QueryKeys.ExamId] = _examId, [QueryKeys.ExamName] = "Programador C1" });
        return sut;
    }

    private void GivenMistakes(int topic1, int topic2) =>
        _api.GetMistakesAsync(_examId, Arg.Any<CancellationToken>()).Returns(new MistakeSummaryResponse(
            topic1 + topic2,
            [.. new[] { new TopicMistakeCountResponse(_topic1.Id, topic1), new TopicMistakeCountResponse(_topic2.Id, topic2) }.Where(t => t.Count > 0)]));

    private static async Task<ApiException> ProblemAsync(HttpStatusCode status, string json)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/api/v1/quizzes");
        using var response = new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/problem+json") };
        return await ApiException.Create(request, HttpMethod.Post, response, new RefitSettings());
    }

    [Fact]
    public async Task OnAppearingAsync_NoMistakes_DisablesSwitchWithHint()
    {
        GivenMistakes(0, 0);
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.CanReviewMistakes.ShouldBeFalse();
        sut.MistakesHint.ShouldBe("Aún no tienes preguntas falladas.");
        sut.HasMistakesHint.ShouldBeTrue();
    }

    [Fact]
    public async Task OnAppearingAsync_MistakesLoadFails_DisablesSwitchButKeepsTopics()
    {
        _api.GetMistakesAsync(_examId, Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException());
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.CanReviewMistakes.ShouldBeFalse();
        sut.MistakesHint.ShouldBe("No se han podido cargar tus preguntas falladas.");
        sut.Topics.Count.ShouldBe(2);
        sut.HasError.ShouldBeFalse();
    }

    [Fact]
    public async Task MistakesOnly_Enabled_ShowsCountsAndAvailabilityPerTopic()
    {
        GivenMistakes(1, 0);
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        sut.MistakesOnly = true;

        sut.CanReviewMistakes.ShouldBeTrue();
        sut.Topics.Select(t => t.QuestionCountText).ShouldBe(["1 fallada", "Sin falladas"]);
        sut.Topics.Select(t => t.IsAvailable).ShouldBe([true, false]);
        sut.RandomCardTitle.ShouldBe("Repasar todas las falladas");
        sut.RandomCardSubtitle.ShouldBe("1 pregunta fallada");
        sut.StartTopicQuizCommand.CanExecute(sut.Topics[1]).ShouldBeFalse();
    }

    [Fact]
    public async Task MistakesOnly_EnabledWithSeveral_UsesPlural()
    {
        GivenMistakes(3, 2);
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        sut.MistakesOnly = true;

        sut.Topics.Select(t => t.QuestionCountText).ShouldBe(["3 falladas", "2 falladas"]);
        sut.RandomCardSubtitle.ShouldBe("5 preguntas falladas");
    }

    [Fact]
    public async Task MistakesOnly_Disabled_BehavesLikeBefore()
    {
        GivenMistakes(3, 2);
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        sut.MistakesOnly = true;

        sut.MistakesOnly = false;

        sut.Topics.Select(t => t.QuestionCountText).ShouldBe(["10 preguntas", "5 preguntas"]);
        sut.RandomCardTitle.ShouldBe("Test aleatorio");
        sut.RandomCardSubtitle.ShouldBe("Preguntas de todos los temas");
    }

    [Fact]
    public async Task StartTopicQuizCommand_MistakesOnly_SendsFlagAndReviewTitle()
    {
        GivenMistakes(2, 0);
        var quizId = Guid.NewGuid();
        _api.CreateQuizAsync(Arg.Any<CreateQuizRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QuizResponse(quizId, _examId, _topic1.Id, "Review", DateTimeOffset.UtcNow, null, 0, 0, []));
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        sut.MistakesOnly = true;

        await sut.StartTopicQuizCommand.ExecuteAsync(sut.Topics[0]);

        await _api.Received(1).CreateQuizAsync(new CreateQuizRequest(_examId, _topic1.Id, 10, true), Arg.Any<CancellationToken>());
        await _navigation.Received(1).GoToAsync(
            Routes.Quiz,
            Arg.Is<IReadOnlyDictionary<string, object>>(p => (Guid)p[QueryKeys.QuizId] == quizId && (string)p[QueryKeys.QuizTitle] == "Repaso: Tema 1. A"));
    }

    [Fact]
    public async Task StartRandomQuizCommand_MistakesOnly_SendsFlagAndReviewAllTitle()
    {
        GivenMistakes(2, 1);
        _api.CreateQuizAsync(Arg.Any<CreateQuizRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QuizResponse(Guid.NewGuid(), _examId, null, "Review", DateTimeOffset.UtcNow, null, 0, 0, []));
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        sut.MistakesOnly = true;

        await sut.StartRandomQuizCommand.ExecuteAsync(null);

        await _api.Received(1).CreateQuizAsync(new CreateQuizRequest(_examId, null, 10, true), Arg.Any<CancellationToken>());
        await _navigation.Received(1).GoToAsync(
            Routes.Quiz,
            Arg.Is<IReadOnlyDictionary<string, object>>(p => (string)p[QueryKeys.QuizTitle] == "Repaso de falladas"));
    }

    [Fact]
    public async Task StartRandomQuizCommand_NoMistakesProblem_ShowsMessageAndReloadsMistakes()
    {
        GivenMistakes(2, 0);
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        sut.MistakesOnly = true;
        GivenMistakes(0, 0);
        _api.CreateQuizAsync(Arg.Any<CreateQuizRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(await ProblemAsync(
                HttpStatusCode.UnprocessableEntity,
                """{"title":"No tienes preguntas falladas pendientes de repasar aquí.","status":422,"code":"quiz.no_mistakes"}"""));

        await sut.StartRandomQuizCommand.ExecuteAsync(null);

        sut.ErrorMessage.ShouldBe("No tienes preguntas falladas pendientes de repasar aquí.");
        sut.MistakeTotal.ShouldBe(0);
        sut.MistakesOnly.ShouldBeFalse();
        await _api.Received(2).GetMistakesAsync(_examId, Arg.Any<CancellationToken>());
        await _navigation.DidNotReceiveWithAnyArgs().GoToAsync(default!, default);
    }

    [Fact]
    public async Task OnAppearingAsync_SecondTime_ReloadsMistakesButNotTopics()
    {
        GivenMistakes(2, 0);
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        sut.MistakesOnly = true;
        GivenMistakes(0, 0);

        await sut.OnAppearingAsync();

        await _api.Received(1).GetTopicsAsync(_examId, Arg.Any<CancellationToken>());
        await _api.Received(2).GetMistakesAsync(_examId, Arg.Any<CancellationToken>());
        sut.MistakesOnly.ShouldBeFalse();
        sut.CanReviewMistakes.ShouldBeFalse();
    }
}

public sealed class QuizBackAndReviewViewModelTests
{
    private readonly IOpoStudyApi _api = Substitute.For<IOpoStudyApi>();

    private readonly INavigationService _navigation = Substitute.For<INavigationService>();

    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();

    private readonly Guid _quizId = Guid.NewGuid();

    private readonly Guid _examId = Guid.NewGuid();

    private QuizViewModel CreateSut()
    {
        var sut = new QuizViewModel(_api, _navigation, _dialogs, ReminderTestSupport.CreateService(), NullLogger<QuizViewModel>.Instance);
        sut.ApplyQuery(new Dictionary<string, object> { [QueryKeys.QuizId] = _quizId, [QueryKeys.QuizTitle] = "Repaso de falladas" });
        return sut;
    }

    private static QuizQuestionResponse Question(int order, bool answered)
    {
        var options = Enumerable.Range(0, 3).Select(i => new QuizOptionResponse(Guid.NewGuid(), i + 1, $"Option {i + 1}")).ToList();
        AnswerFeedback? feedback = answered ? new AnswerFeedback(options[0].Id, true, options[0].Id, "Explanation", "Art. 1 CE") : null;
        return new QuizQuestionResponse(Guid.NewGuid(), order, $"Statement {order}", options, feedback);
    }

    private void GivenQuiz(string mode, Guid? topicId, params QuizQuestionResponse[] questions) =>
        _api.GetQuizAsync(_quizId, Arg.Any<CancellationToken>())
            .Returns(new QuizResponse(_quizId, _examId, topicId, mode, DateTimeOffset.UtcNow, null, 0, 0, questions));

    [Fact]
    public async Task BackCommand_StartedQuizAndConfirmed_Navigates()
    {
        GivenQuiz("Random", null, Question(1, answered: true), Question(2, answered: false));
        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await sut.BackCommand.ExecuteAsync(null);

        await _dialogs.Received(1).ConfirmAsync(
            "¿Salir del test?",
            "Tus respuestas se guardan y podrás continuarlo desde el inicio.",
            "Salir",
            "Seguir con el test");
        await _navigation.Received(1).GoToAsync(Routes.Back);
    }

    [Fact]
    public async Task BackCommand_ExecutedTwiceConcurrently_AsksOnce()
    {
        GivenQuiz("Random", null, Question(1, answered: true), Question(2, answered: false));
        var answer = new TaskCompletionSource<bool>();
        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(answer.Task);
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        var first = sut.BackCommand.ExecuteAsync(null);
        var second = sut.BackCommand.ExecuteAsync(null);
        answer.SetResult(true);
        await Task.WhenAll(first, second);

        await _dialogs.Received(1).ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        await _navigation.Received(1).GoToAsync(Routes.Back);
    }

    [Fact]
    public async Task BackCommand_StartedQuizAndRejected_DoesNotNavigate()
    {
        GivenQuiz("Random", null, Question(1, answered: true), Question(2, answered: false));
        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await sut.BackCommand.ExecuteAsync(null);

        await _navigation.DidNotReceiveWithAnyArgs().GoToAsync(default!, default);
    }

    [Fact]
    public async Task BackCommand_NoAnswers_NavigatesWithoutAsking()
    {
        GivenQuiz("Random", null, Question(1, answered: false));
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await sut.BackCommand.ExecuteAsync(null);

        await _dialogs.DidNotReceiveWithAnyArgs().ConfirmAsync(default!, default!, default!, default!);
        await _navigation.Received(1).GoToAsync(Routes.Back);
    }

    [Fact]
    public async Task BackCommand_FinishedQuiz_NavigatesWithoutAsking()
    {
        GivenQuiz("Random", null, Question(1, answered: true));
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await sut.BackCommand.ExecuteAsync(null);

        await _dialogs.DidNotReceiveWithAnyArgs().ConfirmAsync(default!, default!, default!, default!);
        await _navigation.Received(1).GoToAsync(Routes.Back);
    }

    [Fact]
    public async Task OnAppearingAsync_FinishedReviewByExam_ShowsTotalLeftAndKeepReviewingButton()
    {
        GivenQuiz("Review", null, Question(1, answered: true));
        _api.GetMistakesAsync(_examId, Arg.Any<CancellationToken>())
            .Returns(new MistakeSummaryResponse(3, [new TopicMistakeCountResponse(Guid.NewGuid(), 2), new TopicMistakeCountResponse(Guid.NewGuid(), 1)]));
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.MistakesLeftText.ShouldBe("Te quedan 3 preguntas falladas");
        sut.HasMistakesLeftText.ShouldBeTrue();
        sut.AnotherQuizText.ShouldBe("Seguir repasando");
    }

    [Fact]
    public async Task OnAppearingAsync_FinishedReviewByTopic_UsesTopicCount()
    {
        var topicId = Guid.NewGuid();
        GivenQuiz("Review", topicId, Question(1, answered: true));
        _api.GetMistakesAsync(_examId, Arg.Any<CancellationToken>())
            .Returns(new MistakeSummaryResponse(4, [new TopicMistakeCountResponse(topicId, 1), new TopicMistakeCountResponse(Guid.NewGuid(), 3)]));
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.MistakesLeftText.ShouldBe("Te queda 1 pregunta fallada");
        sut.AnotherQuizText.ShouldBe("Seguir repasando");
    }

    [Fact]
    public async Task OnAppearingAsync_FinishedReviewWithNoMistakesLeft_CongratulatesAndKeepsDefaultButton()
    {
        var topicId = Guid.NewGuid();
        GivenQuiz("Review", topicId, Question(1, answered: true));
        _api.GetMistakesAsync(_examId, Arg.Any<CancellationToken>())
            .Returns(new MistakeSummaryResponse(2, [new TopicMistakeCountResponse(Guid.NewGuid(), 2)]));
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.MistakesLeftText.ShouldBe("¡Has repasado todas tus preguntas falladas!");
        sut.AnotherQuizText.ShouldBe("Hacer otro test");
    }

    [Fact]
    public async Task OnAppearingAsync_FinishedReviewAndLoadFails_OmitsLineWithoutError()
    {
        GivenQuiz("Review", null, Question(1, answered: true));
        _api.GetMistakesAsync(_examId, Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException());
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.HasMistakesLeftText.ShouldBeFalse();
        sut.HasError.ShouldBeFalse();
        sut.AnotherQuizText.ShouldBe("Hacer otro test");
        sut.IsFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task NextCommand_AfterLastQuestionOfAReview_LoadsMistakesLeft()
    {
        var pending = Question(2, answered: false);
        GivenQuiz("Review", null, Question(1, answered: true), pending);
        var correct = pending.Options[0].Id;
        _api.SubmitAnswerAsync(_quizId, Arg.Any<SubmitAnswerRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AnswerFeedbackResponse(
                pending.QuestionId,
                new AnswerFeedback(correct, true, correct, "Explanation", "Art. 1 CE"),
                true,
                2,
                0,
                2,
                2,
                new DailyProgressResponse(1, 10, false, 0),
                new XpGainResponse(10, 0, 10, 1, false)));
        _api.GetMistakesAsync(_examId, Arg.Any<CancellationToken>()).Returns(new MistakeSummaryResponse(0, []));
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        await sut.AnswerCommand.ExecuteAsync(sut.Options[0]);
        await _api.DidNotReceiveWithAnyArgs().GetMistakesAsync(default, TestContext.Current.CancellationToken);

        await sut.NextCommand.ExecuteAsync(null);

        sut.IsFinished.ShouldBeTrue();
        sut.MistakesLeftText.ShouldBe("¡Has repasado todas tus preguntas falladas!");
    }

    [Fact]
    public async Task OnAppearingAsync_FinishedRandomQuiz_DoesNotLoadMistakes()
    {
        GivenQuiz("Random", null, Question(1, answered: true));
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.HasMistakesLeftText.ShouldBeFalse();
        await _api.DidNotReceiveWithAnyArgs().GetMistakesAsync(default, TestContext.Current.CancellationToken);
    }
}

public sealed class HomeUnfinishedQuizViewModelTests
{
    private readonly IOpoStudyApi _api = Substitute.For<IOpoStudyApi>();

    private readonly INavigationService _navigation = Substitute.For<INavigationService>();

    private readonly IAuthService _auth = Substitute.For<IAuthService>();

    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();

    private readonly FakeTimeProvider _time = new();

    public HomeUnfinishedQuizViewModelTests()
    {
        _api.GetVersionAsync(Arg.Any<CancellationToken>()).Returns(new VersionResponse("0.1.0"));
        _api.GetMeAsync(Arg.Any<CancellationToken>())
            .Returns(new UserProfileResponse(Guid.NewGuid(), new DateTimeOffset(2026, 3, 5, 10, 0, 0, TimeSpan.Zero), null, 10, _time.LocalTimeZone.Id));
        _api.GetStreakAsync(Arg.Any<CancellationToken>()).Returns(new StreakResponse(10, new DateOnly(2026, 11, 20), 0, false, 0, 0, [], 0, 2, 200, 0));
        _api.GetLevelAsync(Arg.Any<CancellationToken>()).Returns(new LevelResponse(1, 0, 0, 100, 0, 0));
    }

    private HomeViewModel CreateSut() =>
        new(_api, _navigation, new AppEnvironment(false), _auth, _dialogs, _time, ReminderTestSupport.CreateService(), NullLogger<HomeViewModel>.Instance);

    private static QuizSummaryResponse Summary(string mode, string? topicTitle, DateTimeOffset? finishedAt, int correct, int incorrect, int total = 10) =>
        new(Guid.NewGuid(), Guid.NewGuid(), topicTitle is null ? null : Guid.NewGuid(), topicTitle, mode, DateTimeOffset.UtcNow, finishedAt, total, correct, incorrect);

    private void GivenLatest(QuizSummaryResponse? quiz) =>
        _api.GetQuizzesAsync(0, 1, Arg.Any<CancellationToken>()).Returns(quiz is null ? [] : [quiz]);

    [Theory]
    [InlineData("ByTopic", "Tema 1. A", "Tema 1. A")]
    [InlineData("Random", null, "Test aleatorio")]
    [InlineData("Review", "Tema 1. A", "Repaso: Tema 1. A")]
    [InlineData("Review", null, "Repaso de falladas")]
    public async Task RefreshCommand_UnfinishedQuizWithAnswers_ShowsCardWithTitleByMode(string mode, string? topicTitle, string expectedTitle)
    {
        GivenLatest(Summary(mode, topicTitle, null, 2, 1));
        var sut = CreateSut();

        await sut.RefreshCommand.ExecuteAsync(null);

        sut.HasUnfinishedQuiz.ShouldBeTrue();
        sut.UnfinishedQuizTitle.ShouldBe(expectedTitle);
        sut.UnfinishedQuizProgressText.ShouldBe("3 de 10 respondidas");
    }

    [Fact]
    public async Task RefreshCommand_FinishedQuiz_HidesCard()
    {
        GivenLatest(Summary("Random", null, DateTimeOffset.UtcNow, 5, 5));
        var sut = CreateSut();

        await sut.RefreshCommand.ExecuteAsync(null);

        sut.HasUnfinishedQuiz.ShouldBeFalse();
    }

    [Fact]
    public async Task RefreshCommand_UnfinishedQuizWithoutAnswers_HidesCard()
    {
        GivenLatest(Summary("Random", null, null, 0, 0));
        var sut = CreateSut();

        await sut.RefreshCommand.ExecuteAsync(null);

        sut.HasUnfinishedQuiz.ShouldBeFalse();
    }

    [Fact]
    public async Task RefreshCommand_NoQuizzes_HidesCard()
    {
        GivenLatest(null);
        var sut = CreateSut();

        await sut.RefreshCommand.ExecuteAsync(null);

        sut.HasUnfinishedQuiz.ShouldBeFalse();
    }

    [Fact]
    public async Task RefreshCommand_QuizzesLoadFails_HidesCardWithoutError()
    {
        _api.GetQuizzesAsync(0, 1, Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException());
        var sut = CreateSut();

        await sut.RefreshCommand.ExecuteAsync(null);

        sut.HasUnfinishedQuiz.ShouldBeFalse();
        sut.HasError.ShouldBeFalse();
    }

    [Fact]
    public async Task OnAppearingAsync_QuizFinishedWhileAway_HidesCard()
    {
        GivenLatest(Summary("Random", null, null, 2, 1));
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        sut.HasUnfinishedQuiz.ShouldBeTrue();
        GivenLatest(Summary("Random", null, DateTimeOffset.UtcNow, 5, 5));

        await sut.OnAppearingAsync();

        sut.HasUnfinishedQuiz.ShouldBeFalse();
    }

    [Fact]
    public async Task ContinueQuizCommand_Executed_NavigatesToQuizWithIdAndTitle()
    {
        var quiz = Summary("Review", "Tema 1. A", null, 1, 1);
        GivenLatest(quiz);
        var sut = CreateSut();
        await sut.RefreshCommand.ExecuteAsync(null);

        await sut.ContinueQuizCommand.ExecuteAsync(null);

        await _navigation.Received(1).GoToAsync(
            Routes.Quiz,
            Arg.Is<IReadOnlyDictionary<string, object>>(p => (Guid)p[QueryKeys.QuizId] == quiz.Id && (string)p[QueryKeys.QuizTitle] == "Repaso: Tema 1. A"));
    }
}

public sealed class MistakesSerializationTests
{
    [Fact]
    public void Deserialize_MistakeSummaryJson_MapsTopics()
    {
        const string json = """{"total":3,"topics":[{"topicId":"11111111-1111-1111-1111-111111111111","count":3}]}""";

        var summary = JsonSerializer.Deserialize(json, ApiJsonContext.Default.MistakeSummaryResponse);

        summary.ShouldNotBeNull();
        summary.Total.ShouldBe(3);
        summary.Topics.Single().Count.ShouldBe(3);
    }

    [Fact]
    public void Deserialize_QuizSummaryListJson_MapsItems()
    {
        const string json = """
            [{"id":"11111111-1111-1111-1111-111111111111","examId":"22222222-2222-2222-2222-222222222222","topicId":null,
              "topicTitle":null,"mode":"Review","startedAt":"2026-01-01T10:00:00+00:00","finishedAt":null,
              "questionCount":10,"correctCount":2,"incorrectCount":1}]
            """;

        var quizzes = JsonSerializer.Deserialize(json, ApiJsonContext.Default.IReadOnlyListQuizSummaryResponse);

        quizzes.ShouldNotBeNull();
        quizzes.Single().Mode.ShouldBe("Review");
        quizzes.Single().FinishedAt.ShouldBeNull();
    }

    [Fact]
    public async Task GetQuizzesAsync_Called_UsesSkipAndTakeQuery()
    {
        using var handler = new StubHandler("[]");
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5054") };
        var api = RestService.ForGenerated<IOpoStudyApi>(client, ApiJsonContext.Default);

        await api.GetQuizzesAsync(0, 1, TestContext.Current.CancellationToken);

        handler.LastRequestUri.ShouldBe(new Uri("http://localhost:5054/api/v1/quizzes?skip=0&take=1"));
    }

    private sealed class StubHandler(string json) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }
}