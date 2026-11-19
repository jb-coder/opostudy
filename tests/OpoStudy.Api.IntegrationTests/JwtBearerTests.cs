using System.Net;
using System.Net.Http.Headers;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

using Shouldly;

namespace OpoStudy.Api.IntegrationTests;

/// <summary>The real JwtBearer scheme (without the test handler), with no network access.</summary>
public sealed class JwtBearerTests(ApiFactory api)
{
    [Fact]
    public async Task GetMe_WithMalformedToken_ReturnsProblemDetails401()
    {
        await using var factory = api.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.Configure<AuthenticationOptions>(o =>
            {
                o.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            });

            // A static configuration prevents the handler from fetching the Google discovery document.
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, o =>
            {
                o.ConfigurationManager = null;
                o.Configuration = new OpenIdConnectConfiguration();
            });
        }));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-jwt");

        var response = await client.GetAsync(new Uri("/api/v1/me", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        response.Headers.WwwAuthenticate.ToString().ShouldStartWith("Bearer");
    }

    [Fact]
    public async Task Start_WithoutFirebaseProjectId_ThrowsOptionsValidationException()
    {
        await using var factory = api.WithWebHostBuilder(builder => builder.UseSetting("Firebase:ProjectId", ""));

        Should.Throw<OptionsValidationException>(() => factory.CreateClient());
        await Task.CompletedTask;
    }
}