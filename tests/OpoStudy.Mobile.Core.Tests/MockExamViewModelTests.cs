using System.Net;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using OpoStudy.Contracts.MockExams;
using OpoStudy.Contracts.Quizzes;
using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.Navigation;
using OpoStudy.Mobile.Core.Services;
using OpoStudy.Mobile.Core.ViewModels;

using Refit;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests;

public sealed class MockExamViewModelTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 26, 10, 0, 0, TimeSpan.Zero);

    private readonly IOpoStudyApi _api = Substitute.For<IOpoStudyApi>();

    private readonly INavigationService _navigation = Substitute.For<INavigationService>();

    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();

    private readonly IScreenReaderAnnouncer _announcer = Substitute.For<IScreenReaderAnnouncer>();

    private readonly FakeTimeProvider _time = new(Start);

    private readonly Guid _examId = Guid.NewGuid();

    private readonly Guid _mockExamId = Guid.NewGuid();

    private MockExamResponse _exam = null!;

    private MockExamViewModel CreateSut(int questionCount = 4, int durationMinutes = 50)
    {
        _exam = BuildExam(questionCount, durationMinutes);
        _api.CreateMockExamAsync(Arg.Any<CreateMockExamRequest>(), Arg.Any<CancellationToken>()).Returns(_exam);
        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _api.SubmitMockExamAsync(_mockExamId, Arg.Any<SubmitMockExamRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => new SubmitMockExamResponse(BuildResult(_exam, call.Arg<SubmitMockExamRequest>()), 12));
        var sut = new MockExamViewModel(_api, _navigation, _dialogs, _announcer, _time, NullLogger<MockExamViewModel>.Instance);
        sut.ApplyQuery(new Dictionary<string, object>
        {
            [QueryKeys.ExamId] = _examId,
            [QueryKeys.ExerciseNumber] = 1,
            [QueryKeys.PassScore] = 10m,
        });
        return sut;
    }

    private MockExamResponse BuildExam(int questionCount, int durationMinutes)
    {
        var questions = Enumerable.Range(0, questionCount)
            .Select(i => new MockExamQuestionResponse(
                Guid.NewGuid(),
                i,
                $"Enunciado {i + 1}",
                [new QuizOptionResponse(Guid.NewGuid(), 0, "Uno"), new QuizOptionResponse(Guid.NewGuid(), 1, "Dos"), new QuizOptionResponse(Guid.NewGuid(), 2, "Tres")]))
            .ToList();
        return new MockExamResponse(_mockExamId, _examId, 1, "Primer ejercicio", Start, Start.AddMinutes(durationMinutes), durationMinutes, 20m, 0.5m, 10m, questions);
    }

    /// <summary>The first option is the correct one of every question.</summary>
    private static MockExamResultResponse BuildResult(MockExamResponse exam, SubmitMockExamRequest request, decimal score = 12.73m, bool late = false, double usedMinutes = 38)
    {
        var selected = request.Answers.ToDictionary(a => a.QuestionId, a => a.OptionId);
        var items = exam.Questions.Select(q =>
        {
            var chosen = selected.TryGetValue(q.QuestionId, out var id) ? id : (Guid?)null;
            var outcome = chosen is null ? "Blank" : chosen == q.Options[0].Id ? "Correct" : "Incorrect";
            return new MockExamResultItem(q.QuestionId, q.Order, q.Statement, q.Options, chosen, q.Options[0].Id, outcome, "Porque sí.", "CE art. 1");
        }).ToList();
        return new MockExamResultResponse(
            exam.Id,
            1,
            exam.Title,
            Start,
            Start.AddMinutes(usedMinutes),
            late,
            exam.Questions.Count,
            items.Count(i => i.Outcome == "Correct"),
            items.Count(i => i.Outcome == "Incorrect"),
            items.Count(i => i.Outcome == "Blank"),
            0.5m,
            score,
            20m,
            10m,
            score >= 10m,
            items);
    }

    [Fact]
    public async Task OnAppearingAsync_CreatesTheMockExamOnlyOnce()
    {
        var sut = CreateSut();

        await sut.OnAppearingAsync();
        await sut.OnAppearingAsync();

        await _api.Received(1).CreateMockExamAsync(
            Arg.Is<CreateMockExamRequest>(r => r.ExamId == _examId && r.ExerciseNumber == 1 && r.PassScore == 10m),
            Arg.Any<CancellationToken>());
        sut.IsRunning.ShouldBeTrue();
        sut.IsLoading.ShouldBeFalse();
        sut.Questions.Count.ShouldBe(4);
        sut.Title.ShouldBe("Primer ejercicio");
        sut.RemainingText.ShouldBe("50:00");
        sut.ProgressText.ShouldBe("Pregunta 1 de 4 · 0 respondidas");
    }

    [Fact]
    public async Task OnAppearingAsync_CreationFails_ShowsErrorAndDoesNotRun()
    {
        var sut = CreateSut();
        _api.CreateMockExamAsync(Arg.Any<CreateMockExamRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("boom"));

        await sut.OnAppearingAsync();

        sut.LoadFailed.ShouldBeTrue();
        sut.ShowLoadError.ShouldBeTrue();
        sut.HasError.ShouldBeTrue();
        sut.IsRunning.ShouldBeFalse();
    }

    [Fact]
    public async Task SelectOption_ChangeAndClear_UpdatesAnsweredCount()
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        var question = sut.CurrentQuestion!;

        sut.SelectOptionCommand.Execute(question.Options[0]);
        sut.ProgressText.ShouldBe("Pregunta 1 de 4 · 1 respondida");
        sut.SelectOptionCommand.Execute(question.Options[1]);
        question.SelectedOptionId.ShouldBe(question.Options[1].Id);
        question.Options[0].IsSelected.ShouldBeFalse();
        question.Options[1].IsSelected.ShouldBeTrue();
        sut.ProgressText.ShouldBe("Pregunta 1 de 4 · 1 respondida");

        sut.ClearAnswerCommand.Execute(null);

        question.IsAnswered.ShouldBeFalse();
        question.StatusText.ShouldBe("en blanco");
        question.Options.ShouldAllBe(o => !o.IsSelected);
        sut.ProgressText.ShouldBe("Pregunta 1 de 4 · 0 respondidas");
    }

    [Fact]
    public async Task Navigation_PreviousNextAndIndex_MovesBetweenQuestions()
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        sut.PreviousCommand.CanExecute(null).ShouldBeFalse();
        sut.NextCommand.Execute(null);
        sut.CurrentIndex.ShouldBe(1);
        sut.GoToQuestionCommand.Execute(sut.Questions[3]);
        sut.CurrentIndex.ShouldBe(3);
        sut.NextCommand.CanExecute(null).ShouldBeFalse();
        sut.ProgressText.ShouldStartWith("Pregunta 4 de 4");
        sut.Questions[3].IsCurrent.ShouldBeTrue();
        sut.Questions[0].IsCurrent.ShouldBeFalse();
        sut.Questions[3].IndexDescription.ShouldBe("Pregunta 4, en blanco");
    }

    [Fact]
    public async Task Submit_SendsOnlyAnsweredQuestionsAndShowsResult()
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        sut.SelectOptionCommand.Execute(sut.Questions[0].Options[0]);
        sut.GoToQuestionCommand.Execute(sut.Questions[2]);
        sut.SelectOptionCommand.Execute(sut.Questions[2].Options[1]);

        await sut.SubmitCommand.ExecuteAsync(null);

        await _dialogs.Received(1).ConfirmAsync(Arg.Any<string>(), "¿Entregar el simulacro? Tienes 2 preguntas en blanco.", Arg.Any<string>(), Arg.Any<string>());
        await _api.Received(1).SubmitMockExamAsync(
            _mockExamId,
            Arg.Is<SubmitMockExamRequest>(r => r.Answers.Count == 2
                && r.Answers.Any(a => a.QuestionId == sut.Questions[0].Id)
                && r.Answers.Any(a => a.QuestionId == sut.Questions[2].Id)),
            Arg.Any<CancellationToken>());
        sut.IsFinished.ShouldBeTrue();
        sut.ShowExam.ShouldBeFalse();
        sut.IsRunning.ShouldBeFalse();
        sut.ScoreText.ShouldBe("12,73 / 20");
        sut.PassedText.ShouldBe("Apto");
        sut.PassScoreText.ShouldBe("Nota para aprobar: 10");
        sut.BreakdownText.ShouldBe("1 acierto · 1 fallo · 2 en blanco");
        sut.NetText.ShouldBe("Netas: 1 − 1 × 0,5 = 0,5");
        sut.TimeText.ShouldBe("Tiempo: 38:00 de 50:00");
        sut.XpText.ShouldBe("+12 XP");
        sut.ResultLines.Select(l => l.OutcomeText).ShouldBe(["Acertada", "En blanco", "Fallada", "En blanco"]);
        sut.ResultLines[2].YourAnswerText.ShouldBe("Tu respuesta: B. Dos");
        sut.ResultLines[2].CorrectAnswerText.ShouldBe("Respuesta correcta: A. Uno");
        sut.ResultLines[0].ShowDetails.ShouldBeFalse();
        sut.ResultLines[1].ShowDetails.ShouldBeTrue();
        sut.ResultLines[1].ReferenceText.ShouldBe("Fuente: CE art. 1");
    }

    [Fact]
    public async Task Submit_WhenUserDeclines_DoesNotCallApi()
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        await sut.SubmitCommand.ExecuteAsync(null);

        await _api.DidNotReceive().SubmitMockExamAsync(Arg.Any<Guid>(), Arg.Any<SubmitMockExamRequest>(), Arg.Any<CancellationToken>());
        sut.IsRunning.ShouldBeTrue();
    }

    [Fact]
    public async Task Submit_Fails_KeepsAnswersAndRetrySucceeds()
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        sut.SelectOptionCommand.Execute(sut.Questions[0].Options[0]);
        _api.SubmitMockExamAsync(_mockExamId, Arg.Any<SubmitMockExamRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                _ => Task.FromException<SubmitMockExamResponse>(new HttpRequestException()),
                call => Task.FromResult(new SubmitMockExamResponse(BuildResult(_exam, call.Arg<SubmitMockExamRequest>()), 5)));

        await sut.SubmitCommand.ExecuteAsync(null);

        sut.SubmitFailed.ShouldBeTrue();
        sut.ShowSubmitError.ShouldBeTrue();
        sut.HasError.ShouldBeTrue();
        sut.IsRunning.ShouldBeTrue();
        sut.Questions[0].IsAnswered.ShouldBeTrue();
        sut.RetrySubmitCommand.CanExecute(null).ShouldBeTrue();

        await sut.RetrySubmitCommand.ExecuteAsync(null);

        sut.IsFinished.ShouldBeTrue();
        sut.HasError.ShouldBeFalse();
    }

    [Fact]
    public async Task Submit_AlreadySubmitted409_LoadsTheResult()
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/api/v1/mock-exams/x/submission");
        using var response = new HttpResponseMessage(HttpStatusCode.Conflict);
        var conflict = await ApiException.Create(request, HttpMethod.Post, response, new RefitSettings());
        _api.SubmitMockExamAsync(_mockExamId, Arg.Any<SubmitMockExamRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(conflict);
        _api.GetMockExamResultAsync(_mockExamId, Arg.Any<CancellationToken>())
            .Returns(BuildResult(_exam, new SubmitMockExamRequest([]), score: 3m));

        await sut.SubmitCommand.ExecuteAsync(null);

        sut.IsFinished.ShouldBeTrue();
        sut.ScoreText.ShouldBe("3 / 20");
        sut.PassedText.ShouldBe("No apto");
        sut.HasXpText.ShouldBeFalse();
    }

    [Fact]
    public async Task Timer_ShowsRemainingTimeAndDescription()
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        _time.Advance(TimeSpan.FromMinutes(26) + TimeSpan.FromSeconds(30));

        sut.RemainingText.ShouldBe("23:30");
        sut.RemainingDescription.ShouldBe("Quedan 23 minutos y 30 segundos");
        sut.IsTimeLow.ShouldBeFalse();
    }

    [Fact]
    public async Task Timer_StartedWithSynchronizationContext_PostsTicksToThatContext()
    {
        var sut = CreateSut();
        var context = new RecordingSynchronizationContext();
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            await sut.OnAppearingAsync();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        var before = sut.RemainingText;
        _time.Advance(TimeSpan.FromMinutes(1));

        sut.RemainingText.ShouldBe(before);
        context.RunPending();
        sut.RemainingText.ShouldBe("49:00");
    }

    private sealed class RecordingSynchronizationContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> _pending = new();

        public override void Post(SendOrPostCallback d, object? state) => _pending.Enqueue((d, state));

        public void RunPending()
        {
            while (_pending.TryDequeue(out var item))
            {
                item.Callback(item.State);
            }
        }
    }

    [Fact]
    public async Task Timer_AnnouncesTenFiveAndOneMinuteOnce()
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        _time.Advance(TimeSpan.FromMinutes(40));
        _announcer.Received(1).Announce("Quedan 10 minutos");
        _time.Advance(TimeSpan.FromMinutes(5));
        _announcer.Received(1).Announce("Quedan 5 minutos");
        _time.Advance(TimeSpan.FromMinutes(4));
        _announcer.Received(1).Announce("Queda 1 minuto");
        _time.Advance(TimeSpan.FromSeconds(10));

        _announcer.ReceivedCalls().Count().ShouldBe(3);
        sut.RemainingDescription.ShouldBe("Queda menos de un minuto");
        sut.IsRunning.ShouldBeTrue();
    }

    [Fact]
    public async Task Timer_FiveMinutesLeft_SetsTimeLowWithText()
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        _time.Advance(TimeSpan.FromMinutes(44) + TimeSpan.FromSeconds(59));
        sut.IsTimeLow.ShouldBeFalse();
        sut.TimeLowText.ShouldBeEmpty();
        _time.Advance(TimeSpan.FromSeconds(1));

        sut.IsTimeLow.ShouldBeTrue();
        sut.TimeLowText.ShouldBe("Quedan menos de 5 minutos");
    }

    [Fact]
    public async Task Timer_TimeUp_LocksAnswersAndSubmitsWithoutConfirmation()
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        var question = sut.Questions[0];
        sut.SelectOptionCommand.Execute(question.Options[0]);

        _time.Advance(TimeSpan.FromMinutes(50));

        sut.IsLocked.ShouldBeTrue();
        sut.IsFinished.ShouldBeTrue();
        sut.RemainingText.ShouldBe("00:00");
        await _dialogs.DidNotReceive().ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        await _api.Received(1).SubmitMockExamAsync(
            _mockExamId,
            Arg.Is<SubmitMockExamRequest>(r => r.Answers.Count == 1 && r.Answers[0].QuestionId == question.Id),
            Arg.Any<CancellationToken>());
        _announcer.Received(1).Announce("Tiempo agotado. Entregando el simulacro.");
    }

    [Fact]
    public async Task Timer_TimeUpAndSubmitFails_KeepsAnswersLockedAndAllowsRetry()
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        sut.SelectOptionCommand.Execute(sut.Questions[0].Options[0]);
        _api.SubmitMockExamAsync(_mockExamId, Arg.Any<SubmitMockExamRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                _ => Task.FromException<SubmitMockExamResponse>(new HttpRequestException()),
                call => Task.FromResult(new SubmitMockExamResponse(BuildResult(_exam, call.Arg<SubmitMockExamRequest>()), 0)));

        _time.Advance(TimeSpan.FromMinutes(50));

        sut.IsFinished.ShouldBeFalse();
        sut.IsLocked.ShouldBeTrue();
        sut.CanAnswer.ShouldBeFalse();
        sut.SelectOptionCommand.CanExecute(sut.Questions[1].Options[0]).ShouldBeFalse();
        sut.Questions[0].IsAnswered.ShouldBeTrue();
        sut.RetrySubmitCommand.CanExecute(null).ShouldBeTrue();

        await sut.RetrySubmitCommand.ExecuteAsync(null);

        sut.IsFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task Exit_WhileRunning_AsksForConfirmation()
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        await sut.ExitCommand.ExecuteAsync(null);
        await _navigation.DidNotReceive().GoToAsync(Routes.Back, Arg.Any<IReadOnlyDictionary<string, object>?>());

        _dialogs.ConfirmAsync(Arg.Any<string>(), "No se guardará y no contará.", "Salir", "Seguir").Returns(true);
        await sut.ExitCommand.ExecuteAsync(null);

        await _navigation.Received(1).GoToAsync(Routes.Back, Arg.Any<IReadOnlyDictionary<string, object>?>());
    }

    [Fact]
    public async Task Exit_WhenFinished_LeavesWithoutConfirmation()
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        await sut.SubmitCommand.ExecuteAsync(null);
        _dialogs.ClearReceivedCalls();

        await sut.ExitCommand.ExecuteAsync(null);

        await _dialogs.DidNotReceive().ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        await _navigation.Received(1).GoToAsync(Routes.Back, Arg.Any<IReadOnlyDictionary<string, object>?>());
    }

    [Fact]
    public async Task Exit_WhenCreationFailed_LeavesWithoutConfirmation()
    {
        var sut = CreateSut();
        _api.CreateMockExamAsync(Arg.Any<CreateMockExamRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("boom"));
        await sut.OnAppearingAsync();

        await sut.ExitCommand.ExecuteAsync(null);

        await _dialogs.DidNotReceive().ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        await _navigation.Received(1).GoToAsync(Routes.Back, Arg.Any<IReadOnlyDictionary<string, object>?>());
    }

    [Fact]
    public async Task Result_AfterFinishing_OffersAnotherMockExamAndHome()
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        await sut.SubmitCommand.ExecuteAsync(null);

        await sut.AnotherCommand.ExecuteAsync(null);
        await sut.GoHomeCommand.ExecuteAsync(null);

        await _navigation.Received(1).GoToAsync(Routes.Back, Arg.Any<IReadOnlyDictionary<string, object>?>());
        await _navigation.Received(1).GoToAsync(Routes.Root(Routes.Home), Arg.Any<IReadOnlyDictionary<string, object>?>());
    }

    [Fact]
    public async Task Result_SubmittedLate_ShowsLateText()
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        _api.SubmitMockExamAsync(_mockExamId, Arg.Any<SubmitMockExamRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => new SubmitMockExamResponse(BuildResult(_exam, call.Arg<SubmitMockExamRequest>(), late: true), 0));

        await sut.SubmitCommand.ExecuteAsync(null);

        sut.IsLate.ShouldBeTrue();
        sut.LateText.ShouldBe("Entregado fuera de tiempo");
        sut.HasXpText.ShouldBeFalse();
    }

    [Theory]
    [InlineData(50.1, false, "Tiempo: 50:00 de 50:00")]
    [InlineData(55, true, "Tiempo: 55:00 de 50:00")]
    public async Task Result_FinishedAfterTheLimit_ClampsTimeUnlessLate(double usedMinutes, bool late, string expected)
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        _api.SubmitMockExamAsync(_mockExamId, Arg.Any<SubmitMockExamRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => new SubmitMockExamResponse(BuildResult(_exam, call.Arg<SubmitMockExamRequest>(), late: late, usedMinutes: usedMinutes), 0));

        await sut.SubmitCommand.ExecuteAsync(null);

        sut.TimeText.ShouldBe(expected);
    }
}
