using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Logging;

namespace OpoStudy.Mobile.Core.Auth;

/// <summary>Source-generated JSON metadata for the persisted session.</summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(AuthSession))]
internal sealed partial class AuthSessionJsonContext : JsonSerializerContext;

/// <summary>
/// Keeps the Firebase session in secure storage and renews the ID token when it is about to expire.
/// Never logs tokens or emails.
/// </summary>
public sealed partial class AuthService(
    IFirebaseAuthClient firebase,
    ISecureStore store,
    IGoogleSignInProvider google,
    TimeProvider time,
    ILogger<AuthService> logger) : IAuthService, IDisposable
{
    private const string SessionKey = "auth.session";
    private const string PasswordProvider = "password";
    private const string GoogleProvider = "google.com";

    private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(5);

    private static readonly HashSet<string> SessionLostCodes = new(StringComparer.Ordinal)
    {
        "INVALID_REFRESH_TOKEN",
        "TOKEN_EXPIRED",
        "USER_DISABLED",
        "USER_NOT_FOUND",
        "INVALID_ID_TOKEN",
    };

    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private AuthSession? _session;

    public event EventHandler? SessionExpired;

    public void Dispose() => _refreshLock.Dispose();

    public bool IsSignedIn => _session is not null;

    public string? Email => _session?.Email;

    public string? Provider => _session?.Provider;

    public async Task RestoreSessionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var json = await store.GetAsync(SessionKey);
            if (json is null)
            {
                _session = null;
                return;
            }

            _session = JsonSerializer.Deserialize(json, AuthSessionJsonContext.Default.AuthSession);
            if (_session is null)
            {
                store.Remove(SessionKey);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogRestoreFailed(ex.GetType().Name);
            _session = null;
            store.Remove(SessionKey);
        }
    }

    public async Task SignInWithEmailAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var tokens = await firebase.SignInWithPasswordAsync(email, password, cancellationToken);
        await SaveSessionAsync(tokens, email, PasswordProvider);
    }

    public async Task SignUpWithEmailAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var tokens = await firebase.SignUpAsync(email, password, cancellationToken);
        await SaveSessionAsync(tokens, email, PasswordProvider);
    }

    public async Task<bool> SignInWithGoogleAsync(CancellationToken cancellationToken = default)
    {
        var googleToken = await google.GetIdTokenAsync(cancellationToken);
        if (googleToken is null)
        {
            return false;
        }

        var tokens = await firebase.SignInWithGoogleAsync(googleToken, cancellationToken);
        await SaveSessionAsync(tokens, string.Empty, GoogleProvider);
        return true;
    }

    public Task SendPasswordResetAsync(string email, CancellationToken cancellationToken = default) =>
        firebase.SendPasswordResetAsync(email, cancellationToken);

    public async Task<string?> GetIdTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_session is null)
        {
            return null;
        }

        if (!NeedsRefresh(_session))
        {
            return _session.IdToken;
        }

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            var current = _session;
            if (current is null)
            {
                return null;
            }

            // Another caller may have renewed the token while this one was waiting.
            if (!NeedsRefresh(current))
            {
                return current.IdToken;
            }

            try
            {
                var tokens = await firebase.RefreshAsync(current.RefreshToken, cancellationToken);
                var renewed = new AuthSession(
                    tokens.IdToken,
                    tokens.RefreshToken,
                    time.GetUtcNow().AddSeconds(tokens.ExpiresIn),
                    current.Email,
                    current.Provider);
                await PersistAsync(renewed);
                return renewed.IdToken;
            }
            catch (FirebaseAuthException ex) when (SessionLostCodes.Contains(ex.Code))
            {
                LogSessionLost(ex.Code);
                ClearSession();
                SessionExpired?.Invoke(this, EventArgs.Empty);
                return null;
            }
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void InvalidateIdToken()
    {
        if (_session is { } current)
        {
            _session = current with { ExpiresAt = DateTimeOffset.MinValue };
        }
    }

    public async Task SignOutAsync()
    {
        ClearSession();
        try
        {
            await google.SignOutAsync();
        }
        catch (Exception ex)
        {
            LogGoogleSignOutFailed(ex.GetType().Name);
        }
    }

    public async Task DeleteFirebaseAccountAsync(CancellationToken cancellationToken = default)
    {
        var token = await GetIdTokenAsync(cancellationToken)
            ?? throw new InvalidOperationException("There is no active session.");

        try
        {
            await firebase.DeleteAccountAsync(token, cancellationToken);
        }
        catch (FirebaseAuthException ex) when (ex.Code == "CREDENTIAL_TOO_OLD_LOGIN_AGAIN")
        {
            await SignOutAsync();
            throw;
        }

        await SignOutAsync();
    }

    private bool NeedsRefresh(AuthSession session) => session.ExpiresAt - time.GetUtcNow() <= RefreshMargin;

    private async Task SaveSessionAsync(FirebaseTokens tokens, string fallbackEmail, string provider)
    {
        var email = string.IsNullOrWhiteSpace(tokens.Email) ? fallbackEmail : tokens.Email;
        await PersistAsync(new AuthSession(
            tokens.IdToken,
            tokens.RefreshToken,
            time.GetUtcNow().AddSeconds(tokens.ExpiresIn),
            email,
            provider));
    }

    private async Task PersistAsync(AuthSession session)
    {
        await store.SetAsync(SessionKey, JsonSerializer.Serialize(session, AuthSessionJsonContext.Default.AuthSession));
        _session = session;
    }

    private void ClearSession()
    {
        _session = null;
        store.Remove(SessionKey);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Stored session could not be restored ({ExceptionType})")]
    private partial void LogRestoreFailed(string exceptionType);

    [LoggerMessage(Level = LogLevel.Information, Message = "Session cleared: Firebase rejected the refresh ({Code})")]
    private partial void LogSessionLost(string code);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Google sign-out failed ({ExceptionType})")]
    private partial void LogGoogleSignOutFailed(string exceptionType);
}