using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace OpoStudy.Api.Authentication;

internal static class FirebaseAuthenticationExtensions
{
    /// <summary>
    /// Validates Firebase ID tokens (signature, issuer, audience and expiry) and makes every endpoint
    /// require an authenticated user unless it is explicitly marked as anonymous.
    /// </summary>
    public static WebApplicationBuilder AddFirebaseAuthentication(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<FirebaseAuthOptions>()
            .Bind(builder.Configuration.GetSection(FirebaseAuthOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // The project id is read lazily from the validated options, so a missing value fails at startup.
        builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<FirebaseAuthOptions>>((jwt, firebase) =>
            {
                var projectId = firebase.Value.ProjectId;
                var issuer = $"https://securetoken.google.com/{projectId}";

                jwt.Authority = issuer;
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = projectId,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                };

                // A signed token without a subject cannot identify a user: reject it as 401 instead of failing later.
                jwt.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        if (string.IsNullOrWhiteSpace(context.Principal?.FindFirst("sub")?.Value))
                        {
                            context.Fail("The token has no subject claim.");
                        }

                        return Task.CompletedTask;
                    },
                };
            });

        builder.Services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return builder;
    }
}