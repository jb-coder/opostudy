using Android.App;
using Android.Content;

using OpoStudy.Mobile.Core.Reminders;

namespace OpoStudy.Mobile.Platforms.Android.Reminders;

/// <summary>Daily reminder on Android: inexact one-shot alarm delivered to <see cref="ReminderReceiver"/>.</summary>
internal sealed class AndroidReminderScheduler : IReminderScheduler
{
    public bool IsSupported => true;

    public async Task<bool> RequestPermissionAsync()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            return true;
        }

        var status = await MainThread.InvokeOnMainThreadAsync(() => Permissions.RequestAsync<Permissions.PostNotifications>());
        return status == PermissionStatus.Granted;
    }

    public void Schedule(DateTimeOffset at)
    {
        var context = global::Android.App.Application.Context;
        if (context.GetSystemService(Context.AlarmService) is not AlarmManager alarms)
        {
            return;
        }

        // Windowed alarm: needs no SCHEDULE_EXACT_ALARM permission and fires within ReminderSchedule.Window.
        // SetAndAllowWhileIdle was discarded: Android 12+ gives it a window of up to one hour.
        using var operation = CreateAlarmIntent(context);
        alarms.SetWindow(AlarmType.RtcWakeup, at.ToUnixTimeMilliseconds(), (long)ReminderSchedule.Window.TotalMilliseconds, operation);
    }

    public void Cancel()
    {
        var context = global::Android.App.Application.Context;
        if (context.GetSystemService(Context.AlarmService) is not AlarmManager alarms)
        {
            return;
        }

        using var intent = CreateAlarmIntent(context);
        alarms.Cancel(intent);
    }

    private static PendingIntent CreateAlarmIntent(Context context)
    {
        var intent = new Intent(context, typeof(ReminderReceiver));
        return PendingIntent.GetBroadcast(
            context,
            ReminderNotifications.AlarmRequestCode,
            intent,
            ReminderNotifications.UpdateImmutable)!;
    }
}