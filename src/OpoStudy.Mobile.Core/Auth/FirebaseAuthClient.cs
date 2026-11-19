using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace OpoStudy.Mobile.Core.Auth;

/// <summary><see cref="IFirebaseAuthClient"/> over <see cref="HttpClient"/> (Firebase Auth REST API).</summary>
public sealed class FirebaseAuthClient : IFirebaseAuthClient
{
    private const string IdentityBase = "https://identitytoolkit.googleapis.com/v1/";
    private const string TokenUrl = "https://securetoken.googleapis.com/v1/token";
    private const string CodeSeparator = " : ";
    private const int DefaultExpiresInSeconds = 3600;

    private readonly HttpClient _http;
    private readonly FirebaseOptions _options;

    public FirebaseAuthClient(HttpClient http, FirebaseOptions options)
    {
        _http = http;
        _options = options;
    }

    public Task<FirebaseTokens> SignUpAsync(string email, string password, CancellationToken cancellationToken = default) =>
        SendForTokensAsync(
            "accounts:signUp",
            new CredentialsRequest(email, password, true),
            FirebaseJsonContext.Default.CredentialsRequest,
            cancellationToken);

    public Task<FirebaseTokens> SignInWithPasswordAsync(string email, string password, CancellationToken cancellationToken = default) =>
        SendForTokensAsync(
            "accounts:signInWithPassword",
            new CredentialsRequest(email, password, true),
            FirebaseJsonContext.Default.CredentialsRequest,
            cancellationToken);

    public Task<FirebaseTokens> SignInWithGoogleAsync(string googleIdToken, CancellationToken cancellationToken = default) =>
        SendForTokensAsync(
            "accounts:signInWithIdp",
            new IdpRequest(
                $"id_token={Uri.EscapeDataString(googleIdToken)}&providerId=google.com",
                "http://localhost",
                true,
                true),
            FirebaseJsonContext.Default.IdpRequest,
            cancellationToken);

    public async Task SendPasswordResetAsync(string email, CancellationToken cancellationToken = default)
    {
        using var request = CreateJsonRequest(
            "accounts:sendOobCode",
            new OobCodeRequest("PASSWORD_RESET", email),
            FirebaseJsonContext.Default.OobCodeRequest);
        request.Headers.Add("X-Firebase-Locale", "es");
        using var response = await SendAsync(request, cancellationToken);
    }

    public async Task<FirebaseTokens> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{TokenUrl}?key={Uri.EscapeDataString(_options.ApiKey)}")
        {
            Content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", refreshToken),
            ]),
        };

        using var response = await SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync(FirebaseJsonContext.Default.RefreshResponse, cancellationToken)
            ?? throw new FirebaseAuthException("UNKNOWN");
        return new FirebaseTokens(payload.IdToken, payload.RefreshToken, ParseSeconds(payload.ExpiresIn), null);
    }

    public async Task DeleteAccountAsync(string idToken, CancellationToken cancellationToken = default)
    {
        using var request = CreateJsonRequest("accounts:delete", new DeleteRequest(idToken), FirebaseJsonContext.Default.DeleteRequest);
        using var response = await SendAsync(request, cancellationToken);
    }

    private static int ParseSeconds(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) ? seconds : DefaultExpiresInSeconds;

    private static async Task<string> ReadErrorCodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync(FirebaseJsonContext.Default.ErrorBody, cancellationToken);
            var message = body?.Error?.Message;
            if (string.IsNullOrWhiteSpace(message))
            {
                return "UNKNOWN";
            }

            var index = message.IndexOf(CodeSeparator, StringComparison.Ordinal);
            return index < 0 ? message : message[..index];
        }
        catch (JsonException)
        {
            return "UNKNOWN";
        }
    }

    private async Task<FirebaseTokens> SendForTokensAsync<T>(string path, T body, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        using var request = CreateJsonRequest(path, body, typeInfo);
        using var response = await SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync(FirebaseJsonContext.Default.SignInResponse, cancellationToken)
            ?? throw new FirebaseAuthException("UNKNOWN");
        return new FirebaseTokens(payload.IdToken, payload.RefreshToken, ParseSeconds(payload.ExpiresIn), payload.Email);
    }

    private HttpRequestMessage CreateJsonRequest<T>(string path, T body, JsonTypeInfo<T> typeInfo)
    {
        EnsureConfigured();
        return new HttpRequestMessage(HttpMethod.Post, $"{IdentityBase}{path}?key={Uri.EscapeDataString(_options.ApiKey)}")
        {
            Content = JsonContent.Create(body, typeInfo),
        };
    }

    private void EnsureConfigured()
    {
        if (!_options.IsConfigured)
        {
            throw new FirebaseAuthException("NOT_CONFIGURED");
        }
    }

    /// <summary>Sends the request; non-success responses become <see cref="FirebaseAuthException"/>. Network errors pass through.</summary>
    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await _http.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        using (response)
        {
            throw new FirebaseAuthException(await ReadErrorCodeAsync(response, cancellationToken));
        }
    }
}