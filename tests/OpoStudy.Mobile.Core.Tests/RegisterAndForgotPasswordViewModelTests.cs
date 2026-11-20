using NSubstitute;
using NSubstitute.ExceptionExtensions;

using OpoStudy.Mobile.Core.Auth;
using OpoStudy.Mobile.Core.Navigation;
using OpoStudy.Mobile.Core.ViewModels;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests;

public sealed class RegisterViewModelTests
{
    private readonly IAuthService _auth = Substitute.For<IAuthService>();
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();

    private RegisterViewModel CreateSut(string email = "a@b.com", string password = "secret1", string confirm = "secret1") =>
        new(_auth, _navigation) { Email = email, Password = password, ConfirmPassword = confirm };

    [Theory]
    [InlineData("bad", "secret1", "secret1", "Escribe un correo electrónico válido.")]
    [InlineData("a@b.com", "12345", "12345", "La contraseña debe tener al menos 6 caracteres.")]
    [InlineData("a@b.com", "secret1", "secret2", "Las contraseñas no coinciden.")]
    public async Task RegisterCommand_InvalidInput_SetsErrorWithoutCallingFirebase(string email, string password, string confirm, string expected)
    {
        var sut = CreateSut(email, password, confirm);

        await sut.RegisterCommand.ExecuteAsync(null);

        sut.ErrorMessage.ShouldBe(expected);
        await _auth.DidNotReceiveWithAnyArgs().SignUpWithEmailAsync(default!, default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RegisterCommand_ValidInput_SignsUpAndNavigatesToHome()
    {
        var sut = CreateSut();

        await sut.RegisterCommand.ExecuteAsync(null);

        await _auth.Received(1).SignUpWithEmailAsync("a@b.com", "secret1", Arg.Any<CancellationToken>());
        await _navigation.Received(1).GoToAsync("//home");
    }

    [Fact]
    public async Task RegisterCommand_EmailExists_ShowsMessage()
    {
        _auth.SignUpWithEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new FirebaseAuthException("EMAIL_EXISTS"));
        var sut = CreateSut();

        await sut.RegisterCommand.ExecuteAsync(null);

        sut.ErrorMessage.ShouldBe(AuthErrorMessages.EmailExists);
        await _navigation.DidNotReceiveWithAnyArgs().GoToAsync(default!);
    }

    [Fact]
    public async Task RegisterCommand_WhileBusy_CannotExecute()
    {
        var gate = new TaskCompletionSource();
        _auth.SignUpWithEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(gate.Task);
        var sut = CreateSut();

        var running = sut.RegisterCommand.ExecuteAsync(null);

        sut.RegisterCommand.CanExecute(null).ShouldBeFalse();
        gate.SetResult();
        await running;
    }
}

public sealed class ForgotPasswordViewModelTests
{
    private readonly IAuthService _auth = Substitute.For<IAuthService>();

    [Fact]
    public void ApplyQuery_EmailParameter_PrefillsEmail()
    {
        var sut = new ForgotPasswordViewModel(_auth);

        sut.ApplyQuery(new Dictionary<string, object> { ["email"] = "a@b.com" });

        sut.Email.ShouldBe("a@b.com");
    }

    [Fact]
    public async Task SendCommand_InvalidEmail_SetsErrorWithoutCallingFirebase()
    {
        var sut = new ForgotPasswordViewModel(_auth) { Email = "nope" };

        await sut.SendCommand.ExecuteAsync(null);

        sut.HasError.ShouldBeTrue();
        sut.IsSent.ShouldBeFalse();
        await _auth.DidNotReceiveWithAnyArgs().SendPasswordResetAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SendCommand_ValidEmail_ShowsConfirmation()
    {
        var sut = new ForgotPasswordViewModel(_auth) { Email = "a@b.com" };

        await sut.SendCommand.ExecuteAsync(null);

        await _auth.Received(1).SendPasswordResetAsync("a@b.com", Arg.Any<CancellationToken>());
        sut.IsSent.ShouldBeTrue();
        sut.ConfirmationMessage.ShouldBe("Si existe una cuenta con ese correo, te hemos enviado un enlace para restablecer la contraseña.");
    }

    [Fact]
    public async Task SendCommand_FirebaseError_ShowsMessage()
    {
        _auth.SendPasswordResetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new FirebaseAuthException("TOO_MANY_ATTEMPTS_TRY_LATER"));
        var sut = new ForgotPasswordViewModel(_auth) { Email = "a@b.com" };

        await sut.SendCommand.ExecuteAsync(null);

        sut.ErrorMessage.ShouldBe(AuthErrorMessages.TooManyAttempts);
        sut.IsSent.ShouldBeFalse();
    }

    [Fact]
    public async Task SendCommand_WhileBusy_CannotExecute()
    {
        var gate = new TaskCompletionSource();
        _auth.SendPasswordResetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(gate.Task);
        var sut = new ForgotPasswordViewModel(_auth) { Email = "a@b.com" };

        var running = sut.SendCommand.ExecuteAsync(null);

        sut.SendCommand.CanExecute(null).ShouldBeFalse();
        gate.SetResult();
        await running;
    }
}