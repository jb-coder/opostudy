using OpoStudy.Mobile.Core.Reminders;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests;

public sealed class ReminderScheduleTests
{
    private static readonly TimeZoneInfo Madrid = TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid");

    private static readonly TimeOnly Eight = new(20, 0);

    [Fact]
    public void NextTrigger_BeforeTime_ReturnsToday()
    {
        var now = new DateTimeOffset(2026, 11, 20, 10, 0, 0, TimeSpan.FromHours(1));

        ReminderSchedule.NextTrigger(now, Eight, Madrid).ShouldBe(new DateTimeOffset(2026, 11, 20, 20, 0, 0, TimeSpan.FromHours(1)));
    }

    [Fact]
    public void NextTrigger_AfterTime_ReturnsTomorrow()
    {
        var now = new DateTimeOffset(2026, 11, 20, 21, 0, 0, TimeSpan.FromHours(1));

        ReminderSchedule.NextTrigger(now, Eight, Madrid).ShouldBe(new DateTimeOffset(2026, 11, 21, 20, 0, 0, TimeSpan.FromHours(1)));
    }

    [Fact]
    public void NextTrigger_ExactlyAtTime_ReturnsTomorrow()
    {
        var now = new DateTimeOffset(2026, 11, 20, 20, 0, 0, TimeSpan.FromHours(1));

        ReminderSchedule.NextTrigger(now, Eight, Madrid).ShouldBe(new DateTimeOffset(2026, 11, 21, 20, 0, 0, TimeSpan.FromHours(1)));
    }

    [Fact]
    public void NextTrigger_NowInOtherOffset_UsesZoneDay()
    {
        // 23:30 UTC on the 20th is already 00:30 on the 21st in Madrid.
        var now = new DateTimeOffset(2026, 11, 20, 23, 30, 0, TimeSpan.Zero);

        ReminderSchedule.NextTrigger(now, Eight, Madrid).ShouldBe(new DateTimeOffset(2026, 11, 21, 20, 0, 0, TimeSpan.FromHours(1)));
    }

    [Fact]
    public void NextTrigger_SpringForwardDay_UsesSummerOffset()
    {
        // Clocks go forward on 2026-03-29: 20:00 that day is UTC+2.
        var now = new DateTimeOffset(2026, 3, 29, 0, 30, 0, TimeSpan.FromHours(1));

        ReminderSchedule.NextTrigger(now, Eight, Madrid).ShouldBe(new DateTimeOffset(2026, 3, 29, 20, 0, 0, TimeSpan.FromHours(2)));
    }

    [Fact]
    public void NextTrigger_TimeSkippedBySpringForward_UsesNextValidTime()
    {
        var now = new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.FromHours(1));

        var next = ReminderSchedule.NextTrigger(now, new TimeOnly(2, 30), Madrid);

        next.ShouldBe(new DateTimeOffset(2026, 3, 29, 3, 0, 0, TimeSpan.FromHours(2)));
    }

    [Fact]
    public void NextTrigger_FallBackDay_UsesWinterOffset()
    {
        // Clocks go back on 2026-10-25: 20:00 that day is UTC+1.
        var now = new DateTimeOffset(2026, 10, 24, 21, 0, 0, TimeSpan.FromHours(2));

        ReminderSchedule.NextTrigger(now, Eight, Madrid).ShouldBe(new DateTimeOffset(2026, 10, 25, 20, 0, 0, TimeSpan.FromHours(1)));
    }
}