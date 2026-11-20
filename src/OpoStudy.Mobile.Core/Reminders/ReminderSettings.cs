namespace OpoStudy.Mobile.Core.Reminders;

/// <summary>Device-level reminder preferences.</summary>
public sealed record ReminderSettings(bool Enabled, TimeOnly Time)
{
    /// <summary>Time used until the user picks another one.</summary>
    public static readonly TimeOnly DefaultTime = new(20, 0);

    /// <summary>The only times the user can choose.</summary>
    public static IReadOnlyList<TimeOnly> AllowedTimes { get; } =
        [new(18, 0), new(19, 0), new(20, 0), new(21, 0), new(22, 0)];

    public static ReminderSettings Default { get; } = new(false, DefaultTime);
}