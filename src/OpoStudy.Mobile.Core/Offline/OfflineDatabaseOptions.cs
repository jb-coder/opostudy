namespace OpoStudy.Mobile.Core.Offline;

/// <summary>Where the offline SQLite database lives (a temp file in tests, <c>offline.db3</c> in the app data folder in the app).</summary>
/// <param name="DatabasePath">Full path of the database file.</param>
public sealed record OfflineDatabaseOptions(string DatabasePath);