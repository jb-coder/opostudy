using System.Reflection;
using OpoStudy.Contracts.Sistema;

namespace OpoStudy.Api.Features.Sistema;

internal static class SistemaEndpoints
{
    public static RouteGroupBuilder MapSistemaEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/version", ObtenerVersion)
            .WithName("ObtenerVersion")
            .WithTags("Sistema")
            .WithSummary("Versión desplegada de la API");

        return api;
    }

    private static VersionResponse ObtenerVersion()
    {
        var version = typeof(SistemaEndpoints).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "desconocida";

        return new VersionResponse(version);
    }
}
