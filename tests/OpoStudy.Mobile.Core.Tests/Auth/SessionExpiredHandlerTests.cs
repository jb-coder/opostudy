using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using OpoStudy.Mobile.Core.Auth;
using OpoStudy.Mobile.Core.Navigation;
using OpoStudy.Mobile.Core.Services;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests.Auth;

public sealed class SessionExpiredHandlerTests
{
    private readonly IAuthService _auth = Substitute.For<IAuthService>();
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();

    private SessionExpiredHandler CreateSut() =>
        new(_auth, _navigation, _dialogs, NullLogger<SessionExpiredHandler>.Instance);

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