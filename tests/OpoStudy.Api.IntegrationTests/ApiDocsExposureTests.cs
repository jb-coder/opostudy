using System.Net;

using Microsoft.AspNetCore.Hosting;

using Shouldly;

namespace OpoStudy.Api.IntegrationTests;

/// <summary>The API documentation (OpenAPI/Scalar) must only exist in Development.</summary>
public sealed class ApiDocsExposureTests(ApiFactory api)
{
    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar")]
    [InlineData("/scalar/v1")]
    public async Task ApiDocs_InProduction_AreNotExposed(string path)
    {
        await using var factory = api.WithWebHostBuilder(b => b.UseEnvironment("Production"));
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // Positive counterpart: proves the paths above are the real ones, so a 404 in Production
    // cannot pass just because the test points to a wrong path.
    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar/v1")]
    public async Task ApiDocs_InDevelopment_AreExposed(string path)
    {
        await using var factory = api.WithWebHostBuilder(b => b.UseEnvironment("Development"));
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}