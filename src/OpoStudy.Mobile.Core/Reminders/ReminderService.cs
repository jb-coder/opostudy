using OpoStudy.Contracts.Progress;

namespace OpoStudy.Mobile.Core.Reminders;

/// <summary>Orchestrates the reminder settings, the progress snapshot and the platform alarm.</summary>
public sealed class ReminderService(IReminderStore store, IReminderScheduler scheduler, TimeProvider time)
{
    public bool IsSupported => scheduler.IsSupported;

    public ReminderSettings Settings => store.GetSettings();

    private DateOnly Today => DateOnly.FromDateTime(time.GetLocalNow().DateTime);

    public void UpdateFromStreak(StreakResponse streak)
    {
        ArgumentNullException.ThrowIfNull(streak);
        store.SaveSnapshot(new ReminderSnapshot(Today, streak.AnsweredToday, streak.DailyGoal, streak.GoalMetToday, streak.CurrentStreak));
    }

    public void UpdateFromProgress(DailyProgressResponse progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        var today = Today;
        var previous = store.GetSnapshot();
        var alreadyMet = previous is { GoalMetToday: true } && previous.Date == today;
        var met = progress.GoalMetNow || progress.AnsweredToday >= progress.DailyGoal || alreadyMet;
        store.SaveSnapshot(new ReminderSnapshot(today, progress.AnsweredToday, progress.DailyGoal, met, progress.CurrentStreak));
    }

    /// <summary>Asks for the permission and, if granted, turns the reminder on. Nothing is saved when denied.</summary>
    public async Task<bool> EnableAsync(TimeOnly at)
    {
        if (!scheduler.IsSupported || !await scheduler.RequestPermissionAsync())
        {
            return false;
        }

        store.SaveSettings(new ReminderSettings(true, at));
        ScheduleNext(at);
        return true;
    }

    public void Disable()
    {
        store.SaveSettings(store.GetSettings() with { Enabled = false });
        scheduler.Cancel();
    }

    public void ChangeTime(TimeOnly at)
    {
        var settings = store.GetSettings() with { Time = at };
        store.SaveSettings(settings);
        if (settings.Enabled)
        {
            ScheduleNext(at);
        }
    }

    /// <summary>Re-programs the alarm when the reminder is on (idempotent).</summary>
    public void EnsureScheduled()
    {
        var settings = store.GetSettings();
        if (settings.Enabled && scheduler.IsSupported)
        {
            ScheduleNext(settings.Time);
        }
    }

    /// <summary>Stops reminders and drops the snapshot so another account never sees stale data. Settings are kept.</summary>
    public void OnSignedOut()
    {
        scheduler.Cancel();
        store.ClearSnapshot();
    }

    private void ScheduleNext(TimeOnly at) => scheduler.Schedule(ReminderSchedule.NextTrigger(time.GetUtcNow(), at, time.LocalTimeZone));
}