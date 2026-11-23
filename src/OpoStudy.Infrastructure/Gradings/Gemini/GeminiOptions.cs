using System.Text.RegularExpressions;

using Microsoft.Extensions.Options;

namespace OpoStudy.Infrastructure.Gradings.Gemini;

/// <summary>Settings of the Gemini corrector (section <c>Gemini</c>). Only registered when <c>Grading:Provider</c> is <c>Gemini</c>.</summary>
public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    /// <summary>API key. A secret: never in versioned files.</summary>
    public string? ApiKey { get; set; }

    public string Model { get; set; } = "gemini-3.8-flash";

    /// <summary>One of <c>MINIMAL</c>, <c>LOW</c>, <c>MEDIUM</c>, <c>HIGH</c>.</summary>
    public string ThinkingLevel { get; set; } = "LOW";

    public int MaxOutputTokens { get; set; } = 8192;

    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/";
}

/// <summary>Fails the startup when the Gemini settings make no sense.</summary>
internal sealed partial class GeminiOptionsValidator : IValidateOptions<GeminiOptions>
{
    private static readonly string[] ThinkingLevels = ["MINIMAL", "LOW", "MEDIUM", "HIGH"];

    [GeneratedRegex("^[a-z0-9.-]+$")]
    private static partial Regex ModelPattern();

    public ValidateOptionsResult Validate(string? name, GeminiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            errors.Add("Gemini:ApiKey is required when Grading:Provider is Gemini.");
        }

        if (string.IsNullOrWhiteSpace(options.Model) || !ModelPattern().IsMatch(options.Model))
        {
            errors.Add("Gemini:Model must not be empty and may only contain lowercase letters, digits, '.' and '-'.");
        }

        if (!ThinkingLevels.Contains(options.ThinkingLevel, StringComparer.Ordinal))
        {
            errors.Add($"Gemini:ThinkingLevel must be one of {string.Join(", ", ThinkingLevels)}.");
        }

        if (options.MaxOutputTokens is < 1024 or > 65536)
        {
            errors.Add("Gemini:MaxOutputTokens must be between 1024 and 65536.");
        }

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUrl)
            || baseUrl.Scheme != Uri.UriSchemeHttps
            || !options.BaseUrl.EndsWith('/'))
        {
            errors.Add("Gemini:BaseUrl must be an absolute https URI ending with '/'.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}