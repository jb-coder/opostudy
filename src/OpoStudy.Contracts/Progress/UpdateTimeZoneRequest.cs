namespace OpoStudy.Contracts.Progress;

/// <summary>Changes the time zone (IANA id) that defines the user's day.</summary>
public sealed record UpdateTimeZoneRequest(string TimeZone);