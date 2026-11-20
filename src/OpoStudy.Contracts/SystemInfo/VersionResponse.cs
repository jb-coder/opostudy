namespace OpoStudy.Contracts.SystemInfo;

/// <summary>Deployed API version.</summary>
/// <param name="Version">Informational version of the assembly (SemVer).</param>
public sealed record VersionResponse(string Version);