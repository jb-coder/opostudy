using System.Net;
using System.Net.Sockets;
using System.Text.Json;

using OpoStudy.Mobile.Core.Auth;

using Refit;

namespace OpoStudy.Mobile.Core.Api;

/// <summary>Maps exceptions to user-facing (Spanish) messages without leaking internal details.</summary>
public static class ApiErrorMessages
{
    public const string ServerError = "El servidor ha respondido con un error. Inténtalo de nuevo más tarde.";
    public const string Unreachable = "No se puede conectar con el servidor. Revisa tu conexión.";
    public const string Timeout = "El servidor tarda demasiado en responder. Inténtalo de nuevo.";
    public const string Unexpected = "Ha ocurrido un error inesperado.";

    public static string From(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return Classify(exception) ?? Unexpected;
    }

    /// <summary>Walks the exception tree (inner exceptions and aggregates) and returns the first known classification.</summary>
    private static string? Classify(Exception exception)
    {
        switch (exception)
        {
            case FirebaseAuthException firebase:
                return AuthErrorMessages.From(firebase.Code);
            case GoogleSignInException googleSignIn:
                return AuthErrorMessages.From(googleSignIn);
            case ApiException api:
                return (api.StatusCode is >= HttpStatusCode.BadRequest and < HttpStatusCode.InternalServerError ? FromProblemDetails(api) : null) ?? ServerError;
            case TaskCanceledException or TimeoutException:
                return Timeout;
            case HttpRequestException or SocketException or IOException:
                return Unreachable;
        }

        if (exception is AggregateException aggregate)
        {
            foreach (var inner in aggregate.InnerExceptions)
            {
                if (Classify(inner) is { } message)
                {
                    return message;
                }
            }

            return null;
        }

        return exception.InnerException is null ? null : Classify(exception.InnerException);
    }

    /// <summary>Stable error <c>code</c> of a ProblemDetails response (4xx only), or <c>null</c>.</summary>
    public static string? ProblemCode(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is not ApiException { StatusCode: >= HttpStatusCode.BadRequest and < HttpStatusCode.InternalServerError } api
            || string.IsNullOrWhiteSpace(api.Content))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(api.Content);
            return document.RootElement.ValueKind == JsonValueKind.Object ? ReadString(document.RootElement, "code") : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? FromProblemDetails(ApiException exception)
    {
        if (string.IsNullOrWhiteSpace(exception.Content))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(exception.Content);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return ReadString(document.RootElement, "detail") ?? ReadString(document.RootElement, "title");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadString(JsonElement root, string name)
    {
        if (root.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value.GetString()))
        {
            return value.GetString();
        }

        return null;
    }
}
