using System.Net;

using Microsoft.AspNetCore.Hosting;

using Shouldly;

namespace OpoStudy.Api.IntegrationTests;

/// <summary>La documentación de la API (OpenAPI/Scalar) solo debe existir en Development.</summary>
public sealed class ExposicionPorEntornoTests(ApiFactory api)
{
    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar")]
    [InlineData("/scalar/v1")]
    public async Task DocumentacionApi_EnProduccion_NoSeExpone(string ruta)
    {
        await using var factory = api.WithWebHostBuilder(b => b.UseEnvironment("Production"));
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri(ruta, UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // Contraparte positiva: garantiza que las rutas del test anterior son las reales,
    // para que un 404 en Production no pase por apuntar a una ruta equivocada.
    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar/v1")]
    public async Task DocumentacionApi_EnDevelopment_SeExpone(string ruta)
    {
        await using var factory = api.WithWebHostBuilder(b => b.UseEnvironment("Development"));
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri(ruta, UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}