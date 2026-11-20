namespace OpoStudy.Mobile.Core.Reminders;

/// <summary>Persistence of the reminder settings and the progress snapshot.</summary>
public interface IReminderStore
{
    ReminderSettings GetSettings();

    void SaveSettings(ReminderSettings settings);

    ReminderSnapshot? GetSnapshot();

    void SaveSnapshot(ReminderSnapshot snapshot);

    void ClearSnapshot();

    /// <summary>Whether reminders are suspended because nobody is signed in.</summary>
    bool GetSuspended();

    void SetSuspended(bool suspended);

    /// <summary>Local date of the last alarm that fired, if any.</summary>
    DateOnly? GetLastFiredOn();

    void SaveLastFiredOn(DateOnly day);
}
