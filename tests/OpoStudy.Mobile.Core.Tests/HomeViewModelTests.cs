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
using OpoStudy.Mobile.Core.Reminders;
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
        _api.GetLevelAsync(Arg.Any<CancellationToken>()).Returns(new LevelResponse(4, 1030, 230, 400, 120, 1030));
    }

    private static StreakResponse StreakWith(int goal = 10, int answered = 7, bool met = false, int current = 5, int longest = 12) =>
        new(goal, new DateOnly(2026, 11, 20), answered, met, current, longest, [], 1, 2, 200, 0);

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
        await _api.Received(2).GetLevelAsync(Arg.Any<CancellationToken>());
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
    public async Task OnAppearingAsync_StaleStreakArrivesAfterGoalChange_KeepsGoalChange()
    {
        _api.GetVersionAsync(Arg.Any<CancellationToken>()).Returns(new VersionResponse("0.1.0"));
        var sut = CreateSut();
        await sut.OnAppearingAsync();
        var pending = new TaskCompletionSource<StreakResponse>();
        _api.GetStreakAsync(Arg.Any<CancellationToken>()).Returns(pending.Task);
        _dialogs.ChooseAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>()).Returns("20");
        _api.UpdateDailyGoalAsync(Arg.Any<UpdateDailyGoalRequest>(), Arg.Any<CancellationToken>())
            .Returns(StreakWith(goal: 20, answered: 7));

        var reload = sut.OnAppearingAsync();
        await sut.ChangeDailyGoalCommand.ExecuteAsync(null);
        pending.SetResult(StreakWith(goal: 10, answered: 7));
        await reload;

        sut.GoalProgressText.ShouldBe("7 de 20 preguntas");
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

    private async Task<HomeViewModel> LoadedSutAsync()
    {
        _api.GetVersionAsync(Arg.Any<CancellationToken>()).Returns(new VersionResponse("0.1.0"));
        var sut = CreateSut();
        await sut.RefreshCommand.ExecuteAsync(null);
        return sut;
    }

    [Fact]
    public async Task RefreshCommand_StreakAndLevelLoaded_FillsFreezeTexts()
    {
        var sut = await LoadedSutAsync();

        sut.FreezesText.ShouldBe("🛡️ Protectores: 1 de 2");
        sut.BuyFreezeText.ShouldBe("Comprar protector · 200 XP");
        sut.BuyStreakFreezeCommand.CanExecute(null).ShouldBeTrue();
        sut.HasSpendableXpText.ShouldBeFalse();
    }

    [Fact]
    public async Task BuyStreakFreezeCommand_ConfirmationCancelled_DoesNotCallApi()
    {
        var sut = await LoadedSutAsync();
        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        await sut.BuyStreakFreezeCommand.ExecuteAsync(null);

        await _api.DidNotReceive().BuyStreakFreezeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BuyStreakFreezeCommand_Confirmed_RefreshesStreakAndLevel()
    {
        var sut = await LoadedSutAsync();
        _dialogs.ConfirmAsync(Arg.Any<string>(), "¿Comprar un protector de racha por 200 XP? Se gastará solo si un día no cumples el objetivo.", Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _api.BuyStreakFreezeAsync(Arg.Any<CancellationToken>()).Returns(StreakWith() with { StreakFreezes = 2 });
        _api.GetLevelAsync(Arg.Any<CancellationToken>()).Returns(new LevelResponse(4, 1030, 230, 400, 120, 830));

        await sut.BuyStreakFreezeCommand.ExecuteAsync(null);

        sut.FreezesText.ShouldBe("🛡️ Protectores: 2 de 2");
        sut.BuyFreezeText.ShouldBe("Tienes el máximo de protectores");
        sut.BuyStreakFreezeCommand.CanExecute(null).ShouldBeFalse();
        sut.SpendableXpText.ShouldBe("Disponible: 830 XP");
        sut.HasSpendableXpText.ShouldBeTrue();
        sut.HasError.ShouldBeFalse();
    }

    [Fact]
    public async Task BuyStreakFreezeCommand_Rejected_ShowsProblemMessage()
    {
        var sut = await LoadedSutAsync();
        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _api.BuyStreakFreezeAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(await ProblemAsync("""{"title":"Ya tienes el máximo de 2 protectores.","code":"streak.freezes_full"}"""));

        await sut.BuyStreakFreezeCommand.ExecuteAsync(null);

        sut.BuyFreezeErrorMessage.ShouldBe("Ya tienes el máximo de 2 protectores.");
        sut.HasBuyFreezeError.ShouldBeTrue();
        sut.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task BuyStreakFreezeCommand_AfterRejection_SuccessfulPurchaseClearsError()
    {
        var sut = await LoadedSutAsync();
        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _api.BuyStreakFreezeAsync(Arg.Any<CancellationToken>())
            .Returns(
                _ => Task.FromException<StreakResponse>(ProblemAsync("""{"title":"Error.","code":"xp.insufficient"}""").GetAwaiter().GetResult()),
                _ => Task.FromResult(StreakWith()));
        await sut.BuyStreakFreezeCommand.ExecuteAsync(null);
        sut.HasBuyFreezeError.ShouldBeTrue();

        await sut.BuyStreakFreezeCommand.ExecuteAsync(null);

        sut.BuyFreezeErrorMessage.ShouldBeNull();
        sut.HasBuyFreezeError.ShouldBeFalse();
    }

    [Fact]
    public async Task RefreshCommand_AfterFailedPurchase_ClearsPurchaseError()
    {
        var sut = await LoadedSutAsync();
        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _api.BuyStreakFreezeAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(await ProblemAsync("""{"title":"Error.","code":"xp.insufficient"}"""));
        await sut.BuyStreakFreezeCommand.ExecuteAsync(null);

        await sut.RefreshCommand.ExecuteAsync(null);

        sut.BuyFreezeErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task RefreshCommand_FreezesUsedNow_ShowsNotice()
    {
        _api.GetStreakAsync(Arg.Any<CancellationToken>()).Returns(StreakWith() with { FreezesUsedNow = 2 });

        var sut = await LoadedSutAsync();

        sut.HasFreezesNotice.ShouldBeTrue();
        sut.FreezesNotice.ShouldBe("🛡️ 2 protectores han salvado tu racha");
    }

    [Fact]
    public async Task BuyStreakFreezeCommand_NotEnoughSpendableXp_CannotExecute()
    {
        _api.GetLevelAsync(Arg.Any<CancellationToken>()).Returns(new LevelResponse(1, 150, 50, 100, 0, 150));

        var sut = await LoadedSutAsync();

        sut.BuyFreezeText.ShouldBe("Necesitas 200 XP disponibles");
        sut.BuyStreakFreezeCommand.CanExecute(null).ShouldBeFalse();
    }

    private static async Task<Refit.ApiException> ProblemAsync(string json)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/api/v1/me/streak-freezes");
        using var response = new HttpResponseMessage(System.Net.HttpStatusCode.UnprocessableEntity)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/problem+json"),
        };
        return await Refit.ApiException.Create(request, HttpMethod.Post, response, new Refit.RefitSettings());
    }

    [Fact]
    public async Task RefreshCommand_LevelLoaded_FillsCardTexts()
    {
        _api.GetVersionAsync(Arg.Any<CancellationToken>()).Returns(new VersionResponse("0.1.0"));
        var sut = CreateSut();

        await sut.RefreshCommand.ExecuteAsync(null);

        sut.HasLevel.ShouldBeTrue();
        sut.LevelTitleText.ShouldBe("Nivel 4");
        sut.LevelDetailText.ShouldBe("230 / 400 XP para el nivel 5");
        sut.XpTodayText.ShouldBe("Hoy: +120 XP");
        sut.LevelProgress.ShouldBe(0.575);
    }

    [Fact]
    public async Task RefreshCommand_LevelFails_ShowsErrorOnlyOnLevelCard()
    {
        _api.GetVersionAsync(Arg.Any<CancellationToken>()).Returns(new VersionResponse("0.1.0"));
        _api.GetLevelAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException());
        var sut = CreateSut();

        await sut.RefreshCommand.ExecuteAsync(null);

        sut.HasLevelError.ShouldBeTrue();
        sut.HasLevel.ShouldBeFalse();
        sut.HasStreak.ShouldBeTrue();
        sut.HasError.ShouldBeFalse();
    }

    [Fact]
    public async Task ChangeDailyGoalCommand_ChoiceMade_ReloadsLevel()
    {
        _dialogs.ChooseAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>()).Returns("20");
        _api.UpdateDailyGoalAsync(Arg.Any<UpdateDailyGoalRequest>(), Arg.Any<CancellationToken>()).Returns(StreakWith(goal: 20));
        var sut = CreateSut();

        await sut.ChangeDailyGoalCommand.ExecuteAsync(null);

        await _api.Received(1).GetLevelAsync(Arg.Any<CancellationToken>());
        sut.XpTodayText.ShouldBe("Hoy: +120 XP");
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

    private static readonly string[] AllowedTimeLabels = ["18:00", "19:00", "20:00", "21:00", "22:00"];

    private readonly FakeReminderStore _reminderStore = new();

    private readonly IReminderScheduler _scheduler = ReminderTestSupport.SupportedScheduler();

    private HomeViewModel CreateSut(bool isDebug = false) =>
        new(_api, _navigation, new AppEnvironment(isDebug), _auth, _dialogs, _time, new ReminderService(_reminderStore, _scheduler, _time), NullLogger<HomeViewModel>.Instance);

    [Fact]
    public async Task OnAppearingAsync_ReminderSaved_ShowsSettings()
    {
        _api.GetVersionAsync(Arg.Any<CancellationToken>()).Returns(new VersionResponse("0.1.0"));
        _reminderStore.Settings = new ReminderSettings(true, new TimeOnly(21, 0));
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.IsReminderSupported.ShouldBeTrue();
        sut.ReminderEnabled.ShouldBeTrue();
        sut.ReminderTimeText.ShouldBe("Todos los días a las 21:00 si no has cumplido el objetivo");
        await _scheduler.DidNotReceive().RequestPermissionAsync();
    }

    [Fact]
    public async Task ReminderEnabled_SwitchedOnWithPermission_EnablesAndSchedules()
    {
        _api.GetVersionAsync(Arg.Any<CancellationToken>()).Returns(new VersionResponse("0.1.0"));
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        sut.ReminderEnabled = true;

        _reminderStore.Settings.Enabled.ShouldBeTrue();
        _scheduler.Received().Schedule(Arg.Any<DateTimeOffset>());
        sut.HasReminderNotice.ShouldBeFalse();
    }

    [Fact]
    public async Task ReminderEnabled_SwitchedOnWithPermissionDenied_TurnsBackOffAndExplains()
    {
        _api.GetVersionAsync(Arg.Any<CancellationToken>()).Returns(new VersionResponse("0.1.0"));
        _scheduler.RequestPermissionAsync().Returns(false);
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        sut.ReminderEnabled = true;

        sut.ReminderEnabled.ShouldBeFalse();
        _reminderStore.Settings.Enabled.ShouldBeFalse();
        sut.ReminderNotice.ShouldBe("Activa las notificaciones de OpoStudy en los ajustes de Android para recibir el recordatorio.");
        sut.HasReminderNotice.ShouldBeTrue();
    }

    [Fact]
    public async Task ReminderEnabled_SwitchedOff_DisablesAndCancels()
    {
        _api.GetVersionAsync(Arg.Any<CancellationToken>()).Returns(new VersionResponse("0.1.0"));
        _reminderStore.Settings = new ReminderSettings(true, ReminderSettings.DefaultTime);
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        sut.ReminderEnabled = false;

        _reminderStore.Settings.Enabled.ShouldBeFalse();
        _scheduler.Received().Cancel();
    }

    [Fact]
    public async Task ChangeReminderTimeCommand_TimeChosen_SavesAndUpdatesText()
    {
        _api.GetVersionAsync(Arg.Any<CancellationToken>()).Returns(new VersionResponse("0.1.0"));
        _dialogs.ChooseAsync("Hora del recordatorio", "Cancelar", Arg.Is<IReadOnlyList<string>>(o => o.SequenceEqual(AllowedTimeLabels)))
            .Returns("22:00");
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await sut.ChangeReminderTimeCommand.ExecuteAsync(null);

        _reminderStore.Settings.Time.ShouldBe(new TimeOnly(22, 0));
        sut.ReminderTimeText.ShouldBe("Todos los días a las 22:00 si no has cumplido el objetivo");
    }

    [Fact]
    public async Task ChangeReminderTimeCommand_Dismissed_KeepsTime()
    {
        _api.GetVersionAsync(Arg.Any<CancellationToken>()).Returns(new VersionResponse("0.1.0"));
        _dialogs.ChooseAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>()).Returns((string?)null);
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        await sut.ChangeReminderTimeCommand.ExecuteAsync(null);

        _reminderStore.Settings.Time.ShouldBe(ReminderSettings.DefaultTime);
    }

    [Fact]
    public async Task RefreshCommand_StreakLoaded_SavesSnapshotAndEnsuresSchedule()
    {
        _api.GetVersionAsync(Arg.Any<CancellationToken>()).Returns(new VersionResponse("0.1.0"));
        _reminderStore.Settings = new ReminderSettings(true, ReminderSettings.DefaultTime);
        var sut = CreateSut();

        await sut.RefreshCommand.ExecuteAsync(null);

        _reminderStore.Snapshot.ShouldNotBeNull().AnsweredToday.ShouldBe(7);
        _scheduler.Received().Schedule(Arg.Any<DateTimeOffset>());
    }

    [Fact]
    public async Task RefreshCommand_StreakLoaded_LiftsReminderSuspension()
    {
        _api.GetVersionAsync(Arg.Any<CancellationToken>()).Returns(new VersionResponse("0.1.0"));
        _reminderStore.Suspended = true;
        var sut = CreateSut();

        await sut.RefreshCommand.ExecuteAsync(null);

        _reminderStore.Suspended.ShouldBeFalse();
    }

    [Fact]
    public async Task ToggleReminderCommand_PermissionPending_DisablesSwitchUntilDone()
    {
        _api.GetVersionAsync(Arg.Any<CancellationToken>()).Returns(new VersionResponse("0.1.0"));
        var permission = new TaskCompletionSource<bool>();
        _scheduler.RequestPermissionAsync().Returns(permission.Task);
        var sut = CreateSut();
        await sut.OnAppearingAsync();

        sut.ReminderEnabled = true;

        sut.IsReminderToggleEnabled.ShouldBeFalse();
        permission.SetResult(true);
        await sut.ToggleReminderCommand.ExecutionTask.ShouldNotBeNull();
        sut.IsReminderToggleEnabled.ShouldBeTrue();
        sut.ReminderEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task SignOutCommand_Executed_CancelsReminderAndClearsSnapshot()
    {
        _reminderStore.Snapshot = new ReminderSnapshot(new DateOnly(2026, 11, 20), 3, 10, false, 2);
        var sut = CreateSut();

        await sut.SignOutCommand.ExecuteAsync(null);

        _scheduler.Received(1).Cancel();
        _reminderStore.Snapshot.ShouldBeNull();
    }

    [Fact]
    public async Task SignOutCommand_SignOutFails_StillCancelsReminder()
    {
        _auth.SignOutAsync().ThrowsAsync(new HttpRequestException());
        var sut = CreateSut();

        await sut.SignOutCommand.ExecuteAsync(null);

        _scheduler.Received(1).Cancel();
        _reminderStore.Snapshot.ShouldBeNull();
    }

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