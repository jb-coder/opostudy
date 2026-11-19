using OpoStudy.Contracts.SystemInfo;

using Refit;

namespace OpoStudy.Mobile.Core.Api;

/// <summary>Typed client for the OpoStudy API.</summary>
public interface IOpoStudyApi
{
    [Get("/api/v1/version")]
    Task<VersionResponse> GetVersionAsync(CancellationToken cancellationToken = default);
}
