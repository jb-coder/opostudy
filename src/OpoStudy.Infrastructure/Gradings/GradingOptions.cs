using Microsoft.Extensions.Options;

namespace OpoStudy.Infrastructure.Gradings;

/// <summary>Settings of the correction queue (section <c>Grading</c>).</summary>
public sealed class GradingOptions
{
    public const string SectionName = "Grading";
    public const string FakeProvider = "Fake";
    public const string GeminiProvider = "Gemini";

    private static readonly TimeSpan[] DefaultRetryDelays = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5)];

    /// <summary>Which <see cref="ILlmCorrector"/> to use. <c>Fake</c> or <c>Gemini</c>.</summary>
    public string Provider { get; set; } = FakeProvider;

    /// <summary>Corrections per user and calendar month (API).</summary>
    public int MonthlyQuota { get; set; } = 10;

    /// <summary>Tries per grading before it fails (Worker).</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Wait before the 2nd, 3rd... try; the last one repeats if there are more tries than delays. Left <c>null</c> it uses the defaults
    /// (a collection with a default value would get the configured items appended to it when bound).
    /// </summary>
    public TimeSpan[]? RetryDelays { get; set; }

    /// <summary>Longest a single correction may take.</summary>
    public TimeSpan CorrectorTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>After this a <c>Processing</c> grading is considered abandoned (the worker died) and is claimed again.</summary>
    public TimeSpan LeaseTimeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>How long the worker sleeps when the queue is empty.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>The delay before the try that follows <paramref name="attempts"/> tries already made (<paramref name="attempts"/> ≥ 1).</summary>
    public TimeSpan RetryDelayAfter(int attempts)
    {
        var delays = RetryDelays is { Length: > 0 } configured ? configured : DefaultRetryDelays;
        return delays[Math.Clamp(attempts - 1, 0, delays.Length - 1)];
    }
}

/// <summary>Fails the startup when the settings make no sense.</summary>
internal sealed class GradingOptionsValidator : IValidateOptions<GradingOptions>
{
    public ValidateOptionsResult Validate(string? name, GradingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var errors = new List<string>();
        if (!string.Equals(options.Provider, GradingOptions.FakeProvider, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(options.Provider, GradingOptions.GeminiProvider, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"Grading:Provider '{options.Provider}' is not supported (use '{GradingOptions.FakeProvider}' or '{GradingOptions.GeminiProvider}').");
        }

        if (options.MonthlyQuota < 1)
        {
            errors.Add("Grading:MonthlyQuota must be at least 1.");
        }

        if (options.MaxAttempts < 1)
        {
            errors.Add("Grading:MaxAttempts must be at least 1.");
        }

        if (options.RetryDelays?.Any(d => d < TimeSpan.Zero) == true)
        {
            errors.Add("Grading:RetryDelays cannot contain negative values.");
        }

        if (options.CorrectorTimeout <= TimeSpan.Zero)
        {
            errors.Add("Grading:CorrectorTimeout must be positive.");
        }

        if (options.LeaseTimeout <= options.CorrectorTimeout)
        {
            errors.Add("Grading:LeaseTimeout must be greater than Grading:CorrectorTimeout.");
        }

        if (options.PollInterval <= TimeSpan.Zero)
        {
            errors.Add("Grading:PollInterval must be positive.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}