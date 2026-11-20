using OpoStudy.Mobile.Core.Auth;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests.Auth;

public sealed class AuthErrorMessagesTests
{
    [Theory]
    [InlineData("INVALID_LOGIN_CREDENTIALS", "Correo o contraseña incorrectos.")]
    [InlineData("INVALID_PASSWORD", "Correo o contraseña incorrectos.")]
    [InlineData("EMAIL_NOT_FOUND", "Correo o contraseña incorrectos.")]
    [InlineData("EMAIL_EXISTS", "Ya existe una cuenta con ese correo. Inicia sesión o recupera la contraseña.")]
    [InlineData("INVALID_EMAIL", "El correo electrónico no es válido.")]
    [InlineData("WEAK_PASSWORD", "La contraseña es demasiado débil. Usa al menos 6 caracteres.")]
    [InlineData("USER_DISABLED", "Esta cuenta está desactivada.")]
    [InlineData("TOO_MANY_ATTEMPTS_TRY_LATER", "Demasiados intentos. Espera unos minutos y vuelve a intentarlo.")]
    [InlineData("OPERATION_NOT_ALLOWED", "Este método de acceso no está disponible.")]
    [InlineData("CREDENTIAL_TOO_OLD_LOGIN_AGAIN", "Por seguridad, vuelve a iniciar sesión y repite el borrado de la cuenta.")]
    [InlineData("TOKEN_EXPIRED", "Tu sesión ha caducado. Vuelve a iniciar sesión.")]
    [InlineData("INVALID_REFRESH_TOKEN", "Tu sesión ha caducado. Vuelve a iniciar sesión.")]
    [InlineData("USER_NOT_FOUND", "Tu sesión ha caducado. Vuelve a iniciar sesión.")]
    [InlineData("INVALID_ID_TOKEN", "Tu sesión ha caducado. Vuelve a iniciar sesión.")]
    [InlineData("NOT_CONFIGURED", "El inicio de sesión no está configurado en esta versión de la app.")]
    [InlineData("SOMETHING_NEW", "No se ha podido completar la operación. Inténtalo de nuevo.")]
    public void From_Code_ReturnsSpanishMessage(string code, string expected) =>
        AuthErrorMessages.From(code).ShouldBe(expected);

    [Theory]
    [InlineData(GoogleSignInFailure.NoAccount, "No hay ninguna cuenta de Google en el dispositivo.")]
    [InlineData(GoogleSignInFailure.Failed, "No se ha podido iniciar sesión con Google. Inténtalo de nuevo o entra con tu correo.")]
    public void From_GoogleFailure_ReturnsSpanishMessage(GoogleSignInFailure failure, string expected) =>
        AuthErrorMessages.From(new GoogleSignInException(failure)).ShouldBe(expected);
}