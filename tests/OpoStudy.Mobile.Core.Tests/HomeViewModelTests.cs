using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using OpoStudy.Contracts.Progress;
using OpoStudy.Contracts.SystemInfo;
using OpoStudy.Contracts.Users;
using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.Auth;
using OpoStudy.Mobile.Core.Navigation;
using OpoStudy.Mobile.Core.Services;
using OpoStudy.Mobile.Core.ViewModels;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests;

public sealed class HomeViewModelTests
{
    private readonly IOpoStudyApi _api = Substitute.For<IOpoStudyApi>();

    private readonly INavigationService _navigation = Substitute.For<INavigationService>();

    private readonly IAuthService _auth = Substitute.For<IAuthService>();

    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();

    public HomeViewModelTests()
    {
        _api.GetMeAsync(Arg.Any<CancellationToken>())
            .Returns(new UserProfileResponse(Guid.NewGuid(), new DateTimeOffset(2026, 3, 5, 10, 0, 0, TimeSpan.Zero), null, 10, "Europe/Madrid"));
        _auth.Email.Returns("a@b.com");
        _api.GetStreakAsync(Arg.Any<CancellationToken>()).Returns(StreakWith());
    }

    private static StreakResponse StreakWith(int goal = 10, int answered = 7, bool met = false, int current = 5, int longest = 12) =>
        new(goal, new DateOnly(2026, 11, 20), answered, met, current, longest, []);

    [Fact]
    public async Task RefreshCommand_TimeZoneDiffers_UpdatesThenLoadsStreak()
    {
        _time.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("Atlantic/Canary", TimeSpan.Zero, "x", "x"));
        _api.GetVersionAsync(Arg.Any<CancellationToken>()).Returns(new VersionResponse("0.1.0"));
        var sut = CreateSut();

        await sut.RefreshCommand.ExecuteAsync(null);

        await _api.Received(1).UpdateTimeZoneAsync(Arg.Is<UpdateTimeZoneRequest>(r => r.TimeZone == "Atlantic/Canary"), Arg.Any<CancellationToken>());
        await _api.Received(1).GetStreakAsync(Arg.Any<CancellationToken>());
        sut.HasStreak.ShouldBeTrue();
    }

    [Fact]
    public async Task RefreshCommand_TimeZoneMatches_DoesNotUpdate()
    {
        _time.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("Europe/Madrid", TimeSpan.FromHours(1), "x", "x"));
        _api.GetVersionAsync(Arg.Any<CancellationToken>()).Returns(new VersionResponse("0.1.0"));
        var sut = CreateSut();

        await sut.RefreshCommand.ExecuteAsync(null);

        await _api.DidNotReceive().UpdateTimeZoneAsync(Arg.Any<UpdateTimeZoneRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshCommand_TimeZoneUpdateFails_StillLoadsStreakWithoutError()
    {
        _time.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("Atlantic/Canary", TimeSpan.Zero, "x", "x"));
        _api.GetVersionAsync(Arg.Any<CancellationToken>()).Returns(new VersionResponse("0.1.0"));
        _api.UpdateTimeZoneAsync(Arg.Any<UpdateTimeZoneRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException());
        var sut = CreateSut();

        await sut.RefreshCommand.ExecuteAsync(null);

        sut.HasError.ShouldBeFalse();
        sut.HasStreak.ShouldBeTrue();
    }

    [Fact]
    public async Task RefreshCommand_StreakLoadFails_KeepsScreenWithoutError()
    {
        _api.GetVersionAsync(Arg.Any<CancellationToken>()).Returns(new VersionResponse("0.1.0"));
        _api.GetStreakAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException());
        var sut = CreateSut();

        await sut.RefreshCommand.ExecuteAsync(null);

        sut.HasError.ShouldBeFalse();
        sut.IsConnected.ShouldBeTrue();
        sut.HasStreak.ShouldBeFalse();
    }

    [Fact]
    public async Task OnAppearingAsync_AlreadyLoaded_ReloadsOnlyStreak()
    {
        _api.GetVersionAsync(Arg.Any<CancellationToken>()).Returns(new VersionResponse("0.1.0"));
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await sut.OnAppearingAsync();

        await _api.Received(1).GetVersionAsync(Arg.Any<CancellationToken>());
        await _api.Received(1).GetMeAsync(Arg.Any<CancellationToken>());
        await _api.Received(2).GetStreakAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshCommand_StreakLoaded_FillsCardTexts()
    {
        _api.GetVersionAsync(Arg.Any<CancellationToken>()).Returns(new VersionResponse("0.1.0"));
        var sut = CreateSut();

        await sut.RefreshCommand.ExecuteAsync(null);

        sut.GoalProgressText.ShouldBe("7 de 10 preguntas");
        sut.GoalProgress.ShouldBe(0.7);
        sut.StreakText.ShouldBe("🔥 5 días");
        sut.LongestStreakText.ShouldBe("Mejor racha: 12");
        sut.KeepStreakText.ShouldBe("Responde 3 preguntas más para mantener tu racha");
    }

    [Fact]
    public async Task ChangeDailyGoalCommand_ChoiceMade_UpdatesGoalAndRefreshesCard()
    {
        _dialogs.ChooseAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>()).Returns("20");
        _api.UpdateDailyGoalAsync(Arg.Is<UpdateDailyGoalRequest>(r => r.DailyGoal == 20), Arg.Any<CancellationToken>())
            .Returns(StreakWith(goal: 20, answered: 7));
        var sut = CreateSut();

        await sut.ChangeDailyGoalCommand.ExecuteAsync(null);

        sut.GoalProgressText.ShouldBe("7 de 20 preguntas");
        await _dialogs.Received(1).ChooseAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Is<IReadOnlyList<string>>(o => o.Count == 4 && o[0] == "10" && o[1] == "20" && o[2] == "30" && o[3] == "50"));
    }

    [Fact]
    public async Task ChangeDailyGoalCommand_Dismissed_DoesNotCallApi()
    {
        _dialogs.ChooseAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>()).Returns((string?)null);
        var sut = CreateSut();

        await sut.ChangeDailyGoalCommand.ExecuteAsync(null);

        await _api.DidNotReceive().UpdateDailyGoalAsync(Arg.Any<UpdateDailyGoalRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeDailyGoalCommand_ApiFails_ShowsError()
    {
        _dialogs.ChooseAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>()).Returns("30");
        _api.UpdateDailyGoalAsync(Arg.Any<UpdateDailyGoalRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException());
        var sut = CreateSut();

        await sut.ChangeDailyGoalCommand.ExecuteAsync(null);

        sut.HasError.ShouldBeTrue();
        sut.IsBusy.ShouldBeFalse();
    }

    private readonly FakeTimeProvider _time = new();

    private HomeViewModel CreateSut(bool isDebug = false) =>
        new(_api, _navigation, new AppEnvironment(isDebug), _auth, _dialogs, _time, NullLogger<HomeViewModel>.Instance);

    [Fact]
    public async Task RefreshCommand_CreatedNearMidnightUtc_UsesLocalDate()
    {
        _time.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("test", TimeSpan.FromHours(2), "test", "test"));
        _api.GetVersionAsync(Arg.Any<CancellationToken>()).Returns(new VersionResponse("0.1.0"));
        _api.GetMeAsync(Arg.Any<CancellationToken>())
            .Returns(new UserProfileResponse(Guid.NewGuid(), new DateTimeOffset(2026, 3, 5, 23, 30, 0, TimeSpan.Zero), null, 10, "Europe/Madrid"));
        var sut = CreateSut();

        await sut.RefreshCommand.ExecuteAsync(null);

        sut.MemberSince.ShouldBe("6 de marzo de 2026");
    }

    [Fact]
    public async Task StartQuizCommand_Executed_NavigatesToExams()
    {
        var sut = CreateSut();

        await sut.StartQuizCommand.ExecuteAsync(null);

        await _navigation.Received(1).GoToAsync(Routes.Exams);
    }

    [Fact]
    public async Task SignOutCommand_WhileBusy_CannotExecute()
    {
        var gate = new TaskCompletionSource();
        _auth.SignOutAsync().Returns(gate.Task);
        var sut = CreateSut();

        var running = sut.SignOutCommand.ExecuteAsync(null);

        sut.SignOutCommand.CanExecute(null).ShouldBeFalse();

        gate.SetResult();
        await running;

        sut.SignOutCommand.CanExecute(null).ShouldBeTrue();
    }

    [Fact]
    public async Task SignOutCommand_NavigationFails_ShowsErrorAndResetsBusy()
    {
        _navigation.GoToAsync(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, object>?>())
            .ThrowsAsync(new InvalidOperationException());
        var sut = CreateSut();

        await sut.SignOutCommand.ExecuteAsync(null);

        sut.HasError.ShouldBeTrue();
        sut.IsBusy.ShouldBeFalse();
    }

    [Fact]
    public async Task RefreshCommand_ApiResponds_LoadsAccountCard()
    {
        _api.GetVersionAsync(Arg.Any<CancellationToken>()).Returns(new VersionResponse("0.1.0"));
        var sut = CreateSut();

        await sut.RefreshCommand.ExecuteAsync(null);

        sut.AccountEmail.ShouldBe("a@b.com");
        sut.MemberSince.ShouldBe("5 de marzo de 2026");
        sut.MemberSinceText.ShouldBe("Miembro desde 5 de marzo de 2026");
    }

    [Fact]
    public async Task SignOutCommand_Executed_SignsOutAndNavigatesToLogin()
    {
        await CreateSut().SignOutCommand.ExecuteAsync(null);

        await _auth.Received(1).SignOutAsync();
        await _navigation.Received(1).GoToAsync("//login");
    }

    [Fact]
    public async Task DeleteAccountCommand_Cancelled_CallsNothing()
    {
        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        await CreateSut().DeleteAccountCommand.ExecuteAsync(null);

        await _api.DidNotReceiveWithAnyArgs().DeleteMeAsync(TestContext.Current.CancellationToken);
        await _auth.DidNotReceiveWithAnyArgs().DeleteFirebaseAccountAsync(TestContext.Current.CancellationToken);
        await _navigation.DidNotReceiveWithAnyArgs().GoToAsync(default!);
    }

    [Fact]
    public async Task DeleteAccountCommand_Confirmed_DeletesApiThenFirebaseThenNavigates()
    {
        _dialogs.ConfirmAsync("¿Borrar tu cuenta?", "Se borrarán tu cuenta y todo tu progreso. No se puede deshacer.", "Borrar", "Cancelar").Returns(true);

        await CreateSut().DeleteAccountCommand.ExecuteAsync(null);

        Received.InOrder(() =>
        {
            _api.DeleteMeAsync(Arg.Any<CancellationToken>());
            _auth.DeleteFirebaseAccountAsync(Arg.Any<CancellationToken>());
            _navigation.GoToAsync("//login");
        });
    }

    [Fact]
    public async Task DeleteAccountCommand_ApiFails_DoesNotTouchFirebase()
    {
        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _api.DeleteMeAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException());
        var sut = CreateSut();

        await sut.DeleteAccountCommand.ExecuteAsync(null);

        await _auth.DidNotReceiveWithAnyArgs().DeleteFirebaseAccountAsync(TestContext.Current.CancellationToken);
        sut.ErrorMessage.ShouldBe(ApiErrorMessages.Unreachable);
        sut.IsBusy.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAccountCommand_CredentialTooOld_NavigatesToLoginAndAlerts()
    {
        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _auth.DeleteFirebaseAccountAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new FirebaseAuthException("CREDENTIAL_TOO_OLD_LOGIN_AGAIN"));

        await CreateSut().DeleteAccountCommand.ExecuteAsync(null);

        await _navigation.Received(1).GoToAsync("//login");
        await _dialogs.Received(1).AlertAsync(Arg.Any<string>(), AuthErrorMessages.CredentialTooOld);
    }

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