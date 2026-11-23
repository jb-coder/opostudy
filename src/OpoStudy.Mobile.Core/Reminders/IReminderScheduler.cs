namespace OpoStudy.Mobile.Core.Reminders;

/// <summary>Platform side of the reminder: notification permission and the one-shot alarm.</summary>
public interface IReminderScheduler
{
    bool IsSupported { get; }

    /// <summary>Asks for the notification permission when the platform requires it; returns whether it is granted.</summary>
    Task<bool> RequestPermissionAsync();

    /// <summary>Schedules a single alarm at <paramref name="at"/>, replacing any previous one.</summary>
    void Schedule(DateTimeOffset at);

    void Cancel();
}

/// <summary>Scheduler for platforms without reminder support.</summary>
public sealed class NullReminderScheduler : IReminderScheduler
{
    public bool IsSupported => false;

    public Task<bool> RequestPermissionAsync() => Task.FromResult(false);

    public void Schedule(DateTimeOffset at)
    {
    }

    public void Cancel()
    {
    }
}