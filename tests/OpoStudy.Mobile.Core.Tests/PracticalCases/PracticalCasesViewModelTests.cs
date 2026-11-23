using System.Net;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using OpoStudy.Contracts.Gradings;
using OpoStudy.Contracts.PracticalCases;
using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.Navigation;
using OpoStudy.Mobile.Core.PracticalCases;
using OpoStudy.Mobile.Core.ViewModels;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests.PracticalCases;

public sealed class PracticalCasesViewModelTests
{
    private readonly IOpoStudyApi _api = Substitute.For<IOpoStudyApi>();

    private readonly INavigationService _navigation = Substitute.For<INavigationService>();

    private readonly FakeDraftStore _drafts = new();

    private readonly OfflineDoubles _offline = new();

    private readonly Guid _examId = Guid.NewGuid();

    public PracticalCasesViewModelTests()
    {
        _api.GetGradingQuotaAsync(Arg.Any<CancellationToken>()).Returns(PracticalCaseTestData.Quota());
        _api.GetGradingsAsync(0, 1, Arg.Any<CancellationToken>()).Returns(Array.Empty<GradingSummaryResponse>());
        _api.GetPracticalCasesAsync(_examId, Arg.Any<CancellationToken>()).Returns(Array.Empty<PracticalCaseSummaryResponse>());
    }

    private PracticalCasesViewModel CreateSut()
    {
        var sut = new PracticalCasesViewModel(_api, _navigation, _drafts, _offline.State, NullLogger<PracticalCasesViewModel>.Instance);
        sut.ApplyQuery(new Dictionary<string, object> { [QueryKeys.ExamId] = _examId, [QueryKeys.ExamName] = "Programador C1" });
        return sut;
    }

    private void GivenCases(params PracticalCaseSummaryResponse[] cases) =>
        _api.GetPracticalCasesAsync(_examId, Arg.Any<CancellationToken>()).Returns(cases);

    [Fact]
    public async Task OnAppearingAsync_CasesWithSections_GroupsByFirstAppearanceAndNullLast()
    {
        GivenCases(
            PracticalCaseTestData.Summary("S1", null),
            PracticalCaseTestData.Summary("S2", "Bloque B"),
            PracticalCaseTestData.Summary("S3", "Bloque A"),
            PracticalCaseTestData.Summary("S4", "Bloque B"));
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.Groups.Select(g => g.Title).ShouldBe(["Bloque B", "Bloque A", "Otros supuestos"]);
        sut.Groups[0].Select(i => i.Title).ShouldBe(["S2", "S4"]);
        sut.Groups[2].Select(i => i.Title).ShouldBe(["S1"]);
        sut.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public async Task OnAppearingAsync_NoCases_ShowsEmptyState()
    {
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.IsEmpty.ShouldBeTrue();
        sut.Groups.ShouldBeEmpty();
    }

    [Fact]
    public async Task OnAppearingAsync_Quota_ShowsTexts()
    {
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.HasQuota.ShouldBeTrue();
        sut.QuotaText.ShouldBe("Te quedan 3 de 5 correcciones este mes");
        sut.QuotaResetText.ShouldBe("Se renuevan el 1 de diciembre");
        sut.IsQuotaExhausted.ShouldBeFalse();
    }

    [Fact]
    public async Task OnAppearingAsync_ExhaustedQuota_FlagsIt()
    {
        _api.GetGradingQuotaAsync(Arg.Any<CancellationToken>()).Returns(PracticalCaseTestData.Quota(0));
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.IsQuotaExhausted.ShouldBeTrue();
        sut.QuotaText.ShouldBe("Has agotado tus 5 correcciones de este mes");
    }

    [Fact]
    public async Task OnAppearingAsync_QuotaFails_StillShowsTheList()
    {
        _api.GetGradingQuotaAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException());
        GivenCases(PracticalCaseTestData.Summary("S1", "A"));
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.HasQuota.ShouldBeFalse();
        sut.HasError.ShouldBeFalse();
        sut.Groups.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("Pending", true)]
    [InlineData("Processing", true)]
    [InlineData("Completed", false)]
    [InlineData("Failed", false)]
    public async Task OnAppearingAsync_LatestGrading_ShowsCardOnlyWhenInProgress(string status, bool expected)
    {
        _api.GetGradingsAsync(0, 1, Arg.Any<CancellationToken>()).Returns([PracticalCaseTestData.SummaryGrading(status)]);
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.HasPendingGrading.ShouldBe(expected);
        if (expected)
        {
            sut.PendingGradingText.ShouldBe("Corrección en curso: Supuesto 1");
        }
    }

    [Fact]
    public async Task OpenPendingGradingCommand_InProgress_NavigatesToTheGrading()
    {
        _api.GetGradingsAsync(0, 1, Arg.Any<CancellationToken>()).Returns([PracticalCaseTestData.SummaryGrading("Pending")]);
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await sut.OpenPendingGradingCommand.ExecuteAsync(null);

        await _navigation.Received(1).GoToAsync(
            Routes.Grading,
            Arg.Is<IReadOnlyDictionary<string, object>>(q => (Guid)q[QueryKeys.GradingId] == PracticalCaseTestData.GradingId));
    }

    [Fact]
    public async Task OnAppearingAsync_GradingsFail_OmitsTheCard()
    {
        _api.GetGradingsAsync(0, 1, Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException());
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.HasPendingGrading.ShouldBeFalse();
        sut.HasError.ShouldBeFalse();
    }

    [Fact]
    public async Task OnAppearingAsync_Offline_DoesNotCallTheApi()
    {
        _offline.Network.HasInternet.Returns(false);
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.NeedsConnection.ShouldBeTrue();
        await _api.DidNotReceiveWithAnyArgs().GetPracticalCasesAsync(default, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshCommand_ConnectivityError_ShowsNeedsConnection()
    {
        _api.GetPracticalCasesAsync(_examId, Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException());
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.NeedsConnection.ShouldBeTrue();
        sut.HasError.ShouldBeFalse();
    }

    [Fact]
    public async Task RefreshCommand_ServerError_ShowsErrorMessage()
    {
        _api.GetPracticalCasesAsync(_examId, Arg.Any<CancellationToken>())
            .ThrowsAsync(await PracticalCaseTestData.ApiExceptionAsync(HttpStatusCode.InternalServerError));
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.NeedsConnection.ShouldBeFalse();
        sut.ErrorMessage.ShouldBe(ApiErrorMessages.ServerError);
    }

    [Fact]
    public async Task OnAppearingAsync_CaseWithDraft_MarksIt()
    {
        var withDraft = PracticalCaseTestData.Summary("S1", "A");
        GivenCases(withDraft, PracticalCaseTestData.Summary("S2", "A"));
        _drafts.Drafts[withDraft.Id] = new PracticalCaseDraft(["x"], DateTimeOffset.UtcNow);
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.Groups[0][0].HasDraft.ShouldBeTrue();
        sut.Groups[0][1].HasDraft.ShouldBeFalse();
    }

    [Fact]
    public async Task OpenCaseCommand_Item_NavigatesWithTheCaseId()
    {
        var summary = PracticalCaseTestData.Summary("S1", "A");
        GivenCases(summary);
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await sut.OpenCaseCommand.ExecuteAsync(sut.Groups[0][0]);

        await _navigation.Received(1).GoToAsync(
            Routes.PracticalCase,
            Arg.Is<IReadOnlyDictionary<string, object>>(q => (Guid)q[QueryKeys.PracticalCaseId] == summary.Id));
    }
}