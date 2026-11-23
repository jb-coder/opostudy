using System.Net;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using OpoStudy.Contracts.Gradings;
using OpoStudy.Contracts.PracticalCases;
using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.Navigation;
using OpoStudy.Mobile.Core.PracticalCases;
using OpoStudy.Mobile.Core.Services;
using OpoStudy.Mobile.Core.ViewModels;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests.PracticalCases;

public sealed class PracticalCaseViewModelTests
{
    private readonly IOpoStudyApi _api = Substitute.For<IOpoStudyApi>();

    private readonly INavigationService _navigation = Substitute.For<INavigationService>();

    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();

    private readonly FakeDraftStore _drafts = new();

    private readonly OfflineDoubles _offline = new();

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 23, 9, 0, 0, TimeSpan.Zero));

    public PracticalCaseViewModelTests()
    {
        _api.GetPracticalCaseAsync(PracticalCaseTestData.CaseId, Arg.Any<CancellationToken>()).Returns(PracticalCaseTestData.Case());
        _api.GetGradingQuotaAsync(Arg.Any<CancellationToken>()).Returns(PracticalCaseTestData.Quota());
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);
    }

    private PracticalCaseViewModel CreateSut()
    {
        var sut = new PracticalCaseViewModel(
            _api, _navigation, _dialogs, _drafts, _offline.State, _time, NullLogger<PracticalCaseViewModel>.Instance);
        sut.ApplyQuery(new Dictionary<string, object> { [QueryKeys.PracticalCaseId] = PracticalCaseTestData.CaseId });
        return sut;
    }

    private async Task<PracticalCaseViewModel> LoadedAsync()
    {
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        return sut;
    }

    [Fact]
    public async Task OnAppearingAsync_Loads_ShowsCaseAndParts()
    {
        var sut = await LoadedAsync();

        sut.IsLoaded.ShouldBeTrue();
        sut.Title.ShouldBe("Supuesto 1");
        sut.Header.ShouldBe("~45 min · 25 puntos");
        sut.Parts.Count.ShouldBe(2);
        sut.Parts[0].Title.ShouldBe("Apartado 1 · 10 puntos");
        sut.QuotaSummary.ShouldBe("3/5 este mes");
    }

    [Fact]
    public async Task OnAppearingAsync_SecondTime_DoesNotReloadNorOverwriteAnswers()
    {
        var sut = await LoadedAsync();
        sut.Parts[0].Answer = "escrito";

        await sut.OnAppearingAsync();

        sut.Parts[0].Answer.ShouldBe("escrito");
        await _api.Received(1).GetPracticalCaseAsync(PracticalCaseTestData.CaseId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnAppearingAsync_QuotaFails_StillLoadsTheCase()
    {
        _api.GetGradingQuotaAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException());

        var sut = await LoadedAsync();

        sut.IsLoaded.ShouldBeTrue();
        sut.HasQuota.ShouldBeFalse();
    }

    [Fact]
    public async Task OnAppearingAsync_Offline_ShowsNeedsConnectionWithoutCalling()
    {
        _offline.Network.HasInternet.Returns(false);

        var sut = await LoadedAsync();

        sut.NeedsConnection.ShouldBeTrue();
        await _api.DidNotReceiveWithAnyArgs().GetPracticalCaseAsync(default, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnAppearingAsync_ConnectivityError_ShowsNeedsConnection()
    {
        _api.GetPracticalCaseAsync(PracticalCaseTestData.CaseId, Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException());

        var sut = await LoadedAsync();

        sut.NeedsConnection.ShouldBeTrue();
        sut.IsLoaded.ShouldBeFalse();
    }

    [Fact]
    public async Task OnAppearingAsync_OtherError_ShowsMessageWithRetry()
    {
        _api.GetPracticalCaseAsync(PracticalCaseTestData.CaseId, Arg.Any<CancellationToken>())
            .ThrowsAsync(await PracticalCaseTestData.ApiExceptionAsync(HttpStatusCode.InternalServerError));

        var sut = await LoadedAsync();

        sut.HasLoadError.ShouldBeTrue();
        sut.NeedsConnection.ShouldBeFalse();
    }

    [Fact]
    public async Task OnAppearingAsync_DraftMatches_RestoresAnswersAndNotice()
    {
        _drafts.Drafts[PracticalCaseTestData.CaseId] = new PracticalCaseDraft(["uno", "dos"], new DateTimeOffset(2026, 11, 20, 10, 0, 0, TimeSpan.Zero));

        var sut = await LoadedAsync();

        sut.Parts.Select(p => p.Answer).ShouldBe(["uno", "dos"]);
        sut.HasDraftNotice.ShouldBeTrue();
        sut.DraftNotice.ShouldStartWith("Borrador recuperado (");
        _drafts.Saves.ShouldBe(0);
    }

    [Fact]
    public async Task OnAppearingAsync_DraftWithOtherPartCount_IsDiscarded()
    {
        _drafts.Drafts[PracticalCaseTestData.CaseId] = new PracticalCaseDraft(["solo uno"], DateTimeOffset.UtcNow);

        var sut = await LoadedAsync();

        sut.Parts.ShouldAllBe(p => p.Answer == string.Empty);
        sut.HasDraftNotice.ShouldBeFalse();
        _drafts.Drafts.ShouldBeEmpty();
    }

    [Fact]
    public async Task Answer_Changed_SavesOnceAfterDebounce()
    {
        var sut = await LoadedAsync();

        sut.Parts[0].Answer = "a";
        _time.Advance(TimeSpan.FromMilliseconds(1000));
        sut.Parts[0].Answer = "ab";
        _time.Advance(TimeSpan.FromMilliseconds(1000));
        _drafts.Saves.ShouldBe(0);
        _time.Advance(TimeSpan.FromMilliseconds(500));
        await PracticalCaseTestData.EventuallyAsync(() => _drafts.Saves == 1);

        _drafts.Drafts[PracticalCaseTestData.CaseId].Answers.ShouldBe(["ab", ""]);
        sut.SavedNotice.ShouldBe("Guardado en el dispositivo");
    }

    [Fact]
    public async Task OnDisappearing_PendingChanges_SavesImmediately()
    {
        var sut = await LoadedAsync();
        sut.Parts[1].Answer = "texto";

        sut.OnDisappearing();

        _drafts.Drafts[PracticalCaseTestData.CaseId].Answers.ShouldBe(["", "texto"]);
        _time.Advance(TimeSpan.FromSeconds(5));
        _drafts.Saves.ShouldBe(1);
    }

    [Fact]
    public async Task OnDisappearing_AllBlank_DeletesTheDraft()
    {
        _drafts.Drafts[PracticalCaseTestData.CaseId] = new PracticalCaseDraft(["x", ""], DateTimeOffset.UtcNow);
        var sut = await LoadedAsync();
        sut.Parts[0].Answer = string.Empty;

        sut.OnDisappearing();

        _drafts.Drafts.ShouldBeEmpty();
    }

    [Fact]
    public async Task SubmitCommand_AllBlank_CannotExecute()
    {
        var sut = await LoadedAsync();

        sut.SubmitCommand.CanExecute(null).ShouldBeFalse();

        sut.Parts[0].Answer = "algo";
        sut.SubmitCommand.CanExecute(null).ShouldBeTrue();
    }

    [Fact]
    public async Task SubmitCommand_QuotaExhausted_CannotExecuteAndExplainsIt()
    {
        _api.GetGradingQuotaAsync(Arg.Any<CancellationToken>()).Returns(PracticalCaseTestData.Quota(0));
        var sut = await LoadedAsync();
        sut.Parts[0].Answer = "algo";

        sut.SubmitCommand.CanExecute(null).ShouldBeFalse();
        sut.QuotaMessage.ShouldBe("Has agotado tus 5 correcciones de este mes");
    }

    [Fact]
    public async Task SubmitCommand_Busy_CannotExecute()
    {
        var sut = await LoadedAsync();
        sut.Parts[0].Answer = "algo";

        sut.IsBusy = true;

        sut.SubmitCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public async Task SubmitCommand_ConfirmationCancelled_DoesNotSend()
    {
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(false);
        var sut = await LoadedAsync();
        sut.Parts[0].Answer = "algo";

        await sut.SubmitCommand.ExecuteAsync(null);

        await _api.DidNotReceiveWithAnyArgs().SubmitGradingAsync(default, default!, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubmitCommand_Accepted_SendsAllAnswersInOrderAndNavigatesReplacingThePage()
    {
        _api.SubmitGradingAsync(PracticalCaseTestData.CaseId, Arg.Any<SubmitGradingRequest>(), Arg.Any<CancellationToken>())
            .Returns(PracticalCaseTestData.Grading("Pending"));
        var sut = await LoadedAsync();
        sut.Parts[1].Answer = "  segunda  ";

        await sut.SubmitCommand.ExecuteAsync(null);

        await _api.Received(1).SubmitGradingAsync(
            PracticalCaseTestData.CaseId,
            Arg.Is<SubmitGradingRequest>(r => r.Answers.SequenceEqual(new[] { string.Empty, "  segunda  " })),
            Arg.Any<CancellationToken>());
        _drafts.Drafts.ShouldBeEmpty();
        await _navigation.Received(1).GoToAsync(
            "../grading",
            Arg.Is<IReadOnlyDictionary<string, object>>(q => (Guid)q[QueryKeys.GradingId] == PracticalCaseTestData.GradingId));

        // Leaving the page after sending must not bring the draft back.
        sut.OnDisappearing();
        _drafts.Drafts.ShouldBeEmpty();
    }

    [Fact]
    public async Task SubmitCommand_AlreadyPendingWithRunningGrading_OffersToOpenIt()
    {
        _api.SubmitGradingAsync(default, default!, Arg.Any<CancellationToken>()).ReturnsForAnyArgs(
            Task.FromException<GradingResponse>(await PracticalCaseTestData.ApiExceptionAsync(HttpStatusCode.Conflict, "grading.already_pending")));
        _api.GetGradingsAsync(0, 1, Arg.Any<CancellationToken>()).Returns([PracticalCaseTestData.SummaryGrading("Processing")]);
        var sut = await LoadedAsync();
        sut.Parts[0].Answer = "algo";

        await sut.SubmitCommand.ExecuteAsync(null);

        await _dialogs.Received(1).ConfirmAsync("Ya tienes una corrección en curso", "Espera a que termine para enviar otra.", "Ver estado", "Cerrar");
        await _navigation.Received(1).GoToAsync(
            Routes.Grading,
            Arg.Is<IReadOnlyDictionary<string, object>>(q => (Guid)q[QueryKeys.GradingId] == PracticalCaseTestData.GradingId));
        _drafts.Drafts.ShouldContainKey(PracticalCaseTestData.CaseId);
    }

    [Fact]
    public async Task SubmitCommand_AlreadyPendingWithoutRunningGrading_ShowsServerMessage()
    {
        _api.SubmitGradingAsync(default, default!, Arg.Any<CancellationToken>()).ReturnsForAnyArgs(
            Task.FromException<GradingResponse>(await PracticalCaseTestData.ApiExceptionAsync(HttpStatusCode.Conflict, "grading.already_pending", "Ya hay una")));
        _api.GetGradingsAsync(0, 1, Arg.Any<CancellationToken>()).Returns(Array.Empty<GradingSummaryResponse>());
        var sut = await LoadedAsync();
        sut.Parts[0].Answer = "algo";

        await sut.SubmitCommand.ExecuteAsync(null);

        sut.ErrorMessage.ShouldBe("Ya hay una");
        await _navigation.DidNotReceiveWithAnyArgs().GoToAsync(default!, default);
    }

    [Fact]
    public async Task SubmitCommand_QuotaExceeded_ShowsDetailAndReloadsQuota()
    {
        _api.SubmitGradingAsync(default, default!, Arg.Any<CancellationToken>()).ReturnsForAnyArgs(
            Task.FromException<GradingResponse>(await PracticalCaseTestData.ApiExceptionAsync(HttpStatusCode.UnprocessableEntity, "grading.quota_exceeded", "Sin cuota")));
        var sut = await LoadedAsync();
        sut.Parts[0].Answer = "algo";
        _api.GetGradingQuotaAsync(Arg.Any<CancellationToken>()).Returns(PracticalCaseTestData.Quota(0));

        await sut.SubmitCommand.ExecuteAsync(null);

        sut.ErrorMessage.ShouldBe("Sin cuota");
        sut.SubmitCommand.CanExecute(null).ShouldBeFalse();
        _drafts.Drafts.ShouldContainKey(PracticalCaseTestData.CaseId);
    }

    [Fact]
    public async Task SubmitCommand_QuotaExceededAndReloadFails_KeepsQuotaExhausted()
    {
        _api.SubmitGradingAsync(default, default!, Arg.Any<CancellationToken>()).ReturnsForAnyArgs(
            Task.FromException<GradingResponse>(await PracticalCaseTestData.ApiExceptionAsync(HttpStatusCode.UnprocessableEntity, "grading.quota_exceeded", "Sin cuota")));
        var sut = await LoadedAsync();
        sut.Parts[0].Answer = "algo";
        _api.GetGradingQuotaAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException<GradingQuotaResponse>(new HttpRequestException()));

        await sut.SubmitCommand.ExecuteAsync(null);

        sut.SubmitCommand.CanExecute(null).ShouldBeFalse();
        sut.QuotaMessage.ShouldBe("Has agotado tus 5 correcciones de este mes");
    }

    [Fact]
    public async Task OnAppearingAsync_CaseFailsAndQuotaCancelled_ClassifiesTheCaseError()
    {
        _api.GetPracticalCaseAsync(PracticalCaseTestData.CaseId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<PracticalCaseResponse>(new HttpRequestException()));
        _api.GetGradingQuotaAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<GradingQuotaResponse>(new OperationCanceledException()));
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.NeedsConnection.ShouldBeTrue();
        sut.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task SubmitCommand_NavigationFails_KeepsDraftAndKeepsSavingEdits()
    {
        _api.SubmitGradingAsync(PracticalCaseTestData.CaseId, Arg.Any<SubmitGradingRequest>(), Arg.Any<CancellationToken>())
            .Returns(PracticalCaseTestData.Grading("Pending"));
        _navigation.GoToAsync(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, object>?>())
            .Returns(Task.FromException(new InvalidOperationException("nav")));
        var sut = await LoadedAsync();
        sut.Parts[0].Answer = "uno";

        await sut.SubmitCommand.ExecuteAsync(null);

        _drafts.Drafts.ShouldContainKey(PracticalCaseTestData.CaseId);
        sut.Parts[0].Answer = "dos";
        sut.OnDisappearing();
        _drafts.Drafts[PracticalCaseTestData.CaseId].Answers[0].ShouldBe("dos");
    }

    [Fact]
    public async Task SubmitCommand_OtherUnprocessable_ShowsServerDetail()
    {
        _api.SubmitGradingAsync(default, default!, Arg.Any<CancellationToken>()).ReturnsForAnyArgs(
            Task.FromException<GradingResponse>(await PracticalCaseTestData.ApiExceptionAsync(HttpStatusCode.UnprocessableEntity, "grading.invalid_answer", "Respuesta demasiado larga")));
        var sut = await LoadedAsync();
        sut.Parts[0].Answer = "algo";

        await sut.SubmitCommand.ExecuteAsync(null);

        sut.ErrorMessage.ShouldBe("Respuesta demasiado larga");
        _drafts.Drafts.ShouldContainKey(PracticalCaseTestData.CaseId);
    }

    [Fact]
    public async Task SubmitCommand_NotFound_DisablesSubmission()
    {
        _api.SubmitGradingAsync(default, default!, Arg.Any<CancellationToken>()).ReturnsForAnyArgs(
            Task.FromException<GradingResponse>(await PracticalCaseTestData.ApiExceptionAsync(HttpStatusCode.NotFound, "practical_case.not_found", "No existe")));
        var sut = await LoadedAsync();
        sut.Parts[0].Answer = "algo";

        await sut.SubmitCommand.ExecuteAsync(null);

        sut.ErrorMessage.ShouldBe("No existe");
        sut.SubmitCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public async Task SubmitCommand_ConnectivityError_KeepsDraftAndShowsOfflineMessage()
    {
        _api.SubmitGradingAsync(default, default!, Arg.Any<CancellationToken>()).ThrowsAsyncForAnyArgs(new HttpRequestException());
        var sut = await LoadedAsync();
        sut.Parts[0].Answer = "algo";

        await sut.SubmitCommand.ExecuteAsync(null);

        sut.ErrorMessage.ShouldBe(PracticalCaseTexts.SubmitOffline);
        _drafts.Drafts[PracticalCaseTestData.CaseId].Answers.ShouldBe(["algo", ""]);
    }

    [Fact]
    public async Task Answer_NearLimit_ChangesCounter()
    {
        var sut = await LoadedAsync();

        sut.Parts[0].Answer = new string('a', 100);
        sut.Parts[0].CharacterCountText.ShouldBe("100 / 8000");
        sut.Parts[0].IsNearLimit.ShouldBeFalse();

        sut.Parts[0].Answer = new string('a', 7200);
        sut.Parts[0].IsNearLimit.ShouldBeTrue();
        sut.Parts[0].CharacterCountText.ShouldBe("7200 / 8000 · Te quedan 800");
    }
}