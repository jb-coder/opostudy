using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using OpoStudy.Mobile.Core.Auth;
using OpoStudy.Mobile.Core.Navigation;
using OpoStudy.Mobile.Core.Reminders;
using OpoStudy.Mobile.Core.Services;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests.Auth;

public sealed class SessionExpiredHandlerTests
{
    private readonly IAuthService _auth = Substitute.For<IAuthService>();
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly FakeReminderStore _reminderStore = new();
    private readonly IReminderScheduler _scheduler = ReminderTestSupport.SupportedScheduler();

    private SessionExpiredHandler CreateSut() =>
        new(
            _auth,
            _navigation,
            _dialogs,
            new ReminderService(_reminderStore, _scheduler, new FakeTimeProvider()),
            NullLogger<SessionExpiredHandler>.Instance);

    [Fact]
    public async Task HandleAsync_SessionExpired_CancelsReminderAndSuspendsIt()
    {
        _reminderStore.Settings = new ReminderSettings(true, new TimeOnly(20, 0));
        _reminderStore.Snapshot = new ReminderSnapshot(new DateOnly(2026, 11, 20), 3, 10, false, 2);

        await CreateSut().HandleAsync("TOKEN_EXPIRED");

        _scheduler.Received(1).Cancel();
        _reminderStore.Snapshot.ShouldBeNull();
        _reminderStore.Suspended.ShouldBeTrue();
        _reminderStore.Settings.Enabled.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAsync_UserDisabled_NavigatesToLoginThenAlerts()
    {
        await CreateSut().HandleAsync("USER_DISABLED");

        Received.InOrder(() =>
        {
            _navigation.GoToAsync("//login");
            _dialogs.AlertAsync("Sesión cerrada", AuthErrorMessages.From("USER_DISABLED"));
        });
    }

    [Fact]
    public async Task HandleAsync_NavigationFails_DoesNotThrow()
    {
        _navigation.GoToAsync(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, object>?>())
            .ThrowsAsync(new InvalidOperationException());

        await Should.NotThrowAsync(() => CreateSut().HandleAsync("USER_DISABLED"));
    }

    [Fact]
    public async Task Start_SessionExpiredRaised_ShowsNotice()
    {
        CreateSut().Start();

        _auth.SessionExpired += Raise.EventWith(new SessionExpiredEventArgs("USER_DISABLED"));

        await _dialogs.Received(1).AlertAsync("Sesión cerrada", AuthErrorMessages.From("USER_DISABLED"));
    }
}