namespace OpoStudy.Mobile.Core;

/// <summary>Build-time flags of the app, injected so ViewModels never use conditional compilation.</summary>
/// <param name="IsDebug">True in debug builds, where developer tools such as the style catalog are available.</param>
public sealed record AppEnvironment(bool IsDebug);