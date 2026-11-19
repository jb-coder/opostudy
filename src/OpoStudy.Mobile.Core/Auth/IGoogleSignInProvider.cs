namespace OpoStudy.Mobile.Core.Auth;

/// <summary>Obtains a Google ID token from the device.</summary>
public interface IGoogleSignInProvider
{
    /// <summary>Returns the Google ID token, or null if the user cancelled. Throws <see cref="GoogleSignInException"/> on failure.</summary>
    Task<string?> GetIdTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>Clears the credential state so the account picker appears again.</summary>
    Task SignOutAsync();
}