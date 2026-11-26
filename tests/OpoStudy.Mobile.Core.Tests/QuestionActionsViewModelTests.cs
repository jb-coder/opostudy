using System.Net;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using OpoStudy.Contracts.Exams;
using OpoStudy.Contracts.Questions;
using OpoStudy.Contracts.Quizzes;
using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.Navigation;
using OpoStudy.Mobile.Core.Services;
using OpoStudy.Mobile.Core.ViewModels;

using Refit;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests;

public sealed class QuizQuestionActionsViewModelTests
{
    private readonly IOpoStudyApi _api = Substitute.For<IOpoStudyApi>();

    private readonly OfflineDoubles _offline = new();

    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();

    private readonly INavigationService _navigation = Substitute.For<INavigationService>();

    private readonly Guid _quizId = Guid.NewGuid();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private QuizViewModel CreateSut()
    {
        var sut = new QuizViewModel(
            _api,
            _navigation,
            _dialogs,
            ReminderTestSupport.CreateService(new FakeReminderStore()),
            _offline.Quizzes,
            _offline.Synchronizer,
            _offline.State,
            NullLogger<QuizViewModel>.Instance);
        sut.ApplyQuery(new Dictionary<string, object> { [QueryKeys.QuizId] = _quizId, [QueryKeys.QuizTitle] = "Test" });
        return sut;
    }

    private static QuizQuestionResponse Question(int order, bool bookmarked) =>
        new(
            Guid.NewGuid(),
            order,
            $"Statement {order}",
            [.. Enumerable.Range(1, 3).Select(i => new QuizOptionResponse(Guid.NewGuid(), i, $"Option {i}"))],
            null,
            bookmarked);

    private QuizResponse Quiz(params QuizQuestionResponse[] questions) =>
        new(_quizId, Guid.NewGuid(), null, "Random", DateTimeOffset.UtcNow, null, 0, 0, questions);

    private async Task<(QuizViewModel Sut, QuizQuestionResponse First, QuizQuestionResponse Second)> LoadedAsync(bool firstBookmarked = false, bool secondBookmarked = false)
    {
        var first = Question(1, firstBookmarked);
        var second = Question(2, secondBookmarked);
        _api.GetQuizAsync(_quizId, Arg.Any<CancellationToken>()).Returns(Quiz(first, second));
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        return (sut, first, second);
    }

    private static async Task<ApiException> ProblemAsync(HttpStatusCode status, string json)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/api/v1/questions/x/reports");
        using var response = new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/problem+json") };
        return await ApiException.Create(request, HttpMethod.Post, response, new RefitSettings());
    }

    [Fact]
    public async Task OnAppearingAsync_BookmarkedQuestion_ShowsMarcadaState()
    {
        var (sut, _, _) = await LoadedAsync(firstBookmarked: true);

        sut.IsCurrentBookmarked.ShouldBeTrue();
        sut.BookmarkText.ShouldBe("Marcada");
        sut.BookmarkDescription.ShouldBe("Quitar marca");
        sut.ShowQuestionActions.ShouldBeTrue();
    }

    [Fact]
    public async Task ToggleBookmarkCommand_NotBookmarked_BookmarksOptimistically()
    {
        var (sut, first, _) = await LoadedAsync();
        var pending = new TaskCompletionSource();
        _api.BookmarkQuestionAsync(first.QuestionId, Arg.Any<CancellationToken>()).Returns(pending.Task);

        var run = sut.ToggleBookmarkCommand.ExecuteAsync(null);

        sut.IsCurrentBookmarked.ShouldBeTrue();
        sut.BookmarkText.ShouldBe("Marcada");
        sut.ToggleBookmarkCommand.CanExecute(null).ShouldBeFalse();
        pending.SetResult();
        await run;
        sut.ToggleBookmarkCommand.CanExecute(null).ShouldBeTrue();
        await _api.Received(1).BookmarkQuestionAsync(first.QuestionId, Arg.Any<CancellationToken>());
        sut.HasError.ShouldBeFalse();
    }

    [Fact]
    public async Task ToggleBookmarkCommand_Bookmarked_Unbookmarks()
    {
        var (sut, first, _) = await LoadedAsync(firstBookmarked: true);

        await sut.ToggleBookmarkCommand.ExecuteAsync(null);

        sut.IsCurrentBookmarked.ShouldBeFalse();
        sut.BookmarkText.ShouldBe("Marcar");
        await _api.Received(1).UnbookmarkQuestionAsync(first.QuestionId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ToggleBookmarkCommand_ApiFails_RevertsAndShowsError()
    {
        var (sut, first, _) = await LoadedAsync();
        _api.BookmarkQuestionAsync(first.QuestionId, Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException());

        await sut.ToggleBookmarkCommand.ExecuteAsync(null);

        sut.IsCurrentBookmarked.ShouldBeFalse();
        sut.ErrorMessage.ShouldBe(ApiErrorMessages.Unreachable);
        sut.ToggleBookmarkCommand.CanExecute(null).ShouldBeTrue();
    }

    [Fact]
    public async Task NextCommand_AfterBookmarking_KeepsStatePerQuestion()
    {
        var (sut, first, second) = await LoadedAsync(secondBookmarked: true);
        await sut.ToggleBookmarkCommand.ExecuteAsync(null);
        sut.IsCurrentBookmarked.ShouldBeTrue();

        // Moves to the next question the way the quiz does after an answer is given.
        var optionId = sut.Options[0].Id;
        _api.SubmitAnswerAsync(_quizId, Arg.Any<SubmitAnswerRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AnswerFeedbackResponse(
                first.QuestionId,
                new AnswerFeedback(optionId, true, optionId, "E", "R"),
                false,
                1,
                0,
                1,
                2,
                new OpoStudy.Contracts.Progress.DailyProgressResponse(1, 10, false, 0),
                new OpoStudy.Contracts.Progress.XpGainResponse(10, 0, 10, 1, false)));
        await sut.AnswerCommand.ExecuteAsync(sut.Options[0]);
        await sut.NextCommand.ExecuteAsync(null);

        sut.CurrentStatement.ShouldBe(second.Statement);
        sut.IsCurrentBookmarked.ShouldBeTrue();
    }

    [Fact]
    public async Task OnAppearingAsync_QuizCorrectedOffline_HidesQuestionActions()
    {
        var question = Question(1, false);
        _offline.Quizzes.GetAsync(_quizId, Arg.Any<CancellationToken>()).Returns(Quiz(question));
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.IsOffline.ShouldBeTrue();
        sut.ShowQuestionActions.ShouldBeFalse();
    }

    [Fact]
    public async Task ReportCommand_ReasonAndComment_SendsReportAndThanks()
    {
        var (sut, first, _) = await LoadedAsync();
        _dialogs.ChooseAsync(Arg.Any<string>(), "Cancelar", Arg.Any<IReadOnlyList<string>>()).Returns("Ley derogada o modificada");
        _dialogs.PromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), 500).Returns("  Art. 5 derogado  ");

        await sut.ReportCommand.ExecuteAsync(null);

        await _api.Received(1).ReportQuestionAsync(
            first.QuestionId,
            new CreateQuestionReportRequest("OutdatedLegislation", "Art. 5 derogado"),
            Arg.Any<CancellationToken>());
        await _dialogs.Received(1).AlertAsync("Gracias", "Revisaremos la pregunta.");
    }

    [Fact]
    public async Task ReportCommand_EmptyOptionalComment_SendsNullComment()
    {
        var (sut, first, _) = await LoadedAsync();
        _dialogs.ChooseAsync(Arg.Any<string>(), "Cancelar", Arg.Any<IReadOnlyList<string>>()).Returns("Errata");
        _dialogs.PromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), 500).Returns(string.Empty);

        await sut.ReportCommand.ExecuteAsync(null);

        await _api.Received(1).ReportQuestionAsync(first.QuestionId, new CreateQuestionReportRequest("Typo", null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReportCommand_CancelAtReason_SendsNothing()
    {
        var (sut, _, _) = await LoadedAsync();
        _dialogs.ChooseAsync(Arg.Any<string>(), "Cancelar", Arg.Any<IReadOnlyList<string>>()).Returns((string?)null);

        await sut.ReportCommand.ExecuteAsync(null);

        await _dialogs.DidNotReceive().PromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>());
        await _api.DidNotReceive().ReportQuestionAsync(Arg.Any<Guid>(), Arg.Any<CreateQuestionReportRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReportCommand_CancelAtComment_SendsNothing()
    {
        var (sut, _, _) = await LoadedAsync();
        _dialogs.ChooseAsync(Arg.Any<string>(), "Cancelar", Arg.Any<IReadOnlyList<string>>()).Returns("Errata");
        _dialogs.PromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>()).Returns((string?)null);

        await sut.ReportCommand.ExecuteAsync(null);

        await _api.DidNotReceive().ReportQuestionAsync(Arg.Any<Guid>(), Arg.Any<CreateQuestionReportRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReportCommand_OtherWithEmptyCommentTwice_AsksAgainOnceAndSendsNothing()
    {
        var (sut, _, _) = await LoadedAsync();
        _dialogs.ChooseAsync(Arg.Any<string>(), "Cancelar", Arg.Any<IReadOnlyList<string>>()).Returns("Otro");
        _dialogs.PromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>()).Returns("   ");

        await sut.ReportCommand.ExecuteAsync(null);

        await _dialogs.Received(2).PromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>());
        await _dialogs.Received(2).PromptAsync(Arg.Any<string>(), "Cuéntanos qué está mal.", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>());
        await _api.DidNotReceive().ReportQuestionAsync(Arg.Any<Guid>(), Arg.Any<CreateQuestionReportRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReportCommand_OtherWithEmptyThenFilledComment_Sends()
    {
        var (sut, first, _) = await LoadedAsync();
        _dialogs.ChooseAsync(Arg.Any<string>(), "Cancelar", Arg.Any<IReadOnlyList<string>>()).Returns("Otro");
        _dialogs.PromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>()).Returns("", "Algo raro");

        await sut.ReportCommand.ExecuteAsync(null);

        await _api.Received(1).ReportQuestionAsync(first.QuestionId, new CreateQuestionReportRequest("Other", "Algo raro"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReportCommand_AlreadyReported_ShowsProblemMessage()
    {
        var (sut, _, _) = await LoadedAsync();
        _dialogs.ChooseAsync(Arg.Any<string>(), "Cancelar", Arg.Any<IReadOnlyList<string>>()).Returns("Errata");
        _dialogs.PromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>()).Returns("x");
        _api.ReportQuestionAsync(Arg.Any<Guid>(), Arg.Any<CreateQuestionReportRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(await ProblemAsync(HttpStatusCode.Conflict, """{"title":"Ya reportaste esta pregunta. La revisaremos pronto.","status":409,"code":"question.already_reported"}"""));

        await sut.ReportCommand.ExecuteAsync(null);

        await _dialogs.Received(1).AlertAsync(Arg.Any<string>(), "Ya reportaste esta pregunta. La revisaremos pronto.");
        sut.HasError.ShouldBeFalse();
    }

    [Fact]
    public async Task ReportCommand_OtherError_ShowsErrorMessage()
    {
        var (sut, _, _) = await LoadedAsync();
        _dialogs.ChooseAsync(Arg.Any<string>(), "Cancelar", Arg.Any<IReadOnlyList<string>>()).Returns("Errata");
        _dialogs.PromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>()).Returns("x");
        _api.ReportQuestionAsync(Arg.Any<Guid>(), Arg.Any<CreateQuestionReportRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException());

        await sut.ReportCommand.ExecuteAsync(null);

        sut.ErrorMessage.ShouldBe(ApiErrorMessages.Unreachable);
        await _dialogs.DidNotReceive().AlertAsync("Gracias", Arg.Any<string>());
    }

    [Fact]
    public void ReasonOf_Labels_MapToApiNames()
    {
        QuestionReportReasons.ReasonOf("La respuesta correcta está mal").ShouldBe("WrongAnswer");
        QuestionReportReasons.ReasonOf("Enunciado u opciones confusos").ShouldBe("UnclearStatement");
        QuestionReportReasons.ReasonOf("Otro").ShouldBe("Other");
        QuestionReportReasons.ReasonOf("nope").ShouldBeNull();
        QuestionReportReasons.Labels.Count.ShouldBe(5);
    }
}

public sealed class TopicsBookmarksViewModelTests
{
    private readonly IOpoStudyApi _api = Substitute.For<IOpoStudyApi>();

    private readonly OfflineDoubles _offline = new();

    private readonly INavigationService _navigation = Substitute.For<INavigationService>();

    private readonly Guid _examId = Guid.NewGuid();

    private readonly TopicResponse _topic = new(Guid.NewGuid(), 1, "Tema 1. A", null, 10);

    private async Task<TopicsViewModel> LoadedAsync(int mistakes = 0)
    {
        _api.GetTopicsAsync(_examId, Arg.Any<CancellationToken>()).Returns([_topic]);
        _api.GetMistakesAsync(_examId, Arg.Any<CancellationToken>()).Returns(new MistakeSummaryResponse(
            mistakes,
            mistakes > 0 ? [new TopicMistakeCountResponse(_topic.Id, mistakes)] : []));
        var sut = new TopicsViewModel(_api, _navigation, _offline.Content, _offline.Quizzes, _offline.State, NullLogger<TopicsViewModel>.Instance);
        sut.ApplyQuery(new Dictionary<string, object> { [QueryKeys.ExamId] = _examId, [QueryKeys.ExamName] = "Programador C1" });
        await sut.OnAppearingAsync();
        return sut;
    }

    [Fact]
    public async Task BookmarkedOnly_EnabledWhileMistakesOnly_TurnsMistakesOff()
    {
        var sut = await LoadedAsync(mistakes: 2);
        sut.MistakesOnly = true;

        sut.BookmarkedOnly = true;

        sut.BookmarkedOnly.ShouldBeTrue();
        sut.MistakesOnly.ShouldBeFalse();
    }

    [Fact]
    public async Task MistakesOnly_EnabledWhileBookmarkedOnly_TurnsBookmarksOff()
    {
        var sut = await LoadedAsync(mistakes: 2);
        sut.BookmarkedOnly = true;

        sut.MistakesOnly = true;

        sut.MistakesOnly.ShouldBeTrue();
        sut.BookmarkedOnly.ShouldBeFalse();
    }

    [Fact]
    public async Task StartTopicQuizCommand_BookmarkedOnly_SendsFlagAndTitle()
    {
        var quizId = Guid.NewGuid();
        _api.CreateQuizAsync(Arg.Any<CreateQuizRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QuizResponse(quizId, _examId, _topic.Id, "Bookmarks", DateTimeOffset.UtcNow, null, 0, 0, []));
        var sut = await LoadedAsync();
        sut.BookmarkedOnly = true;

        await sut.StartTopicQuizCommand.ExecuteAsync(sut.Topics[0]);

        await _api.Received(1).CreateQuizAsync(
            new CreateQuizRequest(_examId, _topic.Id, 10, BookmarkedOnly: true),
            Arg.Any<CancellationToken>());
        await _navigation.Received(1).GoToAsync(
            Routes.Quiz,
            Arg.Is<IReadOnlyDictionary<string, object>>(p => (Guid)p[QueryKeys.QuizId] == quizId && (string)p[QueryKeys.QuizTitle] == "Preguntas marcadas"));
    }

    [Fact]
    public async Task StartRandomQuizCommand_BookmarkedOnly_SendsFlagWithoutTopic()
    {
        _api.CreateQuizAsync(Arg.Any<CreateQuizRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QuizResponse(Guid.NewGuid(), _examId, null, "Bookmarks", DateTimeOffset.UtcNow, null, 0, 0, []));
        var sut = await LoadedAsync();
        sut.BookmarkedOnly = true;

        await sut.StartRandomQuizCommand.ExecuteAsync(null);

        await _api.Received(1).CreateQuizAsync(new CreateQuizRequest(_examId, null, 10, BookmarkedOnly: true), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartRandomQuizCommand_BookmarkedOnlyConnectionLost_ShowsErrorAndDoesNotUseOfflineCopy()
    {
        _api.CreateQuizAsync(Arg.Any<CreateQuizRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException());
        var sut = await LoadedAsync();
        sut.BookmarkedOnly = true;

        await sut.StartRandomQuizCommand.ExecuteAsync(null);

        sut.ErrorMessage.ShouldBe(ApiErrorMessages.Unreachable);
        await _offline.Quizzes.DidNotReceive().CreateAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}

public sealed class QuestionContractsTests
{
    [Fact]
    public void QuizTitlesFor_BookmarksSummary_UsesBookmarksTitle()
    {
        var quiz = new QuizSummaryResponse(Guid.NewGuid(), Guid.NewGuid(), null, null, "Bookmarks", DateTimeOffset.UtcNow, null, 5, 1, 0);

        QuizTitles.For(quiz).ShouldBe("Preguntas marcadas");
    }

    [Fact]
    public void Serialize_CreateQuestionReportRequest_UsesCamelCase()
    {
        var json = JsonSerializer.Serialize(new CreateQuestionReportRequest("Typo", "x"), ApiJsonContext.Default.CreateQuestionReportRequest);

        json.ShouldBe("""{"reason":"Typo","comment":"x"}""");
    }

    [Fact]
    public void Serialize_CreateQuizRequest_IncludesBookmarkedOnly()
    {
        var json = JsonSerializer.Serialize(new CreateQuizRequest(Guid.Empty, null, 10, BookmarkedOnly: true), ApiJsonContext.Default.CreateQuizRequest);

        json.ShouldContain("\"bookmarkedOnly\":true");
    }

    [Fact]
    public void Deserialize_QuizResponseJson_MapsIsBookmarked()
    {
        const string json = """
            {"id":"11111111-1111-1111-1111-111111111111","examId":"22222222-2222-2222-2222-222222222222","topicId":null,
             "mode":"Bookmarks","startedAt":"2026-01-01T10:00:00+00:00","finishedAt":null,"correctCount":0,"incorrectCount":0,
             "questions":[{"questionId":"33333333-3333-3333-3333-333333333333","order":1,"statement":"S",
               "options":[{"id":"44444444-4444-4444-4444-444444444444","order":1,"text":"T"}],"feedback":null,"isBookmarked":true}]}
            """;

        var quiz = JsonSerializer.Deserialize(json, ApiJsonContext.Default.QuizResponse);

        quiz.ShouldNotBeNull();
        quiz.Questions[0].IsBookmarked.ShouldBeTrue();
    }
}