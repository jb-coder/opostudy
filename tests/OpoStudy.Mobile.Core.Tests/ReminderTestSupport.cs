using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using OpoStudy.Mobile.Core.Reminders;

namespace OpoStudy.Mobile.Core.Tests;

/// <summary>In-memory <see cref="IPreferencesStore"/>.</summary>
internal sealed class FakePreferencesStore : IPreferencesStore
{
    public Dictionary<string, string> Values { get; } = [];

    public string? GetString(string key) => Values.GetValueOrDefault(key);

    public void SetString(string key, string value) => Values[key] = value;

    public void Remove(string key) => Values.Remove(key);
}

/// <summary>In-memory <see cref="IReminderStore"/>.</summary>
internal sealed class FakeReminderStore : IReminderStore
{
    public ReminderSettings Settings { get; set; } = ReminderSettings.Default;

    public ReminderSnapshot? Snapshot { get; set; }

    public ReminderSettings GetSettings() => Settings;

    public void SaveSettings(ReminderSettings settings) => Settings = settings;

    public ReminderSnapshot? GetSnapshot() => Snapshot;

    public void SaveSnapshot(ReminderSnapshot snapshot) => Snapshot = snapshot;

    public void ClearSnapshot() => Snapshot = null;
}

internal static class ReminderTestSupport
{
    public static IReminderScheduler SupportedScheduler(bool permissionGranted = true)
    {
        var scheduler = Substitute.For<IReminderScheduler>();
        scheduler.IsSupported.Returns(true);
        scheduler.RequestPermissionAsync().Returns(permissionGranted);
        return scheduler;
    }

    public static ReminderService CreateService(
        IReminderStore? store = null,
        IReminderScheduler? scheduler = null,
        FakeTimeProvider? time = null) =>
        new(store ?? new FakeReminderStore(), scheduler ?? SupportedScheduler(), time ?? new FakeTimeProvider());
}