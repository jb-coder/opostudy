using System.Net;
using System.Net.Http.Json;

using OpoStudy.Contracts.SystemInfo;

using Shouldly;

namespace OpoStudy.Api.IntegrationTests;

public sealed class SystemInfoEndpointsTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Alive_ReturnsOk()
    {
        var response = await _client.GetAsync(new Uri("/alive", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Version_ReturnsAssemblyVersion()
    {
        var version = await _client.GetFromJsonAsync<VersionResponse>(
            new Uri("/api/v1/version", UriKind.Relative), TestContext.Current.CancellationToken);

        version.ShouldNotBeNull();
        version.Version.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task UnknownRoute_ReturnsProblemDetails()
    {
        var response = await _client.GetAsync(new Uri("/api/v1/does-not-exist", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }
}