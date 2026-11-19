using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

using Shouldly;

namespace OpoStudy.Api.IntegrationTests;

/// <summary>The real JwtBearer scheme (without the test handler), with no network access.</summary>
public sealed class JwtBearerTests(ApiFactory api)
{
    private const string ProjectId = "test-project";
    private const string KeyId = "test-key";
    private const string ValidIssuer = "https://securetoken.google.com/test-project";
    private static readonly Uri MeUri = new("/api/v1/me", UriKind.Relative);

    [Fact]
    public async Task GetMe_WithMalformedToken_ReturnsProblemDetails401()
    {
        using var rsa = RSA.Create(2048);
        await using var factory = CreateFactory(rsa);
        using var client = CreateClient(factory, "not-a-jwt");

        var response = await client.GetAsync(MeUri, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        response.Headers.WwwAuthenticate.ToString().ShouldStartWith("Bearer");
    }

    [Fact]
    public async Task GetMe_WithValidToken_Returns200()
    {
        using var rsa = RSA.Create(2048);
        await using var factory = CreateFactory(rsa);
        using var client = CreateClient(factory, CreateToken(rsa));

        var response = await client.GetAsync(MeUri, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetMe_WithWrongIssuer_ReturnsProblemDetails401()
    {
        using var rsa = RSA.Create(2048);
        await using var factory = CreateFactory(rsa);
        using var client = CreateClient(factory, CreateToken(rsa, issuer: "https://securetoken.google.com/other-project"));

        var response = await client.GetAsync(MeUri, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task GetMe_WithWrongAudience_ReturnsProblemDetails401()
    {
        using var rsa = RSA.Create(2048);
        await using var factory = CreateFactory(rsa);
        using var client = CreateClient(factory, CreateToken(rsa, audience: "other-project"));

        var response = await client.GetAsync(MeUri, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task GetMe_WithExpiredToken_ReturnsProblemDetails401()
    {
        using var rsa = RSA.Create(2048);
        await using var factory = CreateFactory(rsa);
        var now = DateTime.UtcNow;
        // Well beyond the default 5-minute clock skew.
        var token = CreateToken(rsa, notBefore: now.AddHours(-2), expires: now.AddHours(-1));
        using var client = CreateClient(factory, token);

        var response = await client.GetAsync(MeUri, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task GetMe_WithTokenSignedByDifferentKey_ReturnsProblemDetails401()
    {
        using var rsa = RSA.Create(2048);
        using var otherRsa = RSA.Create(2048);
        await using var factory = CreateFactory(rsa);
        using var client = CreateClient(factory, CreateToken(otherRsa));

        var response = await client.GetAsync(MeUri, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task GetMe_WithValidTokenWithoutSubject_ReturnsProblemDetails401()
    {
        using var rsa = RSA.Create(2048);
        await using var factory = CreateFactory(rsa);
        using var client = CreateClient(factory, CreateToken(rsa, subject: null));

        var response = await client.GetAsync(MeUri, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Start_WithoutFirebaseProjectId_ThrowsOptionsValidationException()
    {
        await using var factory = api.WithWebHostBuilder(builder => builder.UseSetting("Firebase:ProjectId", ""));

        Should.Throw<OptionsValidationException>(() => factory.CreateClient());
    }

    private WebApplicationFactory<Program> CreateFactory(RSA rsa) =>
        api.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.Configure<AuthenticationOptions>(o =>
            {
                o.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            });

            // A static configuration with our own signing key prevents any fetch of the Google discovery document.
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, o =>
            {
                var configuration = new OpenIdConnectConfiguration();
                var signingKey = new RsaSecurityKey(rsa) { KeyId = KeyId };
                configuration.SigningKeys.Add(signingKey);
                o.ConfigurationManager = null;
                o.Configuration = configuration;
                o.TokenValidationParameters.IssuerSigningKey = signingKey;
            });
        }));

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static string CreateToken(
        RSA rsa,
        string issuer = ValidIssuer,
        string audience = ProjectId,
        string? subject = "uid-jwt-test",
        DateTime? notBefore = null,
        DateTime? expires = null)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            NotBefore = notBefore ?? DateTime.UtcNow.AddMinutes(-1),
            Expires = expires ?? DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(rsa) { KeyId = KeyId }, SecurityAlgorithms.RsaSha256),
            Claims = subject is null ? null : new Dictionary<string, object> { ["sub"] = subject },
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}