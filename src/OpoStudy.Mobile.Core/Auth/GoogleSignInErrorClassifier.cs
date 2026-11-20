using System.Text.RegularExpressions;

namespace OpoStudy.Mobile.Core.Auth;

public enum GoogleSignInOutcome
{
    Cancelled,
    NoAccount,
    Failed,
}

/// <summary>
/// Classifies Credential Manager errors. Credential Manager reports some Google errors (for example an
/// unregistered SHA-1) as a cancellation whose message carries a Play Services status code such as
/// "[16] Account reauth failed.", whereas a real user cancellation carries no bracketed code.
/// </summary>
public static partial class GoogleSignInErrorClassifier
{
    private const string CancellationType = "GetCredentialCancellationException";
    private const string NoCredentialType = "NoCredentialException";

    /// <summary>Classifies a Java exception by its simple type name and message.</summary>
    public static GoogleSignInOutcome Classify(string exceptionTypeName, string? message) => exceptionTypeName switch
    {
        CancellationType => ExtractStatusCode(message) is null ? GoogleSignInOutcome.Cancelled : GoogleSignInOutcome.Failed,
        NoCredentialType => GoogleSignInOutcome.NoAccount,
        _ => GoogleSignInOutcome.Failed,
    };

    /// <summary>Extracts the Google status code in brackets at the start of the message (e.g. "[16] ..."), if any.</summary>
    public static int? ExtractStatusCode(string? message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return null;
        }

        var match = StatusCodeRegex().Match(message);
        return match.Success && int.TryParse(match.Groups[1].Value, out var code) ? code : null;
    }

    [GeneratedRegex(@"^\s*\[(\d+)\]")]
    private static partial Regex StatusCodeRegex();
}