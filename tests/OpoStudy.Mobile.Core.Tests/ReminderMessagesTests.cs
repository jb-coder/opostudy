using OpoStudy.Mobile.Core.Reminders;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests;

public sealed class ReminderMessagesTests
{
    private static readonly DateOnly Today = new(2026, 11, 20);

    private static ReminderSnapshot Snapshot(DateOnly date, int answered = 0, int goal = 10, bool met = false, int streak = 0) =>
        new(date, answered, goal, met, streak);

    [Fact]
    public void For_TodayGoalMet_ReturnsNull() =>
        ReminderMessages.For(Snapshot(Today, 10, met: true, streak: 5), Today).ShouldBeNull();

    [Fact]
    public void For_TodayNotMetWithStreak_WarnsStreakAtRisk()
    {
        var message = ReminderMessages.For(Snapshot(Today, 7, streak: 5), Today);

        message.ShouldNotBeNull();
        message.Title.ShouldBe("🔥 Tu racha de 5 días está en riesgo");
        message.Text.ShouldBe("Responde 3 preguntas más para mantenerla.");
    }

    [Fact]
    public void For_TodayNotMetWithStreak_UsesSingularForOneDayAndOneQuestion()
    {
        var message = ReminderMessages.For(Snapshot(Today, 9, streak: 1), Today);

        message.ShouldNotBeNull();
        message.Title.ShouldBe("🔥 Tu racha de 1 día está en riesgo");
        message.Text.ShouldBe("Responde 1 pregunta más para mantenerla.");
    }

    [Fact]
    public void For_TodayNotMetWithoutStreak_ShowsMissingQuestions()
    {
        var message = ReminderMessages.For(Snapshot(Today, 7), Today);

        message.ShouldNotBeNull();
        message.Title.ShouldBe("🎯 Objetivo diario");
        message.Text.ShouldBe("Te faltan 3 preguntas para cumplir tu objetivo de hoy.");
    }

    [Fact]
    public void For_TodayNotMetWithoutStreak_UsesSingularForOneQuestion()
    {
        var message = ReminderMessages.For(Snapshot(Today, 9), Today);

        message.ShouldNotBeNull();
        message.Text.ShouldBe("Te faltan 1 pregunta para cumplir tu objetivo de hoy.");
    }

    [Fact]
    public void For_YesterdayGoalMetWithStreak_AsksForFullGoal()
    {
        var message = ReminderMessages.For(Snapshot(Today.AddDays(-1), 10, met: true, streak: 5), Today);

        message.ShouldNotBeNull();
        message.Title.ShouldBe("🔥 Tu racha de 5 días está en riesgo");
        message.Text.ShouldBe("Responde 10 preguntas hoy para mantenerla.");
    }

    [Fact]
    public void For_YesterdayNotMet_ReturnsGenericMessage()
    {
        var message = ReminderMessages.For(Snapshot(Today.AddDays(-1), 4, streak: 5), Today);

        message.ShouldNotBeNull();
        message.Title.ShouldBe("🎯 Objetivo diario");
        message.Text.ShouldBe("Aún no has repasado hoy. ¡Unas preguntas y listo!");
    }

    [Fact]
    public void For_YesterdayGoalMetWithoutStreak_ReturnsGenericMessage()
    {
        var message = ReminderMessages.For(Snapshot(Today.AddDays(-1), 10, met: true), Today);

        message.ShouldNotBeNull();
        message.Text.ShouldBe("Aún no has repasado hoy. ¡Unas preguntas y listo!");
    }

    [Fact]
    public void For_NoSnapshot_ReturnsGenericMessage()
    {
        var message = ReminderMessages.For(null, Today);

        message.ShouldNotBeNull();
        message.Title.ShouldBe("🎯 Objetivo diario");
        message.Text.ShouldBe("Aún no has repasado hoy. ¡Unas preguntas y listo!");
    }

    [Fact]
    public void For_OldSnapshot_ReturnsGenericMessage()
    {
        var message = ReminderMessages.For(Snapshot(Today.AddDays(-5), 10, met: true, streak: 9), Today);

        message.ShouldNotBeNull();
        message.Text.ShouldBe("Aún no has repasado hoy. ¡Unas preguntas y listo!");
    }
}