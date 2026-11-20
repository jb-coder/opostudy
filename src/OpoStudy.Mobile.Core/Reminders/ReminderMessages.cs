using System.Globalization;

namespace OpoStudy.Mobile.Core.Reminders;

/// <summary>Title and body of the reminder notification.</summary>
public sealed record ReminderMessage(string Title, string Text);

/// <summary>Chooses the notification text from the last progress snapshot.</summary>
public static class ReminderMessages
{
    private const string GoalTitle = "🎯 Objetivo diario";

    /// <summary>The message to show, or <c>null</c> when today's goal is already met.</summary>
    public static ReminderMessage? For(ReminderSnapshot? snapshot, DateOnly today)
    {
        if (snapshot is not null && snapshot.Date == today)
        {
            if (snapshot.GoalMetToday)
            {
                return null;
            }

            var remaining = Math.Max(snapshot.DailyGoal - snapshot.AnsweredToday, 1);
            return snapshot.CurrentStreak > 0
                ? new ReminderMessage(AtRiskTitle(snapshot.CurrentStreak), $"Responde {Questions(remaining)} más para mantenerla.")
                : new ReminderMessage(GoalTitle, $"Te faltan {Questions(remaining)} para cumplir tu objetivo de hoy.");
        }

        if (snapshot is { GoalMetToday: true, CurrentStreak: > 0 } && snapshot.Date == today.AddDays(-1))
        {
            return new ReminderMessage(AtRiskTitle(snapshot.CurrentStreak), $"Responde {Questions(snapshot.DailyGoal)} hoy para mantenerla.");
        }

        return new ReminderMessage(GoalTitle, "Aún no has repasado hoy. ¡Unas preguntas y listo!");
    }

    private static string AtRiskTitle(int streak) =>
        string.Create(CultureInfo.InvariantCulture, $"🔥 Tu racha de {streak} {(streak == 1 ? "día" : "días")} está en riesgo");

    private static string Questions(int count) =>
        string.Create(CultureInfo.InvariantCulture, $"{count} {(count == 1 ? "pregunta" : "preguntas")}");
}