using System.Text.Json;

namespace OpoStudy.Mobile.Core.Auth;

/// <summary>Reads the Firebase settings the app needs from a <c>google-services.json</c> file.</summary>
public static class GoogleServicesConfig
{
    private const int WebOAuthClientType = 3;

    public static FirebaseOptions Parse(Stream json, string packageName)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageName);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var projectId = GetString(root, "project_info", "project_id")
            ?? throw new InvalidOperationException("google-services.json no contiene project_info.project_id.");

        var client = FindClient(root, packageName)
            ?? throw new InvalidOperationException(
                $"google-services.json no contiene ningún cliente para el paquete '{packageName}': añade la app Android en Firebase y vuelve a descargar el fichero.");

        string? apiKey = null;
        if (client.TryGetProperty("api_key", out var apiKeys) && apiKeys.ValueKind == JsonValueKind.Array)
        {
            foreach (var key in apiKeys.EnumerateArray())
            {
                apiKey = GetString(key, "current_key");
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    break;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("google-services.json no contiene api_key[0].current_key.");
        }

        string? webClientId = null;
        if (client.TryGetProperty("oauth_client", out var oauthClients) && oauthClients.ValueKind == JsonValueKind.Array)
        {
            foreach (var oauth in oauthClients.EnumerateArray())
            {
                if (oauth.TryGetProperty("client_type", out var type)
                    && type.ValueKind == JsonValueKind.Number
                    && type.GetInt32() == WebOAuthClientType)
                {
                    webClientId = GetString(oauth, "client_id");
                    break;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(webClientId))
        {
            throw new InvalidOperationException(
                "google-services.json no contiene un cliente web de OAuth: activa Google en Firebase y vuelve a descargar el fichero.");
        }

        return new FirebaseOptions(apiKey, webClientId, projectId);
    }

    private static JsonElement? FindClient(JsonElement root, string packageName)
    {
        if (!root.TryGetProperty("client", out var clients) || clients.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var client in clients.EnumerateArray())
        {
            if (GetString(client, "client_info", "android_client_info", "package_name") == packageName)
            {
                return client;
            }
        }

        return null;
    }

    private static string? GetString(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var name in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(name, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }
}