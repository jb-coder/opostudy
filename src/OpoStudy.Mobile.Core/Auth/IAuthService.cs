namespace OpoStudy.Mobile.Core.Auth;

/// <summary>Session state and operations of the signed-in user (Firebase Auth).</summary>
public interface IAuthService
{
    /// <summary>Raised when the session can no longer be renewed and was cleared.</summary>
    event EventHandler? SessionExpired;

    bool IsSignedIn { get; }

    string? Email { get; }

    /// <summary><c>password</c> or <c>google.com</c>.</summary>
    string? Provider { get; }

    /// <summary>Loads the stored session without using the network.</summary>
    Task RestoreSessionAsync(CancellationToken cancellationToken = default);

    Task SignInWithEmailAsync(string email, string password, CancellationToken cancellationToken = default);

    Task SignUpWithEmailAsync(string email, string password, CancellationToken cancellationToken = default);

    /// <summary>Returns false when the user cancelled the Google account picker.</summary>
    Task<bool> SignInWithGoogleAsync(CancellationToken cancellationToken = default);

    Task SendPasswordResetAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Returns a valid ID token (renewing it if needed), or null when there is no session.</summary>
    Task<string?> GetIdTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>Marks the current token as expired so the next call renews it.</summary>
    void InvalidateIdToken();

    Task SignOutAsync();

    /// <summary>Deletes the Firebase account and signs out.</summary>
    Task DeleteFirebaseAccountAsync(CancellationToken cancellationToken = default);
}