using System.Net;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using OpoStudy.Contracts.Gradings;
using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.Navigation;
using OpoStudy.Mobile.Core.PracticalCases;
using OpoStudy.Mobile.Core.Services;
using OpoStudy.Mobile.Core.ViewModels;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests.PracticalCases;

public sealed class GradingViewModelTests
{
    private readonly IOpoStudyApi _api = Substitute.For<IOpoStudyApi>();

    private readonly INavigationService _navigation = Substitute.For<INavigationService>();

    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();

    private readonly FakeDraftStore _drafts = new();

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 23, 9, 0, 0, TimeSpan.Zero));

    private GradingViewModel CreateSut()
    {
        var sut = new GradingViewModel(_api, _navigation, _dialogs, _drafts, _time, NullLogger<GradingViewModel>.Instance);
        sut.ApplyQuery(new Dictionary<string, object> { [QueryKeys.GradingId] = PracticalCaseTestData.GradingId });
        return sut;
    }

    private void GivenResponses(params GradingResponse[] responses) =>
        _api.GetGradingAsync(PracticalCaseTestData.GradingId, Arg.Any<CancellationToken>())
            .Returns(responses[0], responses.Skip(1).ToArray());

    private int Calls() => _api.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IOpoStudyApi.GetGradingAsync));

    /// <summary>Advances the clock and waits until the polling loop made one more query.</summary>
    private async Task AdvanceAndWaitForPollAsync(TimeSpan time)
    {
        var before = Calls();
        _time.Advance(time);
        await PracticalCaseTestData.EventuallyAsync(() => Calls() == before + 1);
        await Task.Yield();
    }

    [Theory]
    [InlineData("Pending", 0, "En cola. Empezará en unos segundos.")]
    [InlineData("Processing", 0, "Corrigiendo tu supuesto… Suele tardar menos de un minuto.")]
    [InlineData("Pending", 2, "El corrector no respondía. Lo reintentaremos en unos minutos.")]
    [InlineData("Weird", 0, "Corrigiendo tu supuesto… Suele tardar menos de un minuto.")]
    public async Task OnAppearingAsync_InProgress_ShowsStatusText(string status, int attempts, string expected)
    {
        GivenResponses(PracticalCaseTestData.Grading(status, attempts));
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.StatusText.ShouldBe(expected);
        sut.IsInProgress.ShouldBeTrue();
        sut.CaseTitle.ShouldBe("Supuesto 1");
        sut.OnDisappearing();
    }

    [Fact]
    public async Task OnAppearingAsync_AlreadyCompleted_ShowsScoreWithoutAlertOrPolling()
    {
        GivenResponses(PracticalCaseTestData.Grading("Completed", score: 18.5m));
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.IsCompleted.ShouldBeTrue();
        sut.ScoreText.ShouldBe("18,5 / 25 puntos");
        sut.StatusText.ShouldBe("¡Corrección lista!");
        await _dialogs.DidNotReceiveWithAnyArgs().AlertAsync(default!, default!);
        _time.Advance(TimeSpan.FromMinutes(1));
        Calls().ShouldBe(1);
    }

    [Fact]
    public async Task Polling_PendingProcessingCompleted_UsesIntervalsAndAlertsOnce()
    {
        GivenResponses(
            PracticalCaseTestData.Grading("Pending"),
            PracticalCaseTestData.Grading("Processing"),
            PracticalCaseTestData.Grading("Completed", score: 20m));
        var sut = CreateSut();

        await sut.OnAppearingAsync();
        Calls().ShouldBe(1);

        _time.Advance(TimeSpan.FromSeconds(2.9));
        Calls().ShouldBe(1);
        await AdvanceAndWaitForPollAsync(TimeSpan.FromSeconds(0.1));
        sut.StatusText.ShouldStartWith("Corrigiendo");

        _time.Advance(TimeSpan.FromSeconds(4.9));
        Calls().ShouldBe(2);
        await AdvanceAndWaitForPollAsync(TimeSpan.FromSeconds(0.1));

        await PracticalCaseTestData.EventuallyAsync(() => _dialogs.ReceivedCalls().Any());
        sut.IsCompleted.ShouldBeTrue();
        await _dialogs.Received(1).AlertAsync("Corrección lista", "Has sacado 20 de 25 puntos.");

        _time.Advance(TimeSpan.FromMinutes(1));
        Calls().ShouldBe(3);
    }

    [Fact]
    public async Task Polling_AfterThreeMinutes_SlowsDownToFifteenSeconds()
    {
        GivenResponses(PracticalCaseTestData.Grading("Processing"));
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await AdvanceAndWaitForPollAsync(TimeSpan.FromSeconds(3));
        for (var i = 0; i < 36; i++)
        {
            await AdvanceAndWaitForPollAsync(TimeSpan.FromSeconds(5));
        }

        // Past the three minutes: the next wait is 15 s, not 5 s.
        var before = Calls();
        _time.Advance(TimeSpan.FromSeconds(14.9));
        Calls().ShouldBe(before);
        await AdvanceAndWaitForPollAsync(TimeSpan.FromSeconds(0.1));
        sut.OnDisappearing();
    }

    [Fact]
    public async Task Polling_Failed_ShowsReasonAndAlertsForEachCode()
    {
        GivenResponses(
            PracticalCaseTestData.Grading("Processing"),
            PracticalCaseTestData.Grading("Failed", failureCode: "grading.provider_unavailable"));
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await AdvanceAndWaitForPollAsync(TimeSpan.FromSeconds(3));

        await PracticalCaseTestData.EventuallyAsync(() => sut.IsFailed);
        sut.StatusText.ShouldBe("El corrector no está disponible ahora mismo.");
        await _dialogs.Received(1).AlertAsync("No se ha podido corregir", "El corrector no está disponible ahora mismo.");
    }

    [Fact]
    public async Task OnAppearingAsync_FailedWithOtherCode_ShowsGenericText()
    {
        GivenResponses(PracticalCaseTestData.Grading("Failed", failureCode: "grading.something"));
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.StatusText.ShouldBe("No se ha podido corregir tu supuesto.");
    }

    [Fact]
    public async Task Polling_TransientError_KeepsGoingWithFifteenSecondWait()
    {
        _api.GetGradingAsync(PracticalCaseTestData.GradingId, Arg.Any<CancellationToken>()).Returns(
            _ => Task.FromResult(PracticalCaseTestData.Grading("Pending")),
            _ => Task.FromException<GradingResponse>(new HttpRequestException()),
            _ => Task.FromResult(PracticalCaseTestData.Grading("Processing")));
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await AdvanceAndWaitForPollAsync(TimeSpan.FromSeconds(3));
        sut.ConnectionNotice.ShouldBe("Sin conexión. Seguimos intentándolo…");

        _time.Advance(TimeSpan.FromSeconds(14.9));
        Calls().ShouldBe(2);
        await AdvanceAndWaitForPollAsync(TimeSpan.FromSeconds(0.1));

        sut.HasConnectionNotice.ShouldBeFalse();
        sut.OnDisappearing();
    }

    [Fact]
    public async Task Polling_NotFound_StopsWithServerMessage()
    {
        _api.GetGradingAsync(PracticalCaseTestData.GradingId, Arg.Any<CancellationToken>())
            .ThrowsAsync(await PracticalCaseTestData.ApiExceptionAsync(HttpStatusCode.NotFound, "grading.not_found", "La corrección no existe."));
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.ErrorMessage.ShouldBe("La corrección no existe.");
        _time.Advance(TimeSpan.FromMinutes(1));
        Calls().ShouldBe(1);
    }

    [Fact]
    public async Task OnDisappearing_StopsPollingAndAppearingResumesWithoutDuplicates()
    {
        GivenResponses(PracticalCaseTestData.Grading("Processing"));
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        sut.OnDisappearing();
        _time.Advance(TimeSpan.FromMinutes(1));
        Calls().ShouldBe(1);

        await sut.OnAppearingAsync();
        await sut.OnAppearingAsync();
        Calls().ShouldBe(2);

        // One loop only: a single query per interval.
        await AdvanceAndWaitForPollAsync(TimeSpan.FromSeconds(3));
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Calls().ShouldBe(3);
        sut.OnDisappearing();
    }

    [Fact]
    public async Task RecoverAnswersCommand_Failed_SavesOrderedDraftAndNavigatesReplacingThePage()
    {
        GivenResponses(PracticalCaseTestData.Grading("Failed"));
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        sut.RecoverAnswersCommand.CanExecute(null).ShouldBeTrue();
        await sut.RecoverAnswersCommand.ExecuteAsync(null);

        _drafts.Drafts[PracticalCaseTestData.CaseId].Answers.ShouldBe(["primera", "segunda"]);
        await _navigation.Received(1).GoToAsync(
            "../practical-case",
            Arg.Is<IReadOnlyDictionary<string, object>>(q => (Guid)q[QueryKeys.PracticalCaseId] == PracticalCaseTestData.CaseId));
    }

    [Fact]
    public async Task GoBackCommand_NavigatesBack()
    {
        GivenResponses(PracticalCaseTestData.Grading("Completed", score: 1m));
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await sut.GoBackCommand.ExecuteAsync(null);

        await _navigation.Received(1).GoToAsync(Routes.Back, null);
    }
}