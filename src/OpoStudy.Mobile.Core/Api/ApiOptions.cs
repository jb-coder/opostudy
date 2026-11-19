namespace OpoStudy.Mobile.Core.Api;

/// <summary>Connection settings for the OpoStudy API.</summary>
/// <param name="BaseAddress">Root address of the API.</param>
public sealed record ApiOptions(Uri BaseAddress);
