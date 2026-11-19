using System.ComponentModel.DataAnnotations;

namespace OpoStudy.Api.RateLimiting;

/// <summary>Rate limiting settings (section <c>RateLimiting</c>).</summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    [Range(1, 100_000)]
    public int PermitLimit { get; set; } = 120;

    [Range(1, 3_600)]
    public int WindowSeconds { get; set; } = 60;
}