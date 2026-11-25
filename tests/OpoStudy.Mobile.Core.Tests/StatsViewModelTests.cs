using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using OpoStudy.Contracts.Exams;
using OpoStudy.Contracts.Progress;
using OpoStudy.Contracts.Quizzes;
using OpoStudy.Contracts.Stats;
using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.Navigation;
using OpoStudy.Mobile.Core.Stats;
using OpoStudy.Mobile.Core.ViewModels;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests;

public sealed class StatsViewModelTests
{
    private static readonly DateOnly Today = new(2026, 11, 24);

    private static readonly ExamResponse ExamA = new(Guid.NewGuid(), "a", "Programador C1", "Diputación", "C1");

    private static readonly ExamResponse ExamB = new(Guid.NewGuid(), "b", "Auxiliar C2", "Diputación", "C2");

    private readonly IOpoStudyApi _api = Substitute.For<IOpoStudyApi>();

    private readonly INavigationService _navigation = Substitute.For<INavigationService>();

    private readonly OfflineDoubles _offline = new();

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 24, 10, 0, 0, TimeSpan.Zero));

    public StatsViewModelTests()
    {
        _api.GetExamsAsync(Arg.Any<CancellationToken>()).Returns([ExamA]);
        _api.GetStatsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(FullStats());
        _api.GetActivityAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(Activity());
    }

    private StatsViewModel CreateSut() => new(_api, _navigation, _time, _offline.State, NullLogger<StatsViewModel>.Instance);

    private static TopicStatsResponse Topic(int number, int answered, int correct, int available = 10) =>
        new(Guid.NewGuid(), number, $"Tema {number}. Título", "Bloque", answered, correct, new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero), available);

    private static StatsResponse FullStats(int topicCount = 7, int answered = 100, int correct = 72) =>
        new(
            ExamA.Id,
            new QuizStatsResponse(
                answered,
                correct,
                9,
                12,
                [.. Enumerable.Range(1, topicCount).Select(n => Topic(n, 10, n))]),
            new PracticalCaseStatsResponse(
                3,
                74.5m,
                [new CaseBestScoreResponse(Guid.NewGuid(), "Caso A", "SQL", 2, Guid.NewGuid(), 18.5m, 25, 74m)],
                [
                    new CaseScoreResponse(Guid.NewGuid(), "Caso A", new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero), 10m, 25, 40m),
                    new CaseScoreResponse(Guid.NewGuid(), "Caso A", new DateTimeOffset(2026, 11, 9, 10, 0, 0, TimeSpan.Zero), 18.5m, 25, 74m),
                ]));

    private static StatsResponse EmptyStats() =>
        new(ExamA.Id, new QuizStatsResponse(0, 0, 0, 12, []), new PracticalCaseStatsResponse(0, null, [], []));

    private static ActivityResponse Activity(DateOnly? today = null, params DayActivityResponse[] days) =>
        new(today ?? Today, 10, days);

    private static DayActivityResponse GoalDay(int day, int month = 11) =>
        new(new DateOnly(2026, month, day), 14, 10, true, false, 160);

    private void Offline() => _offline.Network.HasInternet.Returns(false);

    private static Task<T> Cancellable<T>(CancellationToken token) => new TaskCompletionSource<T>().Task.WaitAsync(token);

    [Fact]
    public async Task OnAppearingAsync_FirstTime_LoadsExamsStatsAndActivityOfTheCurrentMonth()
    {
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        await _api.Received(1).GetExamsAsync(Arg.Any<CancellationToken>());
        await _api.Received(1).GetStatsAsync(ExamA.Id, Arg.Any<CancellationToken>());
        await _api.Received(1).GetActivityAsync(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), Arg.Any<CancellationToken>());
        sut.HasStats.ShouldBeTrue();
        sut.IsBusy.ShouldBeFalse();
        sut.HasError.ShouldBeFalse();
        sut.AnsweredText.ShouldBe("100");
        sut.AccuracyText.ShouldBe("72 %");
        sut.QuizzesText.ShouldBe("9");
        sut.TopicsText.ShouldBe("7 de 12");
        sut.GradingsSummaryText.ShouldBe("3 correcciones · media 74,5 %");
        sut.ScoreBars.Count.ShouldBe(2);
        sut.BestScores.Count.ShouldBe(1);
        sut.MonthTitle.ShouldBe("Noviembre de 2026");
        sut.ShowStatsBlocks.ShouldBeTrue();
    }

    [Fact]
    public async Task OnAppearingAsync_SecondTime_ReloadsKeepingTheExams()
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await sut.OnAppearingAsync();

        await _api.Received(1).GetExamsAsync(Arg.Any<CancellationToken>());
        await _api.Received(2).GetStatsAsync(ExamA.Id, Arg.Any<CancellationToken>());
        await _api.Received(2).GetActivityAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshCommand_Reloading_KeepsTheDataVisible()
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        var gate = new TaskCompletionSource<StatsResponse>();
        _api.GetStatsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(gate.Task);

        var running = sut.RefreshCommand.ExecuteAsync(null);

        sut.IsBusy.ShouldBeTrue();
        sut.HasStats.ShouldBeTrue();
        sut.ShowInitialLoading.ShouldBeFalse();
        sut.AnsweredText.ShouldBe("100");

        gate.SetResult(FullStats(answered: 120));
        await running;
        sut.AnsweredText.ShouldBe("120");
    }

    [Fact]
    public async Task OnAppearingAsync_FirstLoadPending_ShowsTheInitialLoading()
    {
        var gate = new TaskCompletionSource<StatsResponse>();
        _api.GetStatsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(gate.Task);
        var sut = CreateSut();

        var running = sut.OnAppearingAsync();

        sut.ShowInitialLoading.ShouldBeTrue();
        gate.SetResult(FullStats());
        await running;
        sut.ShowInitialLoading.ShouldBeFalse();
    }

    [Fact]
    public async Task Accuracy_NoAnswersButGradings_ShowsDash()
    {
        _api.GetStatsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(FullStats() with
        {
            Quizzes = new QuizStatsResponse(0, 0, 0, 12, []),
        });
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.AccuracyText.ShouldBe("—");
        sut.ShowNoAnswers.ShouldBeTrue();
        sut.ShowNoGradings.ShouldBeFalse();
        sut.IsEmptyStats.ShouldBeFalse();
        sut.ShowStatsBlocks.ShouldBeTrue();
    }

    [Fact]
    public async Task OnAppearingAsync_NoGradings_ShowsOnlyThatBlockEmpty()
    {
        _api.GetStatsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(FullStats() with
        {
            PracticalCases = new PracticalCaseStatsResponse(0, null, [], []),
        });
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.ShowNoGradings.ShouldBeTrue();
        sut.ShowNoAnswers.ShouldBeFalse();
        sut.HasScores.ShouldBeFalse();
    }

    [Fact]
    public async Task OnAppearingAsync_NoDataAtAll_ShowsEmptyStateAndKeepsTheCalendar()
    {
        _api.GetStatsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(EmptyStats());
        _api.GetActivityAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(Activity(null, GoalDay(3)));
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.IsEmptyStats.ShouldBeTrue();
        sut.ShowStatsBlocks.ShouldBeFalse();
        sut.ShowActivity.ShouldBeTrue();
        sut.Cells.Single(c => c.Date == new DateOnly(2026, 11, 3)).IsGoalMet.ShouldBeTrue();
    }

    [Fact]
    public async Task StartQuizCommand_EmptyState_OpensTheTopicsOfTheExam()
    {
        _api.GetStatsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(EmptyStats());
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await sut.StartQuizCommand.ExecuteAsync(null);

        await _navigation.Received(1).GoToAsync(
            Routes.Topics,
            Arg.Is<IReadOnlyDictionary<string, object>>(q => (Guid)q[QueryKeys.ExamId] == ExamA.Id && (string)q[QueryKeys.ExamName] == ExamA.Name));
    }

    [Fact]
    public async Task OnAppearingAsync_NoExams_ShowsMessageAndStillLoadsTheCalendar()
    {
        _api.GetExamsAsync(Arg.Any<CancellationToken>()).Returns([]);
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.NoExams.ShouldBeTrue();
        sut.HasStats.ShouldBeFalse();
        sut.IsBusy.ShouldBeFalse();
        await _api.DidNotReceive().GetStatsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _api.Received(1).GetActivityAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnAppearingAsync_SingleExam_HidesThePicker()
    {
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.ShowExamPicker.ShouldBeFalse();
        sut.SelectedExam!.Exam.Id.ShouldBe(ExamA.Id);
    }

    [Fact]
    public async Task OnAppearingAsync_SeveralExams_ShowsThePickerWithTheFirstSelectedAndLoadsOnce()
    {
        _api.GetExamsAsync(Arg.Any<CancellationToken>()).Returns([ExamA, ExamB]);
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.ShowExamPicker.ShouldBeTrue();
        sut.SelectedExam!.Exam.Id.ShouldBe(ExamA.Id);
        await _api.Received(1).GetStatsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectedExam_SetToTheCurrentOrNull_DoesNotReload()
    {
        _api.GetExamsAsync(Arg.Any<CancellationToken>()).Returns([ExamA, ExamB]);
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        _api.ClearReceivedCalls();

        sut.SelectedExam = null;
        sut.SelectedExam = sut.Exams[0];

        await _api.DidNotReceive().GetStatsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        sut.HasStats.ShouldBeTrue();
    }

    [Fact]
    public async Task SelectedExam_Changed_ClearsAndLoadsTheOtherExam()
    {
        _api.GetExamsAsync(Arg.Any<CancellationToken>()).Returns([ExamA, ExamB]);
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        var gate = new TaskCompletionSource<StatsResponse>();
        _api.GetStatsAsync(ExamB.Id, Arg.Any<CancellationToken>()).Returns(gate.Task);

        sut.SelectedExam = sut.Exams[1];

        // The numbers of the first exam are not shown under the second one while it loads.
        sut.HasStats.ShouldBeFalse();
        sut.ShowInitialLoading.ShouldBeTrue();
        gate.SetResult(FullStats(answered: 50));
        await sut.ChangeExamCommand.ExecutionTask!;
        sut.AnsweredText.ShouldBe("50");
        sut.IsBusy.ShouldBeFalse();
        await _api.Received(1).GetStatsAsync(ExamA.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectedExam_LateResponseOfThePreviousExam_DoesNotOverwriteTheNewOne()
    {
        _api.GetExamsAsync(Arg.Any<CancellationToken>()).Returns([ExamA, ExamB]);
        var gateA = new TaskCompletionSource<StatsResponse>();
        var gateB = new TaskCompletionSource<StatsResponse>();
        _api.GetStatsAsync(ExamA.Id, Arg.Any<CancellationToken>()).Returns(gateA.Task);
        _api.GetStatsAsync(ExamB.Id, Arg.Any<CancellationToken>()).Returns(gateB.Task);
        var sut = CreateSut();
        var first = sut.OnAppearingAsync();

        sut.SelectedExam = sut.Exams[1];
        gateB.SetResult(FullStats(answered: 50));
        await sut.ChangeExamCommand.ExecutionTask!;
        gateA.SetResult(FullStats(answered: 999));
        await first;

        sut.AnsweredText.ShouldBe("50");
        sut.IsBusy.ShouldBeFalse();
    }

    [Fact]
    public async Task SelectedExam_ChangedTwiceQuickly_KeepsTheLastOne()
    {
        _api.GetExamsAsync(Arg.Any<CancellationToken>()).Returns([ExamA, ExamB]);
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        var gateB = new TaskCompletionSource<StatsResponse>();
        _api.GetStatsAsync(ExamB.Id, Arg.Any<CancellationToken>()).Returns(gateB.Task);

        sut.SelectedExam = sut.Exams[1];
        var pendingB = sut.ChangeExamCommand.ExecutionTask!;
        _api.GetStatsAsync(ExamA.Id, Arg.Any<CancellationToken>()).Returns(FullStats(answered: 77));
        sut.SelectedExam = sut.Exams[0];
        await sut.ChangeExamCommand.ExecutionTask!;
        gateB.SetResult(FullStats(answered: 999));
        await pendingB;

        sut.AnsweredText.ShouldBe("77");
        sut.IsBusy.ShouldBeFalse();
    }

    [Fact]
    public async Task OnAppearingAsync_StatsFail_ShowsErrorAndKeepsTheCalendar()
    {
        var error = new HttpRequestException("boom", null, System.Net.HttpStatusCode.InternalServerError);
        _api.GetStatsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).ThrowsAsync(error);
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.ErrorMessage.ShouldBe(ApiErrorMessages.From(error));
        sut.HasStats.ShouldBeFalse();
        sut.NeedsConnection.ShouldBeFalse();
        sut.HasActivityError.ShouldBeFalse();
        sut.Cells.ShouldNotBeEmpty();
        sut.IsBusy.ShouldBeFalse();
    }

    [Fact]
    public async Task RefreshCommand_StatsFailAfterLoading_KeepsTheLoadedData()
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        var error = new HttpRequestException("boom", null, System.Net.HttpStatusCode.InternalServerError);
        _api.GetStatsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).ThrowsAsync(error);

        await sut.RefreshCommand.ExecuteAsync(null);

        sut.HasError.ShouldBeTrue();
        sut.HasStats.ShouldBeTrue();
        sut.AnsweredText.ShouldBe("100");
    }

    [Fact]
    public async Task RefreshCommand_AfterAnError_ClearsItOnSuccess()
    {
        var error = new HttpRequestException("boom", null, System.Net.HttpStatusCode.InternalServerError);
        _api.GetStatsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).ThrowsAsync(error);
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        _api.GetStatsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(FullStats());

        await sut.RefreshCommand.ExecuteAsync(null);

        sut.HasError.ShouldBeFalse();
        sut.HasStats.ShouldBeTrue();
    }

    [Fact]
    public async Task OnAppearingAsync_ExamsFail_ShowsErrorAndRetries()
    {
        var error = new HttpRequestException("boom", null, System.Net.HttpStatusCode.InternalServerError);
        _api.GetExamsAsync(Arg.Any<CancellationToken>()).ThrowsAsync(error);
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.HasError.ShouldBeTrue();
        sut.IsBusy.ShouldBeFalse();
        _api.GetExamsAsync(Arg.Any<CancellationToken>()).Returns([ExamA]);
        await sut.RefreshCommand.ExecuteAsync(null);
        sut.HasError.ShouldBeFalse();
        sut.HasStats.ShouldBeTrue();
    }

    [Fact]
    public async Task OnAppearingAsync_ActivityFails_OnlyTheCalendarCardShowsTheError()
    {
        _api.GetActivityAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("boom"));
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.ActivityError.ShouldBe("No se ha podido cargar la actividad.");
        sut.HasActivityError.ShouldBeTrue();
        sut.HasError.ShouldBeFalse();
        sut.HasStats.ShouldBeTrue();
        sut.HasActivitySummary.ShouldBeFalse();
        sut.IsActivityLoading.ShouldBeFalse();
    }

    [Fact]
    public async Task RetryActivityCommand_AfterFailure_LoadsAgain()
    {
        _api.GetActivityAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("boom"));
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        _api.GetActivityAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(Activity(null, GoalDay(3)));

        await sut.RetryActivityCommand.ExecuteAsync(null);

        sut.HasActivityError.ShouldBeFalse();
        sut.HasActivitySummary.ShouldBeTrue();
    }

    [Fact]
    public async Task OnAppearingAsync_NoConnectionAndNoData_ShowsNeedsConnectionWithoutCallingTheApi()
    {
        Offline();
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.NeedsConnection.ShouldBeTrue();
        sut.ShowActivity.ShouldBeFalse();
        sut.ShowOfflineBanner.ShouldBeFalse();
        sut.IsBusy.ShouldBeFalse();
        await _api.DidNotReceiveWithAnyArgs().GetExamsAsync(TestContext.Current.CancellationToken);
        await _api.DidNotReceiveWithAnyArgs().GetStatsAsync(default, TestContext.Current.CancellationToken);
        await _api.DidNotReceiveWithAnyArgs().GetActivityAsync(default, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RefreshCommand_ConnectionBack_LoadsEverything()
    {
        Offline();
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        _offline.Network.HasInternet.Returns(true);

        await sut.RefreshCommand.ExecuteAsync(null);

        sut.NeedsConnection.ShouldBeFalse();
        sut.HasStats.ShouldBeTrue();
        sut.ShowActivity.ShouldBeTrue();
    }

    [Fact]
    public async Task RefreshCommand_NoConnectionWithLoadedData_KeepsDataAndShowsTheNotice()
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        _api.ClearReceivedCalls();
        Offline();

        await sut.RefreshCommand.ExecuteAsync(null);

        sut.HasStats.ShouldBeTrue();
        sut.NeedsConnection.ShouldBeFalse();
        sut.ShowOfflineBanner.ShouldBeTrue();
        sut.OfflineBannerText.ShouldBe("Sin conexión. Se muestran los últimos datos cargados.");
        sut.AnsweredText.ShouldBe("100");
        await _api.DidNotReceiveWithAnyArgs().GetStatsAsync(default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RefreshCommand_ConnectivityErrorWithLoadedData_KeepsDataAndShowsTheNotice()
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        _api.GetStatsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException("down"));

        await sut.RefreshCommand.ExecuteAsync(null);

        sut.HasStats.ShouldBeTrue();
        sut.HasError.ShouldBeFalse();
        sut.ShowOfflineBanner.ShouldBeTrue();
    }

    [Fact]
    public async Task OnAppearingAsync_ConnectivityErrorWithoutData_ShowsNeedsConnection()
    {
        _api.GetStatsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException("down"));
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.NeedsConnection.ShouldBeTrue();
        sut.HasError.ShouldBeFalse();
    }

    [Fact]
    public async Task OnDisappearing_RequestPending_LeavesTheScreenIdleWithoutErrors()
    {
        _api.GetStatsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(ci => Cancellable<StatsResponse>(ci.Arg<CancellationToken>()));
        _api.GetActivityAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(ci => Cancellable<ActivityResponse>(ci.Arg<CancellationToken>()));
        var sut = CreateSut();
        var running = sut.OnAppearingAsync();
        sut.IsBusy.ShouldBeTrue();
        sut.IsActivityLoading.ShouldBeTrue();

        sut.OnDisappearing();
        await running;

        sut.IsBusy.ShouldBeFalse();
        sut.IsActivityLoading.ShouldBeFalse();
        sut.HasError.ShouldBeFalse();
        sut.HasActivityError.ShouldBeFalse();
        sut.NeedsConnection.ShouldBeFalse();
        sut.RefreshCommand.CanExecute(null).ShouldBeTrue();
    }

    [Fact]
    public async Task OnDisappearing_MonthRequestPending_DoesNotShowAnError()
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        _api.GetActivityAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(ci => Cancellable<ActivityResponse>(ci.Arg<CancellationToken>()));
        var running = sut.PreviousMonthCommand.ExecuteAsync(null);

        sut.OnDisappearing();
        await running;

        sut.IsActivityLoading.ShouldBeFalse();
        sut.HasActivityError.ShouldBeFalse();
    }

    [Fact]
    public async Task OnAppearingAsync_ReturnsAfterDisappearing_LoadsAgain()
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        sut.OnDisappearing();

        await sut.OnAppearingAsync();

        await _api.Received(2).GetStatsAsync(ExamA.Id, Arg.Any<CancellationToken>());
        sut.HasStats.ShouldBeTrue();
    }

    [Fact]
    public async Task PreviousMonthCommand_Executed_LoadsThePreviousMonthInclusiveRange()
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await sut.PreviousMonthCommand.ExecuteAsync(null);

        await _api.Received(1).GetActivityAsync(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), Arg.Any<CancellationToken>());
        sut.MonthTitle.ShouldBe("Octubre de 2026");
        sut.NextMonthCommand.CanExecute(null).ShouldBeTrue();
        sut.CanGoNext.ShouldBeTrue();
    }

    [Fact]
    public async Task NextMonthCommand_CurrentMonth_IsDisabled()
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        sut.CanGoNext.ShouldBeFalse();
        sut.NextMonthCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public async Task NextMonthCommand_AfterGoingBack_ReturnsToTheCurrentMonthAndDisablesAgain()
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        await sut.PreviousMonthCommand.ExecuteAsync(null);

        await sut.NextMonthCommand.ExecuteAsync(null);

        sut.MonthTitle.ShouldBe("Noviembre de 2026");
        sut.NextMonthCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public async Task PreviousMonthCommand_LoadedMonth_ShowsItsOwnDays()
    {
        _api.GetActivityAsync(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), Arg.Any<CancellationToken>())
            .Returns(Activity(null, GoalDay(5, 10)));
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await sut.PreviousMonthCommand.ExecuteAsync(null);

        sut.Cells.Single(c => c.Date == new DateOnly(2026, 10, 5)).IsGoalMet.ShouldBeTrue();
        sut.ActivitySummaryText.ShouldBe("1 día con actividad · 1 objetivo cumplido · +160 XP");
    }

    [Fact]
    public async Task PreviousMonthCommand_WhileLoading_DoesNotShowTheOtherMonthDays()
    {
        _api.GetActivityAsync(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), Arg.Any<CancellationToken>())
            .Returns(Activity(null, GoalDay(3)));
        var gate = new TaskCompletionSource<ActivityResponse>();
        _api.GetActivityAsync(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), Arg.Any<CancellationToken>()).Returns(gate.Task);
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        var running = sut.PreviousMonthCommand.ExecuteAsync(null);

        sut.IsActivityLoading.ShouldBeTrue();
        sut.HasActivitySummary.ShouldBeFalse();
        sut.Cells.Where(c => !c.IsPlaceholder).ShouldAllBe(c => c.Mark.Length == 0);
        gate.SetResult(Activity());
        await running;
    }

    [Fact]
    public async Task PreviousMonthCommand_LateResponseOfAnOlderMonth_DoesNotOverwriteTheNewerOne()
    {
        var gateOct = new TaskCompletionSource<ActivityResponse>();
        var gateSep = new TaskCompletionSource<ActivityResponse>();
        _api.GetActivityAsync(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), Arg.Any<CancellationToken>()).Returns(gateOct.Task);
        _api.GetActivityAsync(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), Arg.Any<CancellationToken>()).Returns(gateSep.Task);
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        var october = sut.PreviousMonthCommand.ExecuteAsync(null);
        var september = sut.PreviousMonthCommand.ExecuteAsync(null);
        gateSep.SetResult(Activity(null, GoalDay(8, 9)));
        await september;
        gateOct.SetResult(Activity(null, GoalDay(5, 10)));
        await october;

        sut.MonthTitle.ShouldBe("Septiembre de 2026");
        sut.Cells.Single(c => c.Date == new DateOnly(2026, 9, 8)).IsGoalMet.ShouldBeTrue();
        sut.Cells.Where(c => c.IsGoalMet).Count().ShouldBe(1);
        sut.IsActivityLoading.ShouldBeFalse();
    }

    [Fact]
    public async Task PreviousMonthCommand_FailedMonth_NeverShowsTheDaysOfAnotherMonth()
    {
        _api.GetActivityAsync(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), Arg.Any<CancellationToken>())
            .Returns(Activity(null, GoalDay(3)));
        _api.GetActivityAsync(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await sut.PreviousMonthCommand.ExecuteAsync(null);

        sut.HasActivityError.ShouldBeTrue();
        sut.MonthTitle.ShouldBe("Octubre de 2026");
        sut.HasActivitySummary.ShouldBeFalse();
        sut.Cells.Where(c => !c.IsPlaceholder).ShouldAllBe(c => c.Mark.Length == 0);
    }

    [Fact]
    public async Task RetryActivityCommand_AfterFailedMonth_LoadsThatMonth()
    {
        _api.GetActivityAsync(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        await sut.PreviousMonthCommand.ExecuteAsync(null);
        _api.GetActivityAsync(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), Arg.Any<CancellationToken>())
            .Returns(Activity(null, GoalDay(5, 10)));

        await sut.RetryActivityCommand.ExecuteAsync(null);

        sut.HasActivityError.ShouldBeFalse();
        sut.Cells.Single(c => c.Date == new DateOnly(2026, 10, 5)).IsGoalMet.ShouldBeTrue();
    }

    [Fact]
    public async Task RefreshCommand_AfterBrowsingBack_ReloadsTheShownMonth()
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        await sut.PreviousMonthCommand.ExecuteAsync(null);
        _api.ClearReceivedCalls();

        await sut.RefreshCommand.ExecuteAsync(null);

        await _api.Received(1).GetActivityAsync(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PreviousMonthCommand_NoConnection_ShowsTheActivityErrorWithoutCalling()
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        _api.ClearReceivedCalls();
        Offline();

        await sut.PreviousMonthCommand.ExecuteAsync(null);

        sut.HasActivityError.ShouldBeTrue();
        sut.HasActivitySummary.ShouldBeFalse();
        await _api.DidNotReceiveWithAnyArgs().GetActivityAsync(default, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task OnAppearingAsync_ServerTodayInAnotherMonth_JumpsToThatMonth()
    {
        // The device says 24 November, but for the user (time zone of the profile) it is already 1 December.
        var december = new DateOnly(2026, 12, 1);
        _api.GetActivityAsync(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31), Arg.Any<CancellationToken>())
            .Returns(Activity(december, new DayActivityResponse(december, 3, 10, false, false, 30)));
        _api.GetActivityAsync(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), Arg.Any<CancellationToken>()).Returns(Activity(december));
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.MonthTitle.ShouldBe("Diciembre de 2026");
        sut.Cells.Single(c => c.IsToday).Date.ShouldBe(december);
        sut.HasActivitySummary.ShouldBeTrue();
        sut.CanGoNext.ShouldBeFalse();
    }

    [Fact]
    public async Task SelectDayCommand_PastDay_ShowsItsDetail()
    {
        _api.GetActivityAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(Activity(null, GoalDay(12)));
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        sut.SelectDayCommand.Execute(sut.Cells.Single(c => c.Date == new DateOnly(2026, 11, 12)));

        sut.HasSelectedDay.ShouldBeTrue();
        sut.SelectedDayText.ShouldBe("Jueves 12 de noviembre: 14 preguntas de 10 · objetivo cumplido · +160 XP");
    }

    [Fact]
    public async Task SelectDayCommand_DayWithoutActivity_SaysSo()
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        sut.SelectDayCommand.Execute(sut.Cells.Single(c => c.Date == new DateOnly(2026, 11, 10)));

        sut.SelectedDayText.ShouldBe("Martes 10 de noviembre: sin actividad");
    }

    [Fact]
    public async Task SelectDayCommand_FutureOrBlankCell_IsIgnored()
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        sut.SelectDayCommand.Execute(sut.Cells.Single(c => c.Date == new DateOnly(2026, 11, 30)));
        sut.SelectDayCommand.Execute(sut.Cells.First(c => c.IsPlaceholder));

        sut.HasSelectedDay.ShouldBeFalse();
    }

    [Fact]
    public async Task PreviousMonthCommand_WithSelectedDay_ClearsTheDetail()
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        sut.SelectDayCommand.Execute(sut.Cells.Single(c => c.Date == new DateOnly(2026, 11, 10)));

        await sut.PreviousMonthCommand.ExecuteAsync(null);

        sut.HasSelectedDay.ShouldBeFalse();
    }

    [Fact]
    public async Task ToggleTopicsCommand_MoreThanFive_ShowsTheWeakestFiveThenAll()
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        sut.VisibleTopics.Count.ShouldBe(5);
        sut.CanToggleTopics.ShouldBeTrue();
        sut.ToggleTopicsText.ShouldBe("Ver todos los temas (7)");

        sut.ToggleTopicsCommand.Execute(null);
        sut.VisibleTopics.Count.ShouldBe(7);
        sut.ToggleTopicsText.ShouldBe("Ver menos");

        sut.ToggleTopicsCommand.Execute(null);
        sut.VisibleTopics.Count.ShouldBe(5);
        sut.VisibleTopics[0].Topic.Number.ShouldBe(1);
    }

    [Fact]
    public async Task OnAppearingAsync_FiveTopicsOrLess_HasNoToggle()
    {
        _api.GetStatsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(FullStats(topicCount: 5));
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.VisibleTopics.Count.ShouldBe(5);
        sut.CanToggleTopics.ShouldBeFalse();
    }

    [Fact]
    public async Task RefreshCommand_ListExpanded_KeepsAllTopicsAcrossReloads()
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        sut.ToggleTopicsCommand.Execute(null);

        await sut.RefreshCommand.ExecuteAsync(null);
        await sut.OnAppearingAsync();

        sut.VisibleTopics.Count.ShouldBe(7);
        sut.ToggleTopicsText.ShouldBe("Ver menos");
    }

    [Fact]
    public async Task SelectedExam_ListExpanded_CollapsesWhenTheExamChanges()
    {
        _api.GetExamsAsync(Arg.Any<CancellationToken>()).Returns([ExamA, ExamB]);
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        sut.ToggleTopicsCommand.Execute(null);

        sut.SelectedExam = sut.Exams[1];
        await sut.ChangeExamCommand.ExecutionTask!;

        sut.VisibleTopics.Count.ShouldBe(5);
        sut.ToggleTopicsText.ShouldBe("Ver todos los temas (7)");
    }

    [Fact]
    public async Task PracticeTopicCommand_SecondAttempt_ClearsTheMessageOfOtherTopics()
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        Offline();
        await sut.PracticeTopicCommand.ExecuteAsync(sut.VisibleTopics[3]);
        sut.VisibleTopics[3].HasMessage.ShouldBeTrue();
        sut.VisibleTopics[0].HasMessage.ShouldBeFalse();

        await sut.PracticeTopicCommand.ExecuteAsync(sut.VisibleTopics[1]);

        sut.VisibleTopics[1].HasMessage.ShouldBeTrue();
        sut.VisibleTopics[3].HasMessage.ShouldBeFalse();
    }

    [Fact]
    public async Task RefreshCommand_AfterAMessage_ShowsFreshTopicsWithoutIt()
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        Offline();
        await sut.PracticeTopicCommand.ExecuteAsync(sut.VisibleTopics[0]);
        _offline.Network.HasInternet.Returns(true);

        await sut.RefreshCommand.ExecuteAsync(null);

        sut.VisibleTopics.ShouldAllBe(t => !t.HasMessage);
    }

    [Fact]
    public async Task OnDisappearing_AfterTheFirstActivityResponseNeedsAJump_DoesNotLeaveTheCalendarLoading()
    {
        var december = new DateOnly(2026, 12, 1);
        var sut = CreateSut();
        _api.GetActivityAsync(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                // The page goes away right after the response that asks for another month.
                sut.OnDisappearing();
                return Activity(december);
            });

        await sut.OnAppearingAsync();

        sut.IsActivityLoading.ShouldBeFalse();
        sut.HasActivityError.ShouldBeFalse();
        await _api.DidNotReceive().GetActivityAsync(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void BuildCells_EveryCell_ReservesTheMarkLine()
    {
        var cells = ActivityCalendar.BuildCells(new DateOnly(2026, 11, 1), Today, []);

        cells.Where(c => !c.IsPlaceholder).ShouldAllBe(c => c.MarkLine.Length > 0);
    }

    [Fact]
    public async Task PracticeTopicCommand_Online_CreatesTenQuestionsQuizAndOpensIt()
    {
        var quizId = Guid.NewGuid();
        _api.CreateQuizAsync(Arg.Any<CreateQuizRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QuizResponse(quizId, ExamA.Id, null, "ByTopic", DateTimeOffset.UtcNow, null, 0, 0, []));
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        var topic = sut.VisibleTopics[0];

        await sut.PracticeTopicCommand.ExecuteAsync(topic);

        await _api.Received(1).CreateQuizAsync(
            Arg.Is<CreateQuizRequest>(r => r.ExamId == ExamA.Id && r.TopicId == topic.Id && r.QuestionCount == 10 && !r.MistakesOnly),
            Arg.Any<CancellationToken>());
        await _navigation.Received(1).GoToAsync(
            Routes.Quiz,
            Arg.Is<IReadOnlyDictionary<string, object>>(q => (Guid)q[QueryKeys.QuizId] == quizId && (string)q[QueryKeys.QuizTitle] == topic.Title));
        sut.IsBusy.ShouldBeFalse();
        topic.HasMessage.ShouldBeFalse();
    }

    [Fact]
    public async Task PracticeTopicCommand_NoConnection_ShowsTheMessageWithoutCalling()
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        Offline();

        await sut.PracticeTopicCommand.ExecuteAsync(sut.VisibleTopics[0]);

        sut.VisibleTopics[0].Message.ShouldBe("No hay conexión. Puedes hacer un test sin conexión desde «Hacer un test».");
        await _api.DidNotReceiveWithAnyArgs().CreateQuizAsync(default!, TestContext.Current.CancellationToken);
        await _navigation.DidNotReceiveWithAnyArgs().GoToAsync(default!, default!);
    }

    [Fact]
    public async Task PracticeTopicCommand_ConnectivityErrorWhileCreating_ShowsTheOfflineMessage()
    {
        _api.CreateQuizAsync(Arg.Any<CreateQuizRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException("down"));
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await sut.PracticeTopicCommand.ExecuteAsync(sut.VisibleTopics[0]);

        sut.VisibleTopics[0].Message.ShouldBe(StatsTexts.PracticeOffline);
        sut.IsBusy.ShouldBeFalse();
    }

    [Fact]
    public async Task PracticeTopicCommand_ServerError_ShowsTheErrorAndStaysOnScreen()
    {
        var error = new HttpRequestException("boom", null, System.Net.HttpStatusCode.InternalServerError);
        _api.CreateQuizAsync(Arg.Any<CreateQuizRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(error);
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await sut.PracticeTopicCommand.ExecuteAsync(sut.VisibleTopics[0]);

        sut.VisibleTopics[0].Message.ShouldBe(ApiErrorMessages.From(error));
        sut.HasStats.ShouldBeTrue();
        await _navigation.DidNotReceiveWithAnyArgs().GoToAsync(default!, default!);
    }

    [Fact]
    public async Task PracticeTopicCommand_TopicWithoutQuestions_IsDisabled()
    {
        var stats = FullStats(topicCount: 2);
        stats = stats with
        {
            Quizzes = stats.Quizzes with { Topics = [Topic(1, 10, 1, available: 0), Topic(2, 10, 2)] },
        };
        _api.GetStatsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(stats);
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        sut.PracticeTopicCommand.CanExecute(sut.VisibleTopics[0]).ShouldBeFalse();
        sut.PracticeTopicCommand.CanExecute(sut.VisibleTopics[1]).ShouldBeTrue();
    }

    [Fact]
    public async Task PracticeTopicCommand_WhileBusy_IsDisabled()
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        var topic = sut.VisibleTopics[0];
        var gate = new TaskCompletionSource<StatsResponse>();
        _api.GetStatsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(gate.Task);

        var running = sut.RefreshCommand.ExecuteAsync(null);

        sut.PracticeTopicCommand.CanExecute(topic).ShouldBeFalse();
        gate.SetResult(FullStats());
        await running;
        sut.PracticeTopicCommand.CanExecute(topic).ShouldBeTrue();
    }

    [Fact]
    public async Task OpenGradingCommand_BestScore_NavigatesWithTheBestGradingId()
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        var best = sut.BestScores[0];

        await sut.OpenGradingCommand.ExecuteAsync(best);

        await _navigation.Received(1).GoToAsync(
            Routes.Grading,
            Arg.Is<IReadOnlyDictionary<string, object>>(q => (Guid)q[QueryKeys.GradingId] == best.Best.BestGradingId));
    }

    [Fact]
    public async Task OnAppearingAsync_Online_ReportsSuccessSoTheBannerGoesAway()
    {
        _offline.State.ReportConnectivityFailure();
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        _offline.State.IsOffline.ShouldBeFalse();
        sut.ShowOfflineBanner.ShouldBeFalse();
    }
}