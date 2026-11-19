using OpoStudy.Contracts.SystemInfo;
using OpoStudy.Contracts.Users;

using Refit;

namespace OpoStudy.Mobile.Core.Api;

/// <summary>Typed client for the OpoStudy API.</summary>
public interface IOpoStudyApi
{
    [Get("/api/v1/version")]
    Task<VersionResponse> GetVersionAsync(CancellationToken cancellationToken = default);

    [Get("/api/v1/me")]
    Task<UserProfileResponse> GetMeAsync(CancellationToken cancellationToken = default);

    [Delete("/api/v1/me")]
    Task DeleteMeAsync(CancellationToken cancellationToken = default);
}
