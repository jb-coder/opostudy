using System.Text.Json;
using System.Text.Json.Serialization;

using OpoStudy.Contracts.SystemInfo;
using OpoStudy.Contracts.Users;

namespace OpoStudy.Mobile.Core.Api;

/// <summary>
/// Source-generated JSON metadata for the API contracts (Refit 17 does not use reflection-based JSON).
/// Add every DTO the app sends or receives here.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(VersionResponse))]
[JsonSerializable(typeof(UserProfileResponse))]
public sealed partial class ApiJsonContext : JsonSerializerContext;
