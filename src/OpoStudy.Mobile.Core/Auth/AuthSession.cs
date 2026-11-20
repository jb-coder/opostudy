namespace OpoStudy.Mobile.Core.Auth;

/// <summary>Persisted Firebase session. Provider is <c>password</c> or <c>google.com</c>.</summary>
public sealed record AuthSession(string IdToken, string RefreshToken, DateTimeOffset ExpiresAt, string Email, string Provider);