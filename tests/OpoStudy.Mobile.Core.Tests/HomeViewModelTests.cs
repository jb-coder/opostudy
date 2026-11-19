using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using OpoStudy.Contracts.SystemInfo;
using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.Navigation;
using OpoStudy.Mobile.Core.ViewModels;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests;

public sealed class HomeViewModelTests
{
    private readonly IOpoStudyApi _api = Substitute.For<IOpoStudyApi>();

    private readonly INavigationService _navigation = Substitute.For<INavigationService>();

    private HomeViewModel CreateSut(bool isDebug = false) =>
        new(_api, _navigation, new AppEnvironment(isDebug), NullLogger<HomeViewModel>.Instance);

    [Fact]
    public async Task RefreshCommand_ApiResponds_SetsVersionAndConnected()
    {
        _api.GetVersionAsync(Arg.Any<CancellationToken>()).Returns(new VersionResponse("0.1.0"));
        var sut = CreateSut();

        await sut.RefreshCommand.ExecuteAsync(null);

        sut.ApiVersion.ShouldBe("0.1.0");
        sut.IsConnected.ShouldBeTrue();
        sut.HasError.ShouldBeFalse();
        sut.ConnectionText.ShouldBe("Conectado · API 0.1.0");
    }

    [Fact]
    public async Task ConnectionText_VersionWithBuildMetadata_OmitsMetadata()
    {
        _api.GetVersionAsync(Arg.Any<CancellationToken>()).Returns(new VersionResponse("1.0.0+70845da50d8c1234"));
        var sut = CreateSut();

        await sut.RefreshCommand.ExecuteAsync(null);

        sut.ConnectionText.ShouldBe("Conectado · API 1.0.0");
    }

    [Fact]
    public async Task RefreshCommand_ApiUnreachable_SetsErrorAndNotConnected()
    {
        _api.GetVersionAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException());
        var sut = CreateSut();

        await sut.RefreshCommand.ExecuteAsync(null);

        sut.IsConnected.ShouldBeFalse();
        sut.HasError.ShouldBeTrue();
        sut.ErrorMessage.ShouldBe(ApiErrorMessages.Unreachable);
    }

    [Fact]
    public async Task RefreshCommand_AfterError_ClearsPreviousError()
    {
        _api.GetVersionAsync(Arg.Any<CancellationToken>())
            .Returns(
                _ => Task.FromException<VersionResponse>(new HttpRequestException()),
                _ => Task.FromResult(new VersionResponse("0.1.0")));
        var sut = CreateSut();

        await sut.RefreshCommand.ExecuteAsync(null);
        sut.HasError.ShouldBeTrue();

        await sut.RefreshCommand.ExecuteAsync(null);

        sut.HasError.ShouldBeFalse();
        sut.ErrorMessage.ShouldBeNull();
        sut.IsConnected.ShouldBeTrue();
    }

    [Fact]
    public async Task RefreshCommand_WhileBusy_CannotExecute()
    {
        var gate = new TaskCompletionSource<VersionResponse>();
        _api.GetVersionAsync(Arg.Any<CancellationToken>()).Returns(gate.Task);
        var sut = CreateSut();

        var running = sut.RefreshCommand.ExecuteAsync(null);

        sut.RefreshCommand.CanExecute(null).ShouldBeFalse();

        gate.SetResult(new VersionResponse("0.1.0"));
        await running;

        sut.RefreshCommand.CanExecute(null).ShouldBeTrue();
    }

    [Fact]
    public async Task OnAppearingAsync_CalledTwice_LoadsOnce()
    {
        _api.GetVersionAsync(Arg.Any<CancellationToken>()).Returns(new VersionResponse("0.1.0"));
        var sut = CreateSut();

        await sut.OnAppearingAsync();
        await sut.OnAppearingAsync();

        await _api.Received(1).GetVersionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnAppearingAsync_AfterError_RetriesLoad()
    {
        _api.GetVersionAsync(Arg.Any<CancellationToken>())
            .Returns(
                _ => Task.FromException<VersionResponse>(new HttpRequestException()),
                _ => Task.FromResult(new VersionResponse("0.1.0")));
        var sut = CreateSut();

        await sut.OnAppearingAsync();
        sut.HasError.ShouldBeTrue();

        await sut.OnAppearingAsync();

        sut.HasError.ShouldBeFalse();
        sut.ApiVersion.ShouldBe("0.1.0");
        await _api.Received(2).GetVersionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnAppearingAsync_WhileRefreshing_DoesNotStartSecondRequest()
    {
        var gate = new TaskCompletionSource<VersionResponse>();
        _api.GetVersionAsync(Arg.Any<CancellationToken>()).Returns(gate.Task);
        var sut = CreateSut();

        var first = sut.OnAppearingAsync();
        await sut.OnAppearingAsync();

        gate.SetResult(new VersionResponse("0.1.0"));
        await first;

        await _api.Received(1).GetVersionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OpenStyleGuideCommand_Executed_NavigatesToStyleGuide()
    {
        var sut = CreateSut(isDebug: true);

        await sut.OpenStyleGuideCommand.ExecuteAsync(null);

        await _navigation.Received(1).GoToAsync(Routes.StyleGuide);
    }

    [Fact]
    public async Task IsBusy_DuringRequest_IsTrueAndResetsAfter()
    {
        var gate = new TaskCompletionSource<VersionResponse>();
        _api.GetVersionAsync(Arg.Any<CancellationToken>()).Returns(gate.Task);
        var sut = CreateSut();

        var running = sut.RefreshCommand.ExecuteAsync(null);
        sut.IsBusy.ShouldBeTrue();

        gate.SetResult(new VersionResponse("0.1.0"));
        await running;

        sut.IsBusy.ShouldBeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsStyleGuideVisible_Environment_FollowsIsDebug(bool isDebug) =>
        CreateSut(isDebug).IsStyleGuideVisible.ShouldBe(isDebug);
}