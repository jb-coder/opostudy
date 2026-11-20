using System.Net;

using Microsoft.AspNetCore.Hosting;

using Shouldly;

namespace OpoStudy.Api.IntegrationTests;

public sealed class RateLimitingTests(ApiFactory api)
{
    private static readonly Uri MeUri = new("/api/v1/me", UriKind.Relative);

    [Fact]
    public async Task GetMe_OverLimit_Returns429ProblemDetailsPerUser()
    {
        await using var factory = api.WithWebHostBuilder(b => b.UseSetting("RateLimiting:PermitLimit", "2"));
        using var client = ApiFactory.CreateClientFor(factory, $"uid-{Guid.NewGuid():N}");
        using var other = ApiFactory.CreateClientFor(factory, $"uid-{Guid.NewGuid():N}");

        (await client.GetAsync(MeUri, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync(MeUri, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var limited = await client.GetAsync(MeUri, TestContext.Current.CancellationToken);
        var otherUser = await other.GetAsync(MeUri, TestContext.Current.CancellationToken);

        limited.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        limited.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var body = await limited.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("Demasiadas peticiones");
        otherUser.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetMe_AnonymousOverLimit_Returns429Not401()
    {
        await using var factory = api.WithWebHostBuilder(b => b.UseSetting("RateLimiting:PermitLimit", "2"));
        using var client = factory.CreateClient();

        (await client.GetAsync(MeUri, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.GetAsync(MeUri, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var limited = await client.GetAsync(MeUri, TestContext.Current.CancellationToken);

        limited.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }
}