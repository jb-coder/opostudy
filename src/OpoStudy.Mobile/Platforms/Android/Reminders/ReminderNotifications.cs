using Android.App;
using Android.Content;
using Android.OS;

namespace OpoStudy.Mobile.Platforms.Android.Reminders;

/// <summary>Notification channel and shared constants of the daily reminder.</summary>
internal static class ReminderNotifications
{
    public const string ChannelId = "daily_reminder";

    public const int NotificationId = 4001;

    public const int AlarmRequestCode = 4002;

    /// <summary>FLAG_UPDATE_CURRENT, plus FLAG_IMMUTABLE where it exists (API 23+; older versions are immutable by default).</summary>
    public static PendingIntentFlags UpdateImmutable =>
        OperatingSystem.IsAndroidVersionAtLeast(23) ? PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable : PendingIntentFlags.UpdateCurrent;

    /// <summary>Creates the channel; idempotent, so it is safe to call on every start.</summary>
    public static void EnsureChannel(Context context)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            return;
        }

        if (context.GetSystemService(Context.NotificationService) is not NotificationManager manager)
        {
            return;
        }

        var channel = new NotificationChannel(ChannelId, "Recordatorio diario", NotificationImportance.Default)
        {
            Description = "Aviso diario si aún no has cumplido el objetivo del día",
        };
        manager.CreateNotificationChannel(channel);
    }
}