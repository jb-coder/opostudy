using System.Text;
using System.Text.Json;

namespace OpoStudy.Mobile.Core.Auth;

/// <summary>Reads claims of our own Firebase ID token without validating it (the server does that).</summary>
internal static class IdTokenClaims
{
    /// <summary>The <c>user_id</c> claim (or <c>sub</c>) of the JWT payload, or null when the token is malformed.</summary>
    public static string? ReadUserId(string idToken)
    {
        var parts = idToken.Split('.');
        if (parts.Length < 2)
        {
            return null;
        }

        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');
            using var document = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return ReadString(root, "user_id") ?? ReadString(root, "sub");
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            return null;
        }
    }

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()
            : null;
}
