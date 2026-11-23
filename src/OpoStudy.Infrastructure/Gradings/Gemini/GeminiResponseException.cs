using System.Globalization;

namespace OpoStudy.Infrastructure.Gradings.Gemini;

/// <summary>
/// A final failure of the Gemini corrector (client error, blocked prompt, bad finish reason, invalid output).
/// Holds only the HTTP code, the Google <c>status</c> and a short reason: never the response body, the prompt or the key.
/// </summary>
public sealed class GeminiResponseException : Exception
{
    public GeminiResponseException()
    {
    }

    public GeminiResponseException(string message)
        : base(message)
    {
    }

    public GeminiResponseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public GeminiResponseException(int? httpStatusCode, string? googleStatus, string reason)
        : base(Describe(httpStatusCode, googleStatus, reason))
    {
        HttpStatusCode = httpStatusCode;
        GoogleStatus = googleStatus;
        Reason = reason;
    }

    public int? HttpStatusCode { get; }

    public string? GoogleStatus { get; }

    public string? Reason { get; }

    private static string Describe(int? httpStatusCode, string? googleStatus, string reason) =>
        $"Gemini failure (http={httpStatusCode?.ToString(CultureInfo.InvariantCulture) ?? "-"}, status={googleStatus ?? "-"}): {reason}";
}