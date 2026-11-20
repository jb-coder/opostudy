namespace OpoStudy.Mobile.Core.Auth;

/// <summary>Maps Firebase error codes to user-facing (Spanish) messages.</summary>
public static class AuthErrorMessages
{
    public const string InvalidCredentials = "Correo o contraseña incorrectos.";
    public const string EmailExists = "Ya existe una cuenta con ese correo. Inicia sesión o recupera la contraseña.";
    public const string InvalidEmail = "El correo electrónico no es válido.";
    public const string WeakPassword = "La contraseña es demasiado débil. Usa al menos 6 caracteres.";
    public const string UserDisabled = "Esta cuenta está desactivada.";
    public const string TooManyAttempts = "Demasiados intentos. Espera unos minutos y vuelve a intentarlo.";
    public const string OperationNotAllowed = "Este método de acceso no está disponible.";
    public const string CredentialTooOld = "Por seguridad, vuelve a iniciar sesión y repite el borrado de la cuenta.";
    public const string SessionExpired = "Tu sesión ha caducado. Vuelve a iniciar sesión.";
    public const string NotConfigured = "El inicio de sesión no está configurado en esta versión de la app.";
    public const string Unknown = "No se ha podido completar la operación. Inténtalo de nuevo.";
    public const string GoogleNoAccount = "No hay ninguna cuenta de Google en el dispositivo.";
    public const string GoogleFailed = "No se ha podido iniciar sesión con Google. Inténtalo de nuevo o entra con tu correo.";

    public static string From(string code) => code switch
    {
        "INVALID_LOGIN_CREDENTIALS" or "INVALID_PASSWORD" or "EMAIL_NOT_FOUND" => InvalidCredentials,
        "EMAIL_EXISTS" => EmailExists,
        "INVALID_EMAIL" => InvalidEmail,
        "WEAK_PASSWORD" => WeakPassword,
        "USER_DISABLED" => UserDisabled,
        "TOO_MANY_ATTEMPTS_TRY_LATER" => TooManyAttempts,
        "OPERATION_NOT_ALLOWED" => OperationNotAllowed,
        "CREDENTIAL_TOO_OLD_LOGIN_AGAIN" => CredentialTooOld,
        "TOKEN_EXPIRED" or "INVALID_REFRESH_TOKEN" or "USER_NOT_FOUND" or "INVALID_ID_TOKEN" => SessionExpired,
        "NOT_CONFIGURED" => NotConfigured,
        _ => Unknown,
    };

    public static string From(GoogleSignInException exception) =>
        exception.Failure == GoogleSignInFailure.NoAccount ? GoogleNoAccount : GoogleFailed;
}