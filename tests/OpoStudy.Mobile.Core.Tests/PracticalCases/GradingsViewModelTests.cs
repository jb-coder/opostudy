using System.Net;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using OpoStudy.Contracts.Gradings;
using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.Navigation;
using OpoStudy.Mobile.Core.PracticalCases;
using OpoStudy.Mobile.Core.ViewModels;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests.PracticalCases;

public sealed class GradingsViewModelTests
{
    private readonly IOpoStudyApi _api = Substitute.For<IOpoStudyApi>();

    private readonly INavigationService _navigation = Substitute.For<INavigationService>();

    private readonly OfflineDoubles _offline = new();

    private GradingsViewModel CreateSut() =>
        new(_api, _navigation, _offline.State, NullLogger<GradingsViewModel>.Instance);

    private static GradingSummaryResponse Summary(string status, string title = "Supuesto", string? section = "Bloque A", decimal? score = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), title, section, status, new DateTimeOffset(2026, 11, 23, 9, 30, 0, TimeSpan.Zero), 25, score);

    private static GradingSummaryResponse[] Page(int count) =>
        [.. Enumerable.Range(0, count).Select(i => Summary("Completed", $"Supuesto {i}", score: 10m))];

    [Fact]
    public async Task OnAppearingAsync_LoadsTheFirstPageInOrder()
    {
        _api.GetGradingsAsync(0, 20, Arg.Any<CancellationToken>())
            .Returns([Summary("Completed", "Nuevo", score: 18.5m), Summary("Failed", "Viejo")]);
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.Items.Select(i => i.CaseTitle).ShouldBe(["Nuevo", "Viejo"]);
        sut.IsEmpty.ShouldBeFalse();
        sut.CanLoadMore.ShouldBeFalse();
    }

    [Fact]
    public void Item_StatusText_DependsOnTheStatus()
    {
        new GradingHistoryItem(Summary("Completed", score: 18.5m)).StatusText.ShouldBe("18,5 / 25 puntos");
        new GradingHistoryItem(Summary("Pending")).StatusText.ShouldBe("En curso");
        new GradingHistoryItem(Summary("Processing")).StatusText.ShouldBe("En curso");
        new GradingHistoryItem(Summary("Failed")).StatusText.ShouldBe("No corregida");
    }

    [Fact]
    public void Item_WithoutSection_HidesIt() =>
        new GradingHistoryItem(Summary("Completed", section: null)).HasSection.ShouldBeFalse();

    [Fact]
    public async Task OnAppearingAsync_NoGradings_ShowsEmptyState()
    {
        _api.GetGradingsAsync(0, 20, Arg.Any<CancellationToken>()).Returns(Array.Empty<GradingSummaryResponse>());
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.IsEmpty.ShouldBeTrue();
        sut.CanLoadMore.ShouldBeFalse();
    }

    [Fact]
    public async Task LoadMoreCommand_AfterAFullPage_AsksForTheNextSkipAndAppends()
    {
        _api.GetGradingsAsync(0, 20, Arg.Any<CancellationToken>()).Returns(Page(20));
        _api.GetGradingsAsync(20, 20, Arg.Any<CancellationToken>()).Returns(Page(5));
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        sut.CanLoadMore.ShouldBeTrue();

        await sut.LoadMoreCommand.ExecuteAsync(null);

        sut.Items.Count.ShouldBe(25);
        sut.CanLoadMore.ShouldBeFalse();
        await _api.Received(1).GetGradingsAsync(20, 20, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadMoreCommand_WithIdsAlreadyShown_SkipsTheDuplicates()
    {
        var first = Page(20);
        _api.GetGradingsAsync(0, 20, Arg.Any<CancellationToken>()).Returns(first);
        // A new grading shifted the offset: the last one of the first page comes again.
        _api.GetGradingsAsync(20, 20, Arg.Any<CancellationToken>()).Returns([first[19], Summary("Completed", "Otro", score: 1m)]);
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await sut.LoadMoreCommand.ExecuteAsync(null);

        sut.Items.Count.ShouldBe(21);
        sut.Items.Select(i => i.Id).Distinct().Count().ShouldBe(21);
        sut.CanLoadMore.ShouldBeFalse();
    }

    [Fact]
    public async Task LoadMoreCommand_WhenItFails_KeepsWhatWasLoadedAndShowsTheError()
    {
        _api.GetGradingsAsync(0, 20, Arg.Any<CancellationToken>()).Returns(Page(20));
        _api.GetGradingsAsync(20, 20, Arg.Any<CancellationToken>())
            .ThrowsAsync(await PracticalCaseTestData.ApiExceptionAsync(HttpStatusCode.InternalServerError));
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await sut.LoadMoreCommand.ExecuteAsync(null);

        sut.Items.Count.ShouldBe(20);
        sut.CanLoadMore.ShouldBeTrue();
        sut.ErrorMessage.ShouldBe(ApiErrorMessages.ServerError);
        sut.NeedsConnection.ShouldBeFalse();
    }

    [Fact]
    public async Task OnAppearingAsync_Offline_DoesNotCallTheApi()
    {
        _offline.Network.HasInternet.Returns(false);
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.NeedsConnection.ShouldBeTrue();
        await _api.DidNotReceiveWithAnyArgs().GetGradingsAsync(default, default, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshCommand_ConnectivityError_ShowsNeedsConnection()
    {
        _api.GetGradingsAsync(0, 20, Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException());
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.NeedsConnection.ShouldBeTrue();
        sut.HasError.ShouldBeFalse();
    }

    [Fact]
    public async Task RefreshCommand_ServerError_ShowsErrorMessage()
    {
        _api.GetGradingsAsync(0, 20, Arg.Any<CancellationToken>())
            .ThrowsAsync(await PracticalCaseTestData.ApiExceptionAsync(HttpStatusCode.InternalServerError));
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.ErrorMessage.ShouldBe(ApiErrorMessages.ServerError);
        sut.NeedsConnection.ShouldBeFalse();
        sut.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public async Task OnAppearingAsync_Twice_ReloadsTheFirstPageWithoutDuplicates()
    {
        _api.GetGradingsAsync(0, 20, Arg.Any<CancellationToken>()).Returns(Page(3));
        var sut = CreateSut();

        await sut.OnAppearingAsync();
        await sut.OnAppearingAsync();

        sut.Items.Count.ShouldBe(3);
    }

    [Fact]
    public async Task OpenGradingCommand_NavigatesWithTheGradingId()
    {
        var summary = Summary("Completed", score: 5m);
        _api.GetGradingsAsync(0, 20, Arg.Any<CancellationToken>()).Returns([summary]);
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await sut.OpenGradingCommand.ExecuteAsync(sut.Items[0]);

        await _navigation.Received(1).GoToAsync(
            Routes.Grading,
            Arg.Is<IReadOnlyDictionary<string, object>>(p => (Guid)p[QueryKeys.GradingId] == summary.Id));
    }
}