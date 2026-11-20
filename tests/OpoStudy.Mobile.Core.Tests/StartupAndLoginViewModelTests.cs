using NSubstitute;
using NSubstitute.ExceptionExtensions;

using OpoStudy.Mobile.Core.Auth;
using OpoStudy.Mobile.Core.Navigation;
using OpoStudy.Mobile.Core.ViewModels;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests;

public sealed class StartupViewModelTests
{
    private readonly IAuthService _auth = Substitute.For<IAuthService>();
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();

    [Fact]
    public async Task OnAppearingAsync_SessionRestored_NavigatesToHome()
    {
        _auth.IsSignedIn.Returns(true);

        await new StartupViewModel(_auth, _navigation).OnAppearingAsync();

        await _auth.Received(1).RestoreSessionAsync(Arg.Any<CancellationToken>());
        await _navigation.Received(1).GoToAsync("//home");
    }

    [Fact]
    public async Task OnAppearingAsync_NoSession_NavigatesToLogin()
    {
        _auth.IsSignedIn.Returns(false);

        await new StartupViewModel(_auth, _navigation).OnAppearingAsync();

        await _navigation.Received(1).GoToAsync("//login");
    }
}

public sealed class LoginViewModelTests
{
    private readonly IAuthService _auth = Substitute.For<IAuthService>();
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();

    private LoginViewModel CreateSut() => new(_auth, _navigation);

    [Theory]
    [InlineData("", "secret1")]
    [InlineData("not-an-email", "secret1")]
    [InlineData("a@b", "secret1")]
    [InlineData("a@b.com", "")]
    public async Task SignInCommand_InvalidInput_SetsErrorWithoutCallingFirebase(string email, string password)
    {
        var sut = CreateSut();
        sut.Email = email;
        sut.Password = password;

        await sut.SignInCommand.ExecuteAsync(null);

        sut.HasError.ShouldBeTrue();
        await _auth.DidNotReceiveWithAnyArgs().SignInWithEmailAsync(default!, default!, TestContext.Current.CancellationToken);
        await _navigation.DidNotReceiveWithAnyArgs().GoToAsync(default!);
    }

    [Fact]
    public async Task SignInCommand_ValidCredentials_NavigatesToHomeAndClearsPassword()
    {
        var sut = CreateSut();
        sut.Email = "a@b.com";
        sut.Password = "secret1";

        await sut.SignInCommand.ExecuteAsync(null);

        await _auth.Received(1).SignInWithEmailAsync("a@b.com", "secret1", Arg.Any<CancellationToken>());
        await _navigation.Received(1).GoToAsync("//home");
        sut.Password.ShouldBeEmpty();
        sut.HasError.ShouldBeFalse();
    }

    [Fact]
    public async Task SignInCommand_FirebaseError_ShowsMessageAndStays()
    {
        _auth.SignInWithEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new FirebaseAuthException("INVALID_LOGIN_CREDENTIALS"));
        var sut = CreateSut();
        sut.Email = "a@b.com";
        sut.Password = "wrong";

        await sut.SignInCommand.ExecuteAsync(null);

        sut.ErrorMessage.ShouldBe("Correo o contraseña incorrectos.");
        sut.IsBusy.ShouldBeFalse();
        await _navigation.DidNotReceiveWithAnyArgs().GoToAsync(default!);
    }

    [Fact]
    public async Task SignInCommand_WhileBusy_CannotExecute()
    {
        var gate = new TaskCompletionSource();
        _auth.SignInWithEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(gate.Task);
        var sut = CreateSut();
        sut.Email = "a@b.com";
        sut.Password = "secret1";

        var running = sut.SignInCommand.ExecuteAsync(null);

        sut.SignInCommand.CanExecute(null).ShouldBeFalse();
        sut.SignInWithGoogleCommand.CanExecute(null).ShouldBeFalse();

        gate.SetResult();
        await running;
        sut.SignInCommand.CanExecute(null).ShouldBeTrue();
    }

    [Fact]
    public async Task SignInWithGoogleCommand_Success_NavigatesToHome()
    {
        _auth.SignInWithGoogleAsync(Arg.Any<CancellationToken>()).Returns(true);

        await CreateSut().SignInWithGoogleCommand.ExecuteAsync(null);

        await _navigation.Received(1).GoToAsync("//home");
    }

    [Fact]
    public async Task SignInWithGoogleCommand_Cancelled_ShowsNoError()
    {
        _auth.SignInWithGoogleAsync(Arg.Any<CancellationToken>()).Returns(false);
        var sut = CreateSut();

        await sut.SignInWithGoogleCommand.ExecuteAsync(null);

        sut.HasError.ShouldBeFalse();
        sut.ErrorMessage.ShouldBeNull();
        await _navigation.DidNotReceiveWithAnyArgs().GoToAsync(default!);
    }

    [Fact]
    public async Task SignInWithGoogleCommand_GoogleFailure_ShowsMessageAndStopsBusy()
    {
        _auth.SignInWithGoogleAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new GoogleSignInException(GoogleSignInFailure.Failed));
        var sut = CreateSut();

        await sut.SignInWithGoogleCommand.ExecuteAsync(null);

        sut.ErrorMessage.ShouldBe("No se ha podido iniciar sesión con Google. Inténtalo de nuevo o entra con tu correo.");
        sut.IsBusy.ShouldBeFalse();
        await _navigation.DidNotReceiveWithAnyArgs().GoToAsync(default!);
    }

    [Fact]
    public async Task SignInWithGoogleCommand_NoAccount_ShowsMessage()
    {
        _auth.SignInWithGoogleAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new GoogleSignInException(GoogleSignInFailure.NoAccount));
        var sut = CreateSut();

        await sut.SignInWithGoogleCommand.ExecuteAsync(null);

        sut.ErrorMessage.ShouldBe("No hay ninguna cuenta de Google en el dispositivo.");
    }

    [Fact]
    public async Task GoToRegisterCommand_Executed_NavigatesToRegister()
    {
        await CreateSut().GoToRegisterCommand.ExecuteAsync(null);

        await _navigation.Received(1).GoToAsync("register");
    }

    [Fact]
    public async Task GoToForgotPasswordCommand_Executed_PassesTypedEmail()
    {
        var sut = CreateSut();
        sut.Email = "a@b.com";

        await sut.GoToForgotPasswordCommand.ExecuteAsync(null);

        await _navigation.Received(1).GoToAsync(
            "forgot-password",
            Arg.Is<IReadOnlyDictionary<string, object>>(p => (string)p["email"] == "a@b.com"));
    }
}