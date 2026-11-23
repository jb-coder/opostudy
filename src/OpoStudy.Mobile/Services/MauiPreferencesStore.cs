using OpoStudy.Mobile.Core.Reminders;

namespace OpoStudy.Mobile.Services;

/// <summary><see cref="IPreferencesStore"/> backed by MAUI <see cref="IPreferences"/>.</summary>
internal sealed class MauiPreferencesStore(IPreferences preferences) : IPreferencesStore
{
    public string? GetString(string key) => preferences.ContainsKey(key) ? preferences.Get<string?>(key, null) : null;

    public void SetString(string key, string value) => preferences.Set(key, value);

    public void Remove(string key) => preferences.Remove(key);
}