namespace OpoStudy.Mobile.Core.Reminders;

/// <summary>Minimal string key-value storage (backed by MAUI Preferences in the app), replaceable in tests.</summary>
public interface IPreferencesStore
{
    string? GetString(string key);

    void SetString(string key, string value);

    void Remove(string key);
}