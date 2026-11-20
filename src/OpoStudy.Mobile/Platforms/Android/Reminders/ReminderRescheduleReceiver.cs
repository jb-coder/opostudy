using Android.App;
using Android.Content;

using Microsoft.Extensions.DependencyInjection;

using OpoStudy.Mobile.Core.Reminders;

namespace OpoStudy.Mobile.Platforms.Android.Reminders;

/// <summary>Re-programs the alarm after a reboot, an app update or a clock or time zone change.</summary>
[BroadcastReceiver(Enabled = true, Exported = true)]
[IntentFilter(
    [
        Intent.ActionBootCompleted,
        Intent.ActionMyPackageReplaced,
        Intent.ActionTimeChanged,
        Intent.ActionTimezoneChanged,
    ])]
public sealed class ReminderRescheduleReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        // Only react to system actions; nothing is read from the intent.
        if (intent?.Action is not (Intent.ActionBootCompleted or Intent.ActionMyPackageReplaced or Intent.ActionTimeChanged or Intent.ActionTimezoneChanged))
        {
            return;
        }

        // The runtime caches the time zone: drop it so TimeZoneInfo.Local (read on every call by TimeProvider.System) is fresh.
        if (intent.Action is Intent.ActionTimezoneChanged or Intent.ActionTimeChanged)
        {
            TimeZoneInfo.ClearCachedData();
        }

        IPlatformApplication.Current?.Services.GetRequiredService<ReminderService>().EnsureScheduled();
    }
}