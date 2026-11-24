using System.Globalization;

using OpoStudy.Contracts.Progress;

namespace OpoStudy.Mobile.Core.Stats;

/// <summary>State of a calendar cell. The mark and the colour always go together.</summary>
public enum CalendarDayState
{
    /// <summary>Blank cell before the first day of the month.</summary>
    Placeholder,

    /// <summary>No activity.</summary>
    Empty,

    /// <summary>Activity without meeting the goal.</summary>
    Activity,

    /// <summary>Goal met.</summary>
    GoalMet,

    /// <summary>Covered by a streak freeze.</summary>
    Protected,
}

/// <summary>A cell of the month grid.</summary>
/// <param name="Date">The day, or <c>null</c> for a blank cell.</param>
/// <param name="DayText">The day number, empty for a blank cell.</param>
/// <param name="Mark">A check, a dot, a shield or empty.</param>
/// <param name="State">What the day looked like.</param>
/// <param name="IsToday">It is today.</param>
/// <param name="IsFuture">It is after today: dimmed and not selectable.</param>
/// <param name="Description">The day detail, also its accessible description.</param>
public sealed record CalendarCell(
    DateOnly? Date,
    string DayText,
    string Mark,
    CalendarDayState State,
    bool IsToday,
    bool IsFuture,
    string Description)
{
    public bool IsPlaceholder => State == CalendarDayState.Placeholder;

    public bool HasDate => !IsPlaceholder;

    public bool IsGoalMet => State == CalendarDayState.GoalMet;

    public bool IsActivityOnly => State == CalendarDayState.Activity;

    public bool IsSelectable => !IsPlaceholder && !IsFuture;

    public bool HasMark => Mark.Length > 0;

    /// <summary>The mark, or a non-breaking space so every cell reserves the same two lines and the grid rows are even.</summary>
    public string MarkLine => HasMark ? Mark : " ";
}

/// <summary>Pure calculations of the month calendar of the statistics screen. Weeks go from Monday to Sunday.</summary>
public static class ActivityCalendar
{
    public const string GoalMetMark = "✓";

    public const string ActivityMark = "•";

    public const string ProtectedMark = "🛡️";

    public const string NoActivity = "sin actividad";

    /// <summary>Weekday headers, Monday first.</summary>
    public static IReadOnlyList<string> WeekdayInitials { get; } = ["L", "M", "X", "J", "V", "S", "D"];

    public static DateOnly MonthOf(DateOnly date) => new(date.Year, date.Month, 1);

    public static DateOnly PreviousMonth(DateOnly month) => MonthOf(month).AddMonths(-1);

    public static DateOnly NextMonth(DateOnly month) => MonthOf(month).AddMonths(1);

    public static DateOnly LastDayOf(DateOnly month) => new(month.Year, month.Month, DateTime.DaysInMonth(month.Year, month.Month));

    /// <summary>The next month is only reachable while the shown one is before the current month.</summary>
    public static bool CanGoNext(DateOnly month, DateOnly today) => MonthOf(month) < MonthOf(today);

    /// <summary>Whether the day counts as "with activity": answers, XP or a met goal. A freeze alone does not.</summary>
    public static bool HasActivity(DayActivityResponse day)
    {
        ArgumentNullException.ThrowIfNull(day);
        return day.AnsweredCount > 0 || day.XpEarned > 0 || day.GoalMet;
    }

    /// <summary>Blank cells first (Monday-based), then one cell per day of the month.</summary>
    public static IReadOnlyList<CalendarCell> BuildCells(DateOnly month, DateOnly today, IReadOnlyList<DayActivityResponse> days)
    {
        ArgumentNullException.ThrowIfNull(days);

        month = MonthOf(month);
        var byDate = new Dictionary<DateOnly, DayActivityResponse>();
        foreach (var day in days)
        {
            byDate[day.Date] = day;
        }

        var cells = new List<CalendarCell>();
        var offset = ((int)month.DayOfWeek + 6) % 7;
        for (var i = 0; i < offset; i++)
        {
            cells.Add(new CalendarCell(null, string.Empty, string.Empty, CalendarDayState.Placeholder, false, false, string.Empty));
        }

        var last = LastDayOf(month);
        for (var date = month; date <= last; date = date.AddDays(1))
        {
            var isFuture = date > today;
            byDate.TryGetValue(date, out var activity);
            var state = StateOf(activity);
            cells.Add(new CalendarCell(
                date,
                date.Day.ToString(CultureInfo.InvariantCulture),
                MarkOf(state),
                state,
                date == today,
                isFuture,
                isFuture ? StatsTexts.LongDate(date) : DayDescription(date, activity)));
        }

        return cells;
    }

    /// <summary><c>Martes 12 de noviembre: 14 preguntas de 10 · objetivo cumplido · +160 XP</c>, or the date and "sin actividad".</summary>
    public static string DayDescription(DateOnly date, DayActivityResponse? day)
    {
        var parts = new List<string>();
        if (day is not null)
        {
            if (day.AnsweredCount > 0)
            {
                parts.Add(string.Create(StatsTexts.Culture, $"{day.AnsweredCount} {(day.AnsweredCount == 1 ? "pregunta" : "preguntas")} de {day.Goal}"));
            }

            if (day.GoalMet)
            {
                parts.Add("objetivo cumplido");
            }

            if (day.Protected)
            {
                parts.Add("día protegido");
            }

            if (day.XpEarned > 0)
            {
                parts.Add(string.Create(StatsTexts.Culture, $"+{day.XpEarned} XP"));
            }
        }

        return $"{StatsTexts.LongDate(date)}: {(parts.Count == 0 ? NoActivity : string.Join(" · ", parts))}";
    }

    /// <summary><c>12 días con actividad · 9 objetivos cumplidos · +640 XP</c>, counting only the days of the month.</summary>
    public static string Summary(DateOnly month, IReadOnlyList<DayActivityResponse> days)
    {
        ArgumentNullException.ThrowIfNull(days);

        month = MonthOf(month);
        var inMonth = days.Where(d => MonthOf(d.Date) == month).ToList();
        var active = inMonth.Count(HasActivity);
        var met = inMonth.Count(d => d.GoalMet);
        var xp = inMonth.Sum(d => d.XpEarned);
        return string.Create(
            StatsTexts.Culture,
            $"{StatsTexts.Count(active, "día con actividad", "días con actividad")} · {StatsTexts.Count(met, "objetivo cumplido", "objetivos cumplidos")} · +{xp} XP");
    }

    private static CalendarDayState StateOf(DayActivityResponse? day) => day switch
    {
        null => CalendarDayState.Empty,
        { GoalMet: true } => CalendarDayState.GoalMet,
        { Protected: true } => CalendarDayState.Protected,
        _ when HasActivity(day) => CalendarDayState.Activity,
        _ => CalendarDayState.Empty,
    };

    private static string MarkOf(CalendarDayState state) => state switch
    {
        CalendarDayState.GoalMet => GoalMetMark,
        CalendarDayState.Protected => ProtectedMark,
        CalendarDayState.Activity => ActivityMark,
        _ => string.Empty,
    };
}