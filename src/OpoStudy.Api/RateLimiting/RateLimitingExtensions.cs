using System.Globalization;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

using OpoStudy.Api.Authentication;

namespace OpoStudy.Api.RateLimiting;

internal static class RateLimitingExtensions
{
    public const string PerUserPolicy = "per-user";

    /// <summary>Fixed-window limit per user (Firebase uid) or, without a user, per remote IP.</summary>
    public static WebApplicationBuilder AddPerUserRateLimiting(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<RateLimitingOptions>()
            .Bind(builder.Configuration.GetSection(RateLimitingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddRateLimiter(_ => { });

        // Configured lazily so the limits come from the final configuration (also in tests).
        builder.Services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<RateLimitingOptions>>((limiter, settings) =>
            {
                var window = TimeSpan.FromSeconds(settings.Value.WindowSeconds);
                var permitLimit = settings.Value.PermitLimit;

                limiter.AddPolicy(PerUserPolicy, context =>
                {
                    var uid = context.User.FindFirst(ClaimsPrincipalExtensions.SubjectClaimType)?.Value;
                    var key = uid is not null
                        ? $"user:{uid}"
                        : $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

                    return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = window,
                        QueueLimit = 0,
                    });
                });

                limiter.OnRejected = async (context, _) =>
                {
                    var response = context.HttpContext.Response;
                    response.StatusCode = StatusCodes.Status429TooManyRequests;
                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    {
                        response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                    }

                    var problems = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                    await problems.TryWriteAsync(new ProblemDetailsContext
                    {
                        HttpContext = context.HttpContext,
                        ProblemDetails =
                        {
                            Status = StatusCodes.Status429TooManyRequests,
                            Title = "Demasiadas peticiones. Inténtalo de nuevo en unos segundos.",
                        },
                    });
                };
            });

        return builder;
    }
}