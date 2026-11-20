using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpoStudy.Mobile.Core.Auth;

internal sealed record CredentialsRequest(string Email, string Password, bool ReturnSecureToken);

internal sealed record IdpRequest(string PostBody, string RequestUri, bool ReturnSecureToken, bool ReturnIdpCredential);

internal sealed record OobCodeRequest(string RequestType, string Email);

internal sealed record DeleteRequest(string IdToken);

/// <summary>Sign-in response. <c>ExpiresIn</c> arrives as a string.</summary>
internal sealed record SignInResponse(string IdToken, string RefreshToken, string ExpiresIn, string? Email);

/// <summary>Token refresh response (snake_case).</summary>
internal sealed record RefreshResponse(
    [property: JsonPropertyName("id_token")] string IdToken,
    [property: JsonPropertyName("refresh_token")] string RefreshToken,
    [property: JsonPropertyName("expires_in")] string ExpiresIn);

internal sealed record ErrorBody(ErrorDetail? Error);

internal sealed record ErrorDetail(string? Message);

/// <summary>Source-generated JSON metadata for the Firebase REST DTOs.</summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(CredentialsRequest))]
[JsonSerializable(typeof(IdpRequest))]
[JsonSerializable(typeof(OobCodeRequest))]
[JsonSerializable(typeof(DeleteRequest))]
[JsonSerializable(typeof(SignInResponse))]
[JsonSerializable(typeof(RefreshResponse))]
[JsonSerializable(typeof(ErrorBody))]
internal sealed partial class FirebaseJsonContext : JsonSerializerContext;