namespace OpoStudy.Mobile.Core.Reminders;

/// <summary>Persistence of the reminder settings and the progress snapshot.</summary>
public interface IReminderStore
{
    ReminderSettings GetSettings();

    void SaveSettings(ReminderSettings settings);

    ReminderSnapshot? GetSnapshot();

    void SaveSnapshot(ReminderSnapshot snapshot);

    void ClearSnapshot();
}