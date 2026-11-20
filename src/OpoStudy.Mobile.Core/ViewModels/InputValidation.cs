namespace OpoStudy.Mobile.Core.ViewModels;

/// <summary>Local input checks done before calling the network.</summary>
internal static class InputValidation
{
    public const int MinPasswordLength = 6;

    public const string InvalidEmailMessage = "Escribe un correo electrónico válido.";
    public const string PasswordRequiredMessage = "Escribe tu contraseña.";
    public const string PasswordTooShortMessage = "La contraseña debe tener al menos 6 caracteres.";
    public const string PasswordsDoNotMatchMessage = "Las contraseñas no coinciden.";

    /// <summary>True when the text has a local part, an '@' and a domain containing a dot.</summary>
    public static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var value = email.Trim();
        var at = value.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0 || at != value.LastIndexOf('@'))
        {
            return false;
        }

        var domain = value[(at + 1)..];
        var dot = domain.LastIndexOf('.');
        return dot > 0 && dot < domain.Length - 1 && !value.Contains(' ', StringComparison.Ordinal);
    }
}