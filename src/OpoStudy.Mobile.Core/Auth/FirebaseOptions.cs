namespace OpoStudy.Mobile.Core.Auth;

/// <summary>Firebase settings read from <c>google-services.json</c>.</summary>
public sealed record FirebaseOptions(string ApiKey, string GoogleWebClientId, string ProjectId)
{
    /// <summary>Options used when <c>google-services.json</c> is not embedded in the app.</summary>
    public static FirebaseOptions Empty { get; } = new(string.Empty, string.Empty, string.Empty);

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}