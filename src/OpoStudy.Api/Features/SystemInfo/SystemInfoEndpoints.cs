using System.Reflection;

using OpoStudy.Contracts.SystemInfo;

namespace OpoStudy.Api.Features.SystemInfo;

internal static class SystemInfoEndpoints
{
    public static RouteGroupBuilder MapSystemInfoEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/version", GetVersion)
            .WithName("GetVersion")
            .WithTags("SystemInfo")
            .WithSummary("Deployed API version")
            .AllowAnonymous();

        return api;
    }

    private static VersionResponse GetVersion()
    {
        var version = typeof(SystemInfoEndpoints).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "unknown";

        return new VersionResponse(version);
    }
}