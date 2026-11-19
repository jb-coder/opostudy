using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpoStudy.Contracts.Sistema;
using Shouldly;

namespace OpoStudy.Api.IntegrationTests;

public sealed class SistemaEndpointsTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Health_DevuelveOk()
    {
        var response = await _client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Alive_DevuelveOk()
    {
        var response = await _client.GetAsync(new Uri("/alive", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Version_DevuelveVersionDelEnsamblado()
    {
        var version = await _client.GetFromJsonAsync<VersionResponse>(
            new Uri("/api/v1/version", UriKind.Relative), TestContext.Current.CancellationToken);

        version.ShouldNotBeNull();
        version.Version.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RutaInexistente_DevuelveProblemDetails()
    {
        var response = await _client.GetAsync(new Uri("/api/v1/no-existe", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }
}
