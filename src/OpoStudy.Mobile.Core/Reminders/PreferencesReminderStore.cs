using System.Globalization;
using System.Text.Json;

namespace OpoStudy.Mobile.Core.Reminders;

/// <summary><see cref="IReminderStore"/> over <see cref="IPreferencesStore"/>. Corrupt values fall back to defaults.</summary>
public sealed class PreferencesReminderStore(IPreferencesStore preferences) : IReminderStore
{
    internal const string EnabledKey = "reminder.enabled";

    internal const string TimeKey = "reminder.time";

    internal const string SnapshotKey = "reminder.snapshot";

    internal const string SuspendedKey = "reminder.suspended";

    internal const string LastFiredOnKey = "reminder.lastFiredOn";

    private const string DateFormat = "yyyy-MM-dd";

    private const string TimeFormat = "HH:mm";

    public ReminderSettings GetSettings()
    {
        var enabled = bool.TryParse(preferences.GetString(EnabledKey), out var e) && e;
        var time = TimeOnly.TryParseExact(preferences.GetString(TimeKey), TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)
            ? t
            : ReminderSettings.DefaultTime;
        return new ReminderSettings(enabled, time);
    }

    public void SaveSettings(ReminderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        preferences.SetString(EnabledKey, settings.Enabled ? "true" : "false");
        preferences.SetString(TimeKey, settings.Time.ToString(TimeFormat, CultureInfo.InvariantCulture));
    }

    public ReminderSnapshot? GetSnapshot()
    {
        var json = preferences.GetString(SnapshotKey);
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(json, ReminderJsonContext.Default.ReminderSnapshot);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void SaveSnapshot(ReminderSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        preferences.SetString(SnapshotKey, JsonSerializer.Serialize(snapshot, ReminderJsonContext.Default.ReminderSnapshot));
    }

    public void ClearSnapshot() => preferences.Remove(SnapshotKey);

    public bool GetSuspended() => bool.TryParse(preferences.GetString(SuspendedKey), out var s) && s;

    public void SetSuspended(bool suspended) => preferences.SetString(SuspendedKey, suspended ? "true" : "false");

    public DateOnly? GetLastFiredOn() =>
        DateOnly.TryParseExact(preferences.GetString(LastFiredOnKey), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : null;

    public void SaveLastFiredOn(DateOnly day) =>
        preferences.SetString(LastFiredOnKey, day.ToString(DateFormat, CultureInfo.InvariantCulture));
}