using System.Net;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using OpoStudy.Contracts.Exams;
using OpoStudy.Contracts.Quizzes;
using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.Navigation;
using OpoStudy.Mobile.Core.Reminders;
using OpoStudy.Mobile.Core.Services;
using OpoStudy.Mobile.Core.ViewModels;

using Refit;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests;

public sealed class ExamsViewModelTests
{
    private readonly IOpoStudyApi _api = Substitute.For<IOpoStudyApi>();

    private readonly OfflineDoubles _offline = new();

    private readonly INavigationService _navigation = Substitute.For<INavigationService>();

    private ExamsViewModel CreateSut() => new(_api, _navigation, _offline.Content, _offline.State, NullLogger<ExamsViewModel>.Instance);

    [Fact]
    public async Task OnAppearingAsync_ApiReturnsExams_LoadsList()
    {
        _api.GetExamsAsync(Arg.Any<CancellationToken>())
            .Returns([new ExamResponse(Guid.NewGuid(), "c1", "Programador C1", "Diputación de Málaga", "C1")]);
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.Exams.Count.ShouldBe(1);
        sut.Exams[0].Subtitle.ShouldBe("Diputación de Málaga · Grupo C1");
        sut.IsEmpty.ShouldBeFalse();
        sut.HasError.ShouldBeFalse();
    }

    [Fact]
    public async Task OnAppearingAsync_EmptyList_SetsEmptyState()
    {
        _api.GetExamsAsync(Arg.Any<CancellationToken>()).Returns([]);
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public async Task OnAppearingAsync_ApiFails_SetsErrorMessage()
    {
        _api.GetExamsAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException("bad gateway", null, HttpStatusCode.BadGateway));
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.ErrorMessage.ShouldBe(ApiErrorMessages.Unreachable);
        sut.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public async Task OnAppearingAsync_AlreadyLoaded_DoesNotReload()
    {
        _api.GetExamsAsync(Arg.Any<CancellationToken>()).Returns([]);
        var sut = CreateSut();

        await sut.OnAppearingAsync();
        await sut.OnAppearingAsync();

        await _api.Received(1).GetExamsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectExamCommand_Exam_NavigatesToTopicsWithQuery()
    {
        var exam = new ExamResponse(Guid.NewGuid(), "c1", "Programador C1", "Org", "C1");
        var sut = CreateSut();

        await sut.SelectExamCommand.ExecuteAsync(exam);

        await _navigation.Received(1).GoToAsync(
            Routes.Topics,
            Arg.Is<IReadOnlyDictionary<string, object>>(p =>
                (Guid)p[QueryKeys.ExamId] == exam.Id && (string)p[QueryKeys.ExamName] == "Programador C1"));
    }

    [Fact]
    public async Task SelectExamCommand_WhileNavigating_CannotExecute()
    {
        var exam = new ExamResponse(Guid.NewGuid(), "c1", "Programador C1", "Org", "C1");
        var navigating = new TaskCompletionSource();
        _navigation.GoToAsync(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, object>?>()).Returns(navigating.Task);
        var sut = CreateSut();

        var first = sut.SelectExamCommand.ExecuteAsync(exam);
        // MAUI's TapGestureRecognizer and Button only execute a command when CanExecute is true.
        sut.SelectExamCommand.CanExecute(exam).ShouldBeFalse();
        navigating.SetResult();
        await first;

        sut.SelectExamCommand.CanExecute(exam).ShouldBeTrue();
        await _navigation.Received(1).GoToAsync(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, object>?>());
    }
}

public sealed class TopicsViewModelTests
{
    private readonly IOpoStudyApi _api = Substitute.For<IOpoStudyApi>();

    private readonly OfflineDoubles _offline = new();

    private readonly INavigationService _navigation = Substitute.For<INavigationService>();

    private readonly Guid _examId = Guid.NewGuid();

    private TopicsViewModel CreateSut()
    {
        var sut = new TopicsViewModel(_api, _navigation, _offline.Content, _offline.Quizzes, _offline.State, NullLogger<TopicsViewModel>.Instance);
        sut.ApplyQuery(new Dictionary<string, object> { [QueryKeys.ExamId] = _examId, [QueryKeys.ExamName] = "Programador C1" });
        return sut;
    }

    private void GivenTopics(params TopicResponse[] topics) =>
        _api.GetTopicsAsync(_examId, Arg.Any<CancellationToken>()).Returns(topics);

    private static TopicResponse Topic(int number, int questions, string? section = null) =>
        new(Guid.NewGuid(), number, $"Title {number}", section, questions);

    [Fact]
    public async Task OnAppearingAsync_QueryApplied_LoadsTopicsOfExam()
    {
        GivenTopics(Topic(1, 5));
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.ExamName.ShouldBe("Programador C1");
        sut.Topics.Count.ShouldBe(1);
        await _api.Received(1).GetTopicsAsync(_examId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnAppearingAsync_ApiFails_SetsErrorMessage()
    {
        _api.GetTopicsAsync(_examId, Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException("bad gateway", null, HttpStatusCode.BadGateway));
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.ErrorMessage.ShouldBe(ApiErrorMessages.Unreachable);
    }

    [Fact]
    public async Task OpenPracticalCasesCommand_Executed_NavigatesWithExamIdAndName()
    {
        GivenTopics();
        var sut = CreateSut();

        await sut.OpenPracticalCasesCommand.ExecuteAsync(null);

        await _navigation.Received(1).GoToAsync(
            Routes.PracticalCases,
            Arg.Is<IReadOnlyDictionary<string, object>>(q => (Guid)q[QueryKeys.ExamId] == _examId && (string)q[QueryKeys.ExamName] == "Programador C1"));
    }

    [Fact]
    public async Task StartMockExamCommand_Executed_NavigatesWithExamIdAndName()
    {
        GivenTopics();
        var sut = CreateSut();

        await sut.StartMockExamCommand.ExecuteAsync(null);

        await _navigation.Received(1).GoToAsync(
            Routes.MockExams,
            Arg.Is<IReadOnlyDictionary<string, object>>(q => (Guid)q[QueryKeys.ExamId] == _examId && (string)q[QueryKeys.ExamName] == "Programador C1"));
    }

    [Fact]
    public async Task OnAppearingAsync_NoTopics_SetsEmptyState()
    {
        GivenTopics();
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public async Task QuestionCountText_SingularPluralAndNone_UsesRightText()
    {
        GivenTopics(Topic(1, 1), Topic(2, 7), Topic(3, 0));
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.Topics.Select(t => t.QuestionCountText)
            .ShouldBe(["1 pregunta", "7 preguntas", "Sin preguntas todavía"]);
        sut.Topics[0].Title.ShouldBe("Title 1");
        sut.Topics[2].IsAvailable.ShouldBeFalse();
    }

    [Fact]
    public async Task StartTopicQuizCommand_TopicWithoutQuestions_DoesNotCreateQuiz()
    {
        GivenTopics(Topic(1, 0));
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        sut.StartTopicQuizCommand.CanExecute(sut.Topics[0]).ShouldBeFalse();
        await sut.StartTopicQuizCommand.ExecuteAsync(sut.Topics[0]);

        await _api.DidNotReceiveWithAnyArgs().CreateQuizAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task StartRandomQuizCommand_AllTopicsEmpty_CannotExecute()
    {
        GivenTopics(Topic(1, 0), Topic(2, 0));
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.StartRandomQuizCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public async Task StartTopicQuizCommand_TopicWithQuestions_CreatesQuizAndNavigates()
    {
        var topic = Topic(2, 12);
        GivenTopics(topic);
        var quizId = Guid.NewGuid();
        _api.CreateQuizAsync(Arg.Any<CreateQuizRequest>(), Arg.Any<CancellationToken>()).Returns(EmptyQuiz(quizId));
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        sut.SelectedQuestionCount = 20;

        await sut.StartTopicQuizCommand.ExecuteAsync(sut.Topics[0]);

        await _api.Received(1).CreateQuizAsync(new CreateQuizRequest(_examId, topic.Id, 20), Arg.Any<CancellationToken>());
        await _navigation.Received(1).GoToAsync(
            Routes.Quiz,
            Arg.Is<IReadOnlyDictionary<string, object>>(p =>
                (Guid)p[QueryKeys.QuizId] == quizId && (string)p[QueryKeys.QuizTitle] == "Title 2"));
    }

    [Fact]
    public async Task StartRandomQuizCommand_TopicsWithQuestions_CreatesRandomQuizAndNavigates()
    {
        GivenTopics(Topic(1, 3));
        var quizId = Guid.NewGuid();
        _api.CreateQuizAsync(Arg.Any<CreateQuizRequest>(), Arg.Any<CancellationToken>()).Returns(EmptyQuiz(quizId));
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await sut.StartRandomQuizCommand.ExecuteAsync(null);

        await _api.Received(1).CreateQuizAsync(new CreateQuizRequest(_examId, null, 10), Arg.Any<CancellationToken>());
        await _navigation.Received(1).GoToAsync(
            Routes.Quiz,
            Arg.Is<IReadOnlyDictionary<string, object>>(p =>
                (Guid)p[QueryKeys.QuizId] == quizId && (string)p[QueryKeys.QuizTitle] == "Test aleatorio"));
    }

    [Fact]
    public async Task StartRandomQuizCommand_ApiFails_SetsErrorAndDoesNotNavigate()
    {
        GivenTopics(Topic(1, 3));
        _api.CreateQuizAsync(Arg.Any<CreateQuizRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException("bad gateway", null, HttpStatusCode.BadGateway));
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await sut.StartRandomQuizCommand.ExecuteAsync(null);

        sut.ErrorMessage.ShouldBe(ApiErrorMessages.Unreachable);
        await _navigation.DidNotReceiveWithAnyArgs().GoToAsync(default!, default);
    }

    [Fact]
    public async Task OnAppearingAsync_MissingExamId_SetsErrorWithoutCallingApi()
    {
        var sut = new TopicsViewModel(_api, _navigation, _offline.Content, _offline.Quizzes, _offline.State, NullLogger<TopicsViewModel>.Instance);

        await sut.OnAppearingAsync();

        sut.ErrorMessage.ShouldNotBeNullOrEmpty();
        await _api.DidNotReceiveWithAnyArgs().GetTopicsAsync(default, TestContext.Current.CancellationToken);
    }

    private QuizResponse EmptyQuiz(Guid id) =>
        new(id, _examId, null, "Random", DateTimeOffset.UtcNow, null, 0, 0, []);
}

public sealed class QuizViewModelTests
{
    private readonly IOpoStudyApi _api = Substitute.For<IOpoStudyApi>();

    private readonly OfflineDoubles _offline = new();

    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();

    private readonly INavigationService _navigation = Substitute.For<INavigationService>();

    private readonly Guid _quizId = Guid.NewGuid();

    private readonly FakeReminderStore _reminderStore = new();

    private QuizViewModel CreateSut()
    {
        var sut = new QuizViewModel(_api, _navigation, _dialogs, ReminderTestSupport.CreateService(_reminderStore), _offline.Quizzes, _offline.Synchronizer, _offline.State, NullLogger<QuizViewModel>.Instance);
        sut.ApplyQuery(new Dictionary<string, object> { [QueryKeys.QuizId] = _quizId, [QueryKeys.QuizTitle] = "Test aleatorio" });
        return sut;
    }

    private static QuizQuestionResponse Question(int order, AnswerFeedback? feedback = null, params Guid[] optionIds)
    {
        var ids = optionIds.Length == 3 ? optionIds : [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        return new QuizQuestionResponse(
            Guid.NewGuid(),
            order,
            $"Statement {order}",
            [.. ids.Select((id, i) => new QuizOptionResponse(id, i + 1, $"Option {i + 1}"))],
            feedback,
            false);
    }

    private static AnswerFeedback Feedback(Guid selected, Guid correct) =>
        new(selected, selected == correct, correct, "Explanation", "Art. 1 CE");

    private QuizResponse Quiz(int correct, int incorrect, params QuizQuestionResponse[] questions) =>
        new(_quizId, Guid.NewGuid(), null, "Random", DateTimeOffset.UtcNow, null, correct, incorrect, questions);

    private void GivenQuiz(QuizResponse quiz) => _api.GetQuizAsync(_quizId, Arg.Any<CancellationToken>()).Returns(quiz);

    private void GivenAnswer(QuizQuestionResponse question, Guid selected, Guid correct, bool finished, int ok, int ko, int total) =>
        _api.SubmitAnswerAsync(_quizId, Arg.Is<SubmitAnswerRequest>(r => r.QuestionId == question.QuestionId), Arg.Any<CancellationToken>())
            .Returns(new AnswerFeedbackResponse(question.QuestionId, Feedback(selected, correct), finished, ok, ko, ok + ko, total, new OpoStudy.Contracts.Progress.DailyProgressResponse(1, 10, false, 0), new OpoStudy.Contracts.Progress.XpGainResponse(10, 0, 10, 1, false)));

    [Fact]
    public async Task AnswerCommand_Answered_SavesReminderSnapshot()
    {
        var question = Question(1);
        var correct = question.Options[0].Id;
        GivenQuiz(Quiz(0, 0, question));
        GivenAnswer(question, correct, correct, finished: true, ok: 1, ko: 0, total: 1);
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await sut.AnswerCommand.ExecuteAsync(sut.Options[0]);

        var snapshot = _reminderStore.Snapshot.ShouldNotBeNull();
        snapshot.AnsweredToday.ShouldBe(1);
        snapshot.DailyGoal.ShouldBe(10);
        snapshot.GoalMetToday.ShouldBeFalse();
    }

    [Fact]
    public async Task AnswerCommand_GoalMetNow_ShowsNoticeWithStreak()
    {
        var question = Question(1);
        var correct = question.Options[0].Id;
        GivenQuiz(Quiz(0, 0, question));
        _api.SubmitAnswerAsync(_quizId, Arg.Any<SubmitAnswerRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AnswerFeedbackResponse(question.QuestionId, Feedback(correct, correct), true, 1, 0, 1, 1, new OpoStudy.Contracts.Progress.DailyProgressResponse(10, 10, true, 3), new OpoStudy.Contracts.Progress.XpGainResponse(65, 55, 65, 1, false)));
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await sut.AnswerCommand.ExecuteAsync(sut.Options[0]);

        sut.HasGoalNotice.ShouldBeTrue();
        sut.GoalNotice.ShouldBe("🎯 ¡Objetivo diario cumplido! Racha: 3 días · +55 XP");
    }

    [Fact]
    public async Task AnswerCommand_TwoAnswers_ShowsXpPerAnswerAndTotalInSummary()
    {
        var q1 = Question(1);
        var q2 = Question(2);
        GivenQuiz(Quiz(0, 0, q1, q2));
        GivenAnswerWithXp(q1, q1.Options[0].Id, q1.Options[0].Id, false, 1, 0, 2, new OpoStudy.Contracts.Progress.XpGainResponse(10, 0, 10, 1, false));
        GivenAnswerWithXp(q2, q2.Options[0].Id, q2.Options[1].Id, true, 1, 1, 2, new OpoStudy.Contracts.Progress.XpGainResponse(2, 0, 12, 1, false));
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await sut.AnswerCommand.ExecuteAsync(sut.Options[0]);
        var firstLabel = sut.XpText;
        sut.NextCommand.Execute(null);
        sut.XpText.ShouldBeEmpty();
        await sut.AnswerCommand.ExecuteAsync(sut.Options[0]);
        var secondLabel = sut.XpText;
        sut.NextCommand.Execute(null);

        firstLabel.ShouldBe("+10 XP");
        secondLabel.ShouldBe("+2 XP");
        sut.QuizXpText.ShouldBe("+12 XP en este test");
    }

    [Fact]
    public async Task NextCommand_ResumedQuizWithoutNewAnswers_HidesQuizXp()
    {
        var q = Question(1);
        var answered = q with { Feedback = Feedback(q.Options[0].Id, q.Options[0].Id) };
        GivenQuiz(Quiz(1, 0, answered));
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        sut.NextCommand.Execute(null);

        sut.IsFinished.ShouldBeTrue();
        sut.QuizXpText.ShouldBeEmpty();
        sut.HasQuizXpText.ShouldBeFalse();
    }

    [Fact]
    public async Task AnswerCommand_LevelUpAndGoalMet_ShowsSingleCombinedNotice()
    {
        var question = Question(1);
        var correct = question.Options[0].Id;
        GivenQuiz(Quiz(0, 0, question));
        _api.SubmitAnswerAsync(_quizId, Arg.Any<SubmitAnswerRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AnswerFeedbackResponse(question.QuestionId, Feedback(correct, correct), true, 1, 0, 1, 1, new OpoStudy.Contracts.Progress.DailyProgressResponse(10, 10, true, 1), new OpoStudy.Contracts.Progress.XpGainResponse(65, 55, 120, 2, true)));
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await sut.AnswerCommand.ExecuteAsync(sut.Options[0]);

        sut.GoalNotice.ShouldBe("🎯 ¡Objetivo diario cumplido! Racha: 1 día · +55 XP\n⭐ ¡Has subido al nivel 2!");
    }

    private void GivenAnswerWithXp(QuizQuestionResponse question, Guid selected, Guid correct, bool finished, int ok, int ko, int total, OpoStudy.Contracts.Progress.XpGainResponse xp) =>
        _api.SubmitAnswerAsync(_quizId, Arg.Is<SubmitAnswerRequest>(r => r.QuestionId == question.QuestionId), Arg.Any<CancellationToken>())
            .Returns(new AnswerFeedbackResponse(question.QuestionId, Feedback(selected, correct), finished, ok, ko, ok + ko, total, new OpoStudy.Contracts.Progress.DailyProgressResponse(1, 10, false, 0), xp));

    [Fact]
    public async Task AnswerCommand_GoalNotMetNow_ShowsNoNotice()
    {
        var question = Question(1);
        var correct = question.Options[0].Id;
        GivenQuiz(Quiz(0, 0, question));
        GivenAnswer(question, correct, correct, true, 1, 0, 1);
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await sut.AnswerCommand.ExecuteAsync(sut.Options[0]);

        sut.HasGoalNotice.ShouldBeFalse();
    }

    [Fact]
    public async Task OnAppearingAsync_NoAnswers_ShowsFirstQuestion()
    {
        GivenQuiz(Quiz(0, 0, Question(1), Question(2)));
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.IsLoaded.ShouldBeTrue();
        sut.ProgressText.ShouldBe("Pregunta 1 de 2");
        sut.CurrentStatement.ShouldBe("Statement 1");
        sut.Options.Select(o => o.Letter).ShouldBe(["A", "B", "C"]);
        sut.ScoreText.ShouldBe("0 aciertos · 0 fallos");
        sut.IsAnswered.ShouldBeFalse();
    }

    [Fact]
    public async Task OnAppearingAsync_FirstQuestionAnswered_StartsAtFirstPending()
    {
        var first = Question(1);
        var answered = first with { Feedback = Feedback(first.Options[0].Id, first.Options[0].Id) };
        GivenQuiz(Quiz(1, 0, answered, Question(2)));
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.ProgressText.ShouldBe("Pregunta 2 de 2");
        sut.ScoreText.ShouldBe("1 acierto · 0 fallos");
        sut.IsFinished.ShouldBeFalse();
    }

    [Fact]
    public async Task OnAppearingAsync_AllAnswered_ShowsSummary()
    {
        var q = Question(1);
        var answered = q with { Feedback = Feedback(q.Options[0].Id, q.Options[0].Id) };
        GivenQuiz(Quiz(1, 0, answered));
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.IsFinished.ShouldBeTrue();
        sut.ResultText.ShouldBe("1 de 1 correctas");
    }

    [Fact]
    public async Task OnAppearingAsync_ApiFails_SetsErrorAndIsNotLoaded()
    {
        _api.GetQuizAsync(_quizId, Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException());
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.ErrorMessage.ShouldBe(ApiErrorMessages.Unreachable);
        sut.IsLoaded.ShouldBeFalse();
        sut.ShowLoadError.ShouldBeTrue();
    }

    [Fact]
    public async Task AnswerCommand_CorrectOption_MarksCorrectAndDimsRest()
    {
        var q = Question(1);
        GivenQuiz(Quiz(0, 0, q, Question(2)));
        GivenAnswer(q, q.Options[1].Id, q.Options[1].Id, false, 1, 0, 2);
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await sut.AnswerCommand.ExecuteAsync(sut.Options[1]);

        sut.Options.Select(o => o.State).ShouldBe([OptionState.Dimmed, OptionState.Correct, OptionState.Dimmed]);
        sut.Options[1].StateText.ShouldBe("Correcta");
        sut.IsCorrect.ShouldBeTrue();
        sut.FeedbackTitle.ShouldBe("¡Correcto!");
        sut.Explanation.ShouldBe("Explanation");
        sut.LegalReferenceText.ShouldBe("Fuente: Art. 1 CE");
        sut.ScoreText.ShouldBe("1 acierto · 0 fallos");
        sut.NextButtonText.ShouldBe("Siguiente pregunta");
    }

    [Fact]
    public async Task AnswerCommand_WrongOption_MarksIncorrectAndShowsCorrect()
    {
        var q = Question(1);
        GivenQuiz(Quiz(0, 0, q, Question(2)));
        GivenAnswer(q, q.Options[0].Id, q.Options[2].Id, false, 0, 1, 2);
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await sut.AnswerCommand.ExecuteAsync(sut.Options[0]);

        sut.Options.Select(o => o.State).ShouldBe([OptionState.Incorrect, OptionState.Dimmed, OptionState.Correct]);
        sut.Options[0].StateText.ShouldBe("Tu respuesta: incorrecta");
        sut.FeedbackTitle.ShouldBe("Incorrecto");
        sut.ScoreText.ShouldBe("0 aciertos · 1 fallo");
    }

    [Fact]
    public async Task AnswerCommand_AlreadyAnswered_CannotExecute()
    {
        var q = Question(1);
        GivenQuiz(Quiz(0, 0, q));
        GivenAnswer(q, q.Options[0].Id, q.Options[0].Id, true, 1, 0, 1);
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await sut.AnswerCommand.ExecuteAsync(sut.Options[0]);

        sut.AnswerCommand.CanExecute(sut.Options[1]).ShouldBeFalse();
    }

    [Fact]
    public async Task AnswerCommand_LastQuestion_ShowsViewResultButton()
    {
        var q = Question(1);
        GivenQuiz(Quiz(0, 0, q));
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.NextButtonText.ShouldBe("Ver resultado");
    }

    [Fact]
    public async Task NextCommand_MoreQuestions_ShowsNextQuestion()
    {
        var q = Question(1);
        GivenQuiz(Quiz(0, 0, q, Question(2)));
        GivenAnswer(q, q.Options[0].Id, q.Options[0].Id, false, 1, 0, 2);
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        await sut.AnswerCommand.ExecuteAsync(sut.Options[0]);

        sut.NextCommand.Execute(null);

        sut.ProgressText.ShouldBe("Pregunta 2 de 2");
        sut.IsAnswered.ShouldBeFalse();
        sut.Options.ShouldAllBe(o => o.State == OptionState.Neutral);
    }

    [Fact]
    public async Task NextCommand_LastQuestion_ShowsSummaryWithPercentage()
    {
        var q1 = Question(1);
        var q2 = Question(2);
        var q3 = Question(3);
        GivenQuiz(Quiz(0, 0, q1, q2, q3));
        GivenAnswer(q1, q1.Options[0].Id, q1.Options[0].Id, false, 1, 0, 3);
        GivenAnswer(q2, q2.Options[0].Id, q2.Options[0].Id, false, 2, 0, 3);
        GivenAnswer(q3, q3.Options[1].Id, q3.Options[0].Id, true, 2, 1, 3);
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        for (var i = 0; i < 3; i++)
        {
            await sut.AnswerCommand.ExecuteAsync(sut.Options[i == 2 ? 1 : 0]);
            sut.NextCommand.Execute(null);
        }

        sut.IsFinished.ShouldBeTrue();
        sut.ResultText.ShouldBe("2 de 3 correctas");
        sut.PercentageText.ShouldBe("67 %");
        sut.SummaryItems.Select(s => s.ResultText).ShouldBe(["Acertada", "Acertada", "Fallada"]);
        sut.SummaryItems[2].ShowDetails.ShouldBeTrue();
        sut.SummaryItems[2].ReferenceText.ShouldBe("Fuente: Art. 1 CE");
        sut.SummaryItems[0].ShowDetails.ShouldBeFalse();
    }

    [Fact]
    public async Task AnswerCommand_Conflict_ReloadsQuizAndShowsFeedback()
    {
        var q = Question(1);
        var answered = q with { Feedback = Feedback(q.Options[0].Id, q.Options[0].Id) };
        _api.GetQuizAsync(_quizId, Arg.Any<CancellationToken>()).Returns(Quiz(0, 0, q, Question(2)), Quiz(1, 0, answered, Question(2)));
        _api.SubmitAnswerAsync(_quizId, Arg.Any<SubmitAnswerRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(await CreateApiExceptionAsync(HttpStatusCode.Conflict));
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await sut.AnswerCommand.ExecuteAsync(sut.Options[1]);

        await _api.Received(2).GetQuizAsync(_quizId, Arg.Any<CancellationToken>());
        sut.IsAnswered.ShouldBeTrue();
        sut.Options[0].State.ShouldBe(OptionState.Correct);
        sut.ScoreText.ShouldBe("1 acierto · 0 fallos");
    }

    [Fact]
    public async Task AnswerCommand_NetworkError_ShowsErrorAndAllowsRetry()
    {
        GivenQuiz(Quiz(0, 0, Question(1)));
        _api.SubmitAnswerAsync(_quizId, Arg.Any<SubmitAnswerRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("bad gateway", null, HttpStatusCode.BadGateway));
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await sut.AnswerCommand.ExecuteAsync(sut.Options[0]);

        sut.ErrorMessage.ShouldBe(ApiErrorMessages.Unreachable);
        sut.IsAnswered.ShouldBeFalse();
        sut.AnswerCommand.CanExecute(sut.Options[0]).ShouldBeTrue();
    }

    [Fact]
    public async Task AnotherQuizCommand_Executed_NavigatesBack()
    {
        var sut = CreateSut();

        await sut.AnotherQuizCommand.ExecuteAsync(null);

        await _navigation.Received(1).GoToAsync(Routes.Back);
    }

    [Fact]
    public async Task GoHomeCommand_Executed_NavigatesToHomeRoot()
    {
        var sut = CreateSut();

        await sut.GoHomeCommand.ExecuteAsync(null);

        await _navigation.Received(1).GoToAsync(Routes.Root(Routes.Home));
    }

    [Fact]
    public async Task GoHomeCommand_WhileAnotherQuizNavigates_CannotExecute()
    {
        var navigating = new TaskCompletionSource();
        _navigation.GoToAsync(Routes.Back).Returns(navigating.Task);
        var sut = CreateSut();

        var first = sut.AnotherQuizCommand.ExecuteAsync(null);
        sut.GoHomeCommand.CanExecute(null).ShouldBeFalse();
        sut.AnotherQuizCommand.CanExecute(null).ShouldBeFalse();
        navigating.SetResult();
        await first;

        sut.GoHomeCommand.CanExecute(null).ShouldBeTrue();
    }

    [Fact]
    public async Task OnAppearingAsync_MissingQuizId_SetsErrorWithoutCallingApi()
    {
        var sut = new QuizViewModel(_api, _navigation, _dialogs, ReminderTestSupport.CreateService(), _offline.Quizzes, _offline.Synchronizer, _offline.State, NullLogger<QuizViewModel>.Instance);

        await sut.OnAppearingAsync();

        sut.ErrorMessage.ShouldNotBeNullOrEmpty();
        sut.ShowLoadError.ShouldBeTrue();
        await _api.DidNotReceiveWithAnyArgs().GetQuizAsync(default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task OnAppearingAsync_WhileLoading_IsLoadingUntilLoaded()
    {
        var loading = new TaskCompletionSource<QuizResponse>();
        _api.GetQuizAsync(_quizId, Arg.Any<CancellationToken>()).Returns(loading.Task);
        var sut = CreateSut();

        var appearing = sut.OnAppearingAsync();
        sut.IsLoading.ShouldBeTrue();
        loading.SetResult(Quiz(0, 0, Question(1)));
        await appearing;

        sut.IsLoading.ShouldBeFalse();
    }

    [Fact]
    public async Task AnswerCommand_WhileSending_IsNotLoading()
    {
        var question = Question(1);
        GivenQuiz(Quiz(0, 0, question));
        var sending = new TaskCompletionSource<AnswerFeedbackResponse>();
        _api.SubmitAnswerAsync(_quizId, Arg.Any<SubmitAnswerRequest>(), Arg.Any<CancellationToken>()).Returns(sending.Task);
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        var answering = sut.AnswerCommand.ExecuteAsync(sut.Options[0]);
        sut.IsBusy.ShouldBeTrue();
        sut.IsLoading.ShouldBeFalse();
        var correct = question.Options[0].Id;
        sending.SetResult(new AnswerFeedbackResponse(question.QuestionId, Feedback(correct, correct), true, 1, 0, 1, 1, new OpoStudy.Contracts.Progress.DailyProgressResponse(1, 10, false, 0), new OpoStudy.Contracts.Progress.XpGainResponse(10, 0, 10, 1, false)));
        await answering;

        sut.IsAnswered.ShouldBeTrue();
    }

    private static async Task<ApiException> CreateApiExceptionAsync(HttpStatusCode status)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/api/v1/quizzes/x/answers");
        using var response = new HttpResponseMessage(status);
        return await ApiException.Create(request, HttpMethod.Post, response, new RefitSettings());
    }
}

public sealed class QuizSerializationTests
{
    [Fact]
    public void Deserialize_QuizResponseJson_MapsNestedTypes()
    {
        const string json = """
            {"id":"11111111-1111-1111-1111-111111111111","examId":"22222222-2222-2222-2222-222222222222","topicId":null,
             "mode":"Random","startedAt":"2026-01-01T10:00:00+00:00","finishedAt":null,"correctCount":0,"incorrectCount":0,
             "questions":[{"questionId":"33333333-3333-3333-3333-333333333333","order":1,"statement":"S",
               "options":[{"id":"44444444-4444-4444-4444-444444444444","order":1,"text":"T"}],"feedback":null}]}
            """;

        var quiz = JsonSerializer.Deserialize(json, ApiJsonContext.Default.QuizResponse);

        quiz.ShouldNotBeNull();
        quiz.Questions.Count.ShouldBe(1);
        quiz.Questions[0].Options[0].Text.ShouldBe("T");
        quiz.Questions[0].Feedback.ShouldBeNull();
    }

    [Fact]
    public void Deserialize_AnswerFeedbackResponseJson_MapsFeedback()
    {
        const string json = """
            {"questionId":"33333333-3333-3333-3333-333333333333",
             "feedback":{"selectedOptionId":"44444444-4444-4444-4444-444444444444","isCorrect":true,
               "correctOptionId":"44444444-4444-4444-4444-444444444444","explanation":"E","legalReference":"R"},
             "quizFinished":true,"correctCount":1,"incorrectCount":0,"answeredCount":1,"questionCount":1}
            """;

        var response = JsonSerializer.Deserialize(json, ApiJsonContext.Default.AnswerFeedbackResponse);

        response.ShouldNotBeNull();
        response.Feedback.IsCorrect.ShouldBeTrue();
        response.QuizFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task GetExamsAsync_ValidResponse_CallsExamsRouteAndDeserializes()
    {
        using var handler = new StubHandler("""[{"id":"22222222-2222-2222-2222-222222222222","slug":"c1","name":"N","organization":"O","group":"C1"}]""");
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5054") };
        var api = RestService.ForGenerated<IOpoStudyApi>(client, ApiJsonContext.Default);

        var exams = await api.GetExamsAsync(TestContext.Current.CancellationToken);

        exams.Count.ShouldBe(1);
        handler.LastRequestUri.ShouldBe(new Uri("http://localhost:5054/api/v1/exams"));
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