using Android.App;
using Android.Content;

using AndroidX.Core.App;

using Microsoft.Extensions.DependencyInjection;

using OpoStudy.Mobile.Core.Reminders;

namespace OpoStudy.Mobile.Platforms.Android.Reminders;

/// <summary>Receives the daily alarm, shows the notification when the goal is not met and schedules the next day.</summary>
[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class ReminderReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || IPlatformApplication.Current?.Services is not { } services)
        {
            return;
        }

        var store = services.GetRequiredService<IReminderStore>();
        var settings = store.GetSettings();
        if (!settings.Enabled)
        {
            return;
        }

        var time = services.GetRequiredService<TimeProvider>();
        var today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);
        var message = ReminderMessages.For(store.GetSnapshot(), today);
        if (message is not null && NotificationManagerCompat.From(context)!.AreNotificationsEnabled())
        {
            Show(context, message);
        }

        // One-shot alarm: always program the next day.
        services.GetRequiredService<ReminderService>().EnsureScheduled();
    }

    private static void Show(Context context, ReminderMessage message)
    {
        ReminderNotifications.EnsureChannel(context);

        var openApp = new Intent(context, typeof(MainActivity));
        openApp.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
        using var tap = PendingIntent.GetActivity(
            context,
            ReminderNotifications.NotificationId,
            openApp,
            ReminderNotifications.UpdateImmutable);

        var builder = new NotificationCompat.Builder(context, ReminderNotifications.ChannelId);
        builder.SetSmallIcon(Resource.Drawable.ic_notification);
        builder.SetContentTitle(message.Title);
        builder.SetContentText(message.Text);
        builder.SetContentIntent(tap);
        builder.SetAutoCancel(true);
        var notification = builder.Build()!;

        // Permission is checked through AreNotificationsEnabled() just before.
        NotificationManagerCompat.From(context)!.Notify(ReminderNotifications.NotificationId, notification);
    }
}