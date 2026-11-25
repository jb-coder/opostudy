using OpoStudy.Contracts.Progress;
using OpoStudy.Mobile.Core.Stats;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests;

public sealed class ActivityCalendarTests
{
    private static readonly DateOnly Today = new(2026, 11, 24);

    private static DayActivityResponse Day(
        DateOnly date,
        int answered = 0,
        int goal = 10,
        bool met = false,
        bool isProtected = false,
        int xp = 0) => new(date, answered, goal, met, isProtected, xp);

    [Theory]
    [InlineData(2026, 6, 0)] // 1 June 2026 is a Monday
    [InlineData(2026, 4, 2)] // Wednesday
    [InlineData(2026, 11, 6)] // Sunday
    public void BuildCells_FirstDayOfMonth_AddsBlanksUntilMonday(int year, int month, int expectedBlanks)
    {
        var cells = ActivityCalendar.BuildCells(new DateOnly(year, month, 1), Today, []);

        cells.Count(c => c.IsPlaceholder).ShouldBe(expectedBlanks);
        cells.Take(expectedBlanks).ShouldAllBe(c => c.IsPlaceholder && c.Date == null && c.DayText == string.Empty);
        cells[expectedBlanks].Date.ShouldBe(new DateOnly(year, month, 1));
    }

    [Theory]
    [InlineData(2026, 2, 28)]
    [InlineData(2028, 2, 29)]
    [InlineData(2026, 4, 30)]
    [InlineData(2026, 12, 31)]
    public void BuildCells_MonthLength_HasOneCellPerDay(int year, int month, int days)
    {
        var cells = ActivityCalendar.BuildCells(new DateOnly(year, month, 1), Today, []);

        cells.Count(c => !c.IsPlaceholder).ShouldBe(days);
        cells[^1].Date.ShouldBe(new DateOnly(year, month, days));
    }

    [Fact]
    public void BuildCells_AnyDateOfMonth_UsesItsFirstDay()
    {
        var cells = ActivityCalendar.BuildCells(new DateOnly(2026, 11, 17), Today, []);

        cells.First(c => !c.IsPlaceholder).Date.ShouldBe(new DateOnly(2026, 11, 1));
    }

    [Fact]
    public void BuildCells_GoalMetDay_ShowsCheckMark()
    {
        var day = Day(new DateOnly(2026, 11, 12), answered: 14, met: true, xp: 160);

        var cell = Cell(day);

        cell.Mark.ShouldBe("✓");
        cell.State.ShouldBe(CalendarDayState.GoalMet);
        cell.IsGoalMet.ShouldBeTrue();
    }

    [Fact]
    public void BuildCells_ActivityWithoutGoal_ShowsDot()
    {
        var cell = Cell(Day(new DateOnly(2026, 11, 12), answered: 3, xp: 30));

        cell.Mark.ShouldBe("•");
        cell.IsActivityOnly.ShouldBeTrue();
    }

    [Fact]
    public void BuildCells_ProtectedDay_ShowsShield()
    {
        var cell = Cell(Day(new DateOnly(2026, 11, 12), isProtected: true));

        cell.Mark.ShouldBe("🛡️");
        cell.State.ShouldBe(CalendarDayState.Protected);
    }

    [Fact]
    public void BuildCells_DayWithoutRow_HasNoMark()
    {
        var cell = ActivityCalendar.BuildCells(new DateOnly(2026, 11, 1), Today, [])
            .Single(c => c.Date == new DateOnly(2026, 11, 12));

        cell.Mark.ShouldBeEmpty();
        cell.State.ShouldBe(CalendarDayState.Empty);
        cell.Description.ShouldBe("Jueves 12 de noviembre: sin actividad");
    }

    [Fact]
    public void BuildCells_Today_IsMarkedAndDaysAfterAreFuture()
    {
        var cells = ActivityCalendar.BuildCells(new DateOnly(2026, 11, 1), Today, []);

        cells.Single(c => c.IsToday).Date.ShouldBe(Today);
        cells.Where(c => c.IsFuture).Select(c => c.Date).ShouldBe(
            Enumerable.Range(25, 6).Select(d => (DateOnly?)new DateOnly(2026, 11, d)));
        cells.Where(c => c.IsFuture).ShouldAllBe(c => !c.IsSelectable);
        cells.Single(c => c.IsToday).IsSelectable.ShouldBeTrue();
    }

    [Fact]
    public void BuildCells_PastMonth_HasNoTodayNorFutureDays()
    {
        var cells = ActivityCalendar.BuildCells(new DateOnly(2026, 10, 1), Today, []);

        cells.ShouldAllBe(c => !c.IsToday && !c.IsFuture);
    }

    [Fact]
    public void BuildCells_FutureMonth_AllDaysAreFuture()
    {
        var cells = ActivityCalendar.BuildCells(new DateOnly(2026, 12, 1), Today, []);

        cells.Where(c => !c.IsPlaceholder).ShouldAllBe(c => c.IsFuture);
    }

    [Fact]
    public void BuildCells_DaysOfOtherMonths_AreIgnored()
    {
        var cells = ActivityCalendar.BuildCells(
            new DateOnly(2026, 11, 1),
            Today,
            [Day(new DateOnly(2026, 10, 31), answered: 5, met: true), Day(new DateOnly(2026, 12, 1), answered: 5, met: true)]);

        cells.Where(c => !c.IsPlaceholder).ShouldAllBe(c => c.Mark.Length == 0);
    }

    [Fact]
    public void DayDescription_FullDay_ListsQuestionsGoalAndXp()
    {
        var text = ActivityCalendar.DayDescription(
            new DateOnly(2026, 11, 12),
            Day(new DateOnly(2026, 11, 12), answered: 14, goal: 10, met: true, xp: 160));

        text.ShouldBe("Jueves 12 de noviembre: 14 preguntas de 10 · objetivo cumplido · +160 XP");
    }

    [Fact]
    public void DayDescription_OneQuestion_UsesSingular()
    {
        var text = ActivityCalendar.DayDescription(new DateOnly(2026, 11, 2), Day(new DateOnly(2026, 11, 2), answered: 1, goal: 10));

        text.ShouldBe("Lunes 2 de noviembre: 1 pregunta de 10");
    }

    [Fact]
    public void DayDescription_ProtectedDay_SaysItWithoutQuestions()
    {
        var text = ActivityCalendar.DayDescription(new DateOnly(2026, 11, 3), Day(new DateOnly(2026, 11, 3), isProtected: true));

        text.ShouldBe("Martes 3 de noviembre: día protegido");
    }

    [Fact]
    public void DayDescription_NoRow_SaysNoActivity() =>
        ActivityCalendar.DayDescription(new DateOnly(2026, 11, 3), null).ShouldBe("Martes 3 de noviembre: sin actividad");

    [Fact]
    public void BuildCells_FutureDay_DescriptionIsOnlyTheDate()
    {
        var cell = ActivityCalendar.BuildCells(new DateOnly(2026, 11, 1), Today, []).Single(c => c.Date == new DateOnly(2026, 11, 30));

        cell.Description.ShouldBe("Lunes 30 de noviembre");
    }

    [Fact]
    public void Summary_Month_CountsActiveDaysGoalsAndXp()
    {
        var days = new[]
        {
            Day(new DateOnly(2026, 11, 2), answered: 12, met: true, xp: 200),
            Day(new DateOnly(2026, 11, 3), answered: 4, xp: 40),
            Day(new DateOnly(2026, 11, 4), isProtected: true),
            Day(new DateOnly(2026, 11, 5), xp: 10),
            Day(new DateOnly(2026, 10, 30), answered: 9, met: true, xp: 999),
        };

        ActivityCalendar.Summary(new DateOnly(2026, 11, 1), days)
            .ShouldBe("3 días con actividad · 1 objetivo cumplido · +250 XP");
    }

    [Fact]
    public void Summary_OneDay_UsesSingulars()
    {
        ActivityCalendar.Summary(new DateOnly(2026, 11, 1), [Day(new DateOnly(2026, 11, 2), answered: 12, met: true, xp: 160)])
            .ShouldBe("1 día con actividad · 1 objetivo cumplido · +160 XP");
    }

    [Fact]
    public void Summary_NoDays_IsZeros() =>
        ActivityCalendar.Summary(new DateOnly(2026, 11, 1), []).ShouldBe("0 días con actividad · 0 objetivos cumplidos · +0 XP");

    [Fact]
    public void Summary_OnlyProtectedDay_DoesNotCountAsActive() =>
        ActivityCalendar.Summary(new DateOnly(2026, 11, 1), [Day(new DateOnly(2026, 11, 4), isProtected: true)])
            .ShouldBe("0 días con actividad · 0 objetivos cumplidos · +0 XP");

    [Theory]
    [InlineData(2026, 10, true)]
    [InlineData(2026, 11, false)]
    [InlineData(2026, 12, false)]
    [InlineData(2025, 12, true)]
    public void CanGoNext_Month_OnlyBeforeCurrentMonth(int year, int month, bool expected) =>
        ActivityCalendar.CanGoNext(new DateOnly(year, month, 1), Today).ShouldBe(expected);

    [Fact]
    public void PreviousMonth_January_GoesToDecemberOfPreviousYear() =>
        ActivityCalendar.PreviousMonth(new DateOnly(2026, 1, 1)).ShouldBe(new DateOnly(2025, 12, 1));

    [Fact]
    public void NextMonth_December_GoesToJanuaryOfNextYear() =>
        ActivityCalendar.NextMonth(new DateOnly(2026, 12, 1)).ShouldBe(new DateOnly(2027, 1, 1));

    [Fact]
    public void LastDayOf_LeapFebruary_Is29() =>
        ActivityCalendar.LastDayOf(new DateOnly(2028, 2, 1)).ShouldBe(new DateOnly(2028, 2, 29));

    [Fact]
    public void WeekdayInitials_StartOnMonday() =>
        ActivityCalendar.WeekdayInitials.ShouldBe(["L", "M", "X", "J", "V", "S", "D"]);

    private static CalendarCell Cell(DayActivityResponse day) =>
        ActivityCalendar.BuildCells(new DateOnly(2026, 11, 1), Today, [day]).Single(c => c.Date == day.Date);
}