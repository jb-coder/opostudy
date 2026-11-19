namespace OpoStudy.Mobile.Core.Auth;

/// <summary>Tokens returned by a Firebase sign-in or refresh. <see cref="ExpiresIn"/> is in seconds.</summary>
public sealed record FirebaseTokens(string IdToken, string RefreshToken, int ExpiresIn, string? Email);

/// <summary>Firebase Auth REST API.</summary>
public interface IFirebaseAuthClient
{
    Task<FirebaseTokens> SignUpAsync(string email, string password, CancellationToken cancellationToken = default);

    Task<FirebaseTokens> SignInWithPasswordAsync(string email, string password, CancellationToken cancellationToken = default);

    Task<FirebaseTokens> SignInWithGoogleAsync(string googleIdToken, CancellationToken cancellationToken = default);

    Task SendPasswordResetAsync(string email, CancellationToken cancellationToken = default);

    Task<FirebaseTokens> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task DeleteAccountAsync(string idToken, CancellationToken cancellationToken = default);
}