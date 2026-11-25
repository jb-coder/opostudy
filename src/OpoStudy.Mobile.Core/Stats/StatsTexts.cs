using System.Globalization;

using OpoStudy.Contracts.Stats;

namespace OpoStudy.Mobile.Core.Stats;

/// <summary>User-facing texts (Spanish) and formats of the statistics screen. Pure formatting, no state.</summary>
public static class StatsTexts
{
    public const string Title = "Estadísticas";

    public const string Loading = "Cargando estadísticas…";

    public const string NoData = "Aún no hay estadísticas. Haz tu primer test y aquí verás tu progreso.";

    public const string TakeQuiz = "Hacer un test";

    public const string NoAnswers = "Aún no has respondido preguntas.";

    public const string NoGradings = "Aún no tienes supuestos corregidos.";

    public const string NoExams = "No hay oposiciones disponibles.";

    public const string NeedsConnection = "Las estadísticas necesitan conexión";

    public const string OfflineWithData = "Sin conexión. Se muestran los últimos datos cargados.";

    public const string ActivityFailed = "No se ha podido cargar la actividad.";

    public const string PracticeOffline = "No hay conexión. Puedes hacer un test sin conexión desde «Hacer un test».";

    public const string Retry = "Reintentar";

    public const string LoadingActivity = "Cargando actividad…";

    public const string ExamLabel = "Oposición";

    public const string PreviousGlyph = "‹";

    public const string NextGlyph = "›";

    public const string AnsweredLabel = "Respondidas";

    public const string AccuracyLabel = "Aciertos";

    public const string QuizzesLabel = "Tests completados";

    public const string TopicsLabel = "Temas practicados";

    public const string TopicsSection = "Por tema";

    public const string PracticeTopic = "Practicar este tema";

    public const string ShowFewer = "Ver menos";

    public const string ActivitySection = "Actividad";

    public const string PreviousMonth = "Mes anterior";

    public const string NextMonth = "Mes siguiente";

    public const string Legend = "✓ objetivo cumplido · • con actividad · 🛡️ protegido";

    public const string GradingsSection = "Supuestos";

    public const string BestScores = "Mejor nota por supuesto";

    public const string NoValue = "—";

    /// <summary>Accessible name of the practice button of a topic, e.g. <c>Practicar Tema 3. La Constitución</c>.</summary>
    public static string PracticeTopicDescription(string title) => $"Practicar {title}";

    /// <summary>The topics shown before "see all".</summary>
    public const int CollapsedTopicCount = 5;

    /// <summary>Questions of the quiz created from a weak topic.</summary>
    public const int PracticeQuestionCount = 10;

    private static readonly string[] MonthNames =
    [
        "enero", "febrero", "marzo", "abril", "mayo", "junio",
        "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre",
    ];

    private static readonly string[] MonthAbbreviations =
    [
        "ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic",
    ];

    private static readonly string[] WeekdayNames =
    [
        "Domingo", "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado",
    ];

    public static CultureInfo Culture { get; } = CultureInfo.GetCultureInfo("es-ES");

    /// <summary>Whole-number percentage rounded away from zero, e.g. <c>72 %</c>; a dash when nothing was answered.</summary>
    public static string Percent(int correct, int answered) =>
        answered <= 0 ? NoValue : string.Create(Culture, $"{PercentValue(correct, answered)} %");

    /// <summary>Rounded percentage of correct answers, 0 when nothing was answered.</summary>
    public static int PercentValue(int correct, int answered) =>
        answered <= 0 ? 0 : (int)Math.Round(correct * 100m / answered, MidpointRounding.AwayFromZero);

    /// <summary>Fraction (0 to 1) for a progress bar.</summary>
    public static double Fraction(int correct, int answered) =>
        answered <= 0 ? 0 : Math.Clamp((double)correct / answered, 0, 1);

    /// <summary><c>5 de 12</c>.</summary>
    public static string TopicsPracticed(int practiced, int available) => string.Create(Culture, $"{practiced} de {available}");

    /// <summary><c>72 % · 18 de 25</c>.</summary>
    public static string TopicScore(int correct, int answered) => string.Create(Culture, $"{Percent(correct, answered)} · {correct} de {answered}");

    /// <summary><c>Última práctica: 12 nov 2026</c>, in the given time zone.</summary>
    public static string LastPractice(DateTimeOffset at, TimeZoneInfo zone) => $"Última práctica: {ShortDate(LocalDate(at, zone))}";

    /// <summary><c>Ver todos los temas (12)</c>.</summary>
    public static string ShowAllTopics(int count) => string.Create(Culture, $"Ver todos los temas ({count})");

    public static DateOnly LocalDate(DateTimeOffset at, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, zone).DateTime);
    }

    /// <summary><c>12 nov 2026</c>.</summary>
    public static string ShortDate(DateOnly date) => string.Create(Culture, $"{date.Day} {MonthAbbreviations[date.Month - 1]} {date.Year}");

    /// <summary><c>12/11</c> (day and month), used under the chart bars.</summary>
    public static string DayMonth(DateOnly date) => string.Create(Culture, $"{date.Day}/{date.Month}");

    /// <summary><c>Martes 12 de noviembre</c>.</summary>
    public static string LongDate(DateOnly date) =>
        string.Create(Culture, $"{WeekdayNames[(int)date.DayOfWeek]} {date.Day} de {MonthNames[date.Month - 1]}");

    /// <summary><c>Noviembre de 2026</c>.</summary>
    public static string MonthTitle(DateOnly month) =>
        string.Create(Culture, $"{char.ToUpper(MonthNames[month.Month - 1][0], Culture)}{MonthNames[month.Month - 1][1..]} de {month.Year}");

    /// <summary>A count with its singular or plural noun, e.g. <c>1 intento</c> / <c>2 intentos</c>.</summary>
    public static string Count(int count, string singular, string plural) => string.Create(Culture, $"{count} {(count == 1 ? singular : plural)}");

    /// <summary><c>3 correcciones · media 74,5 %</c>.</summary>
    public static string GradingsSummary(PracticalCaseStatsResponse stats)
    {
        ArgumentNullException.ThrowIfNull(stats);
        var count = Count(stats.CompletedCount, "corrección", "correcciones");
        return stats.AveragePercent is { } average ? $"{count} · media {AveragePercent(average)}" : count;
    }

    /// <summary>The average with at most one decimal and a decimal comma, e.g. <c>74,5 %</c>.</summary>
    public static string AveragePercent(decimal percent) => string.Create(Culture, $"{percent:0.#} %");

    /// <summary>A percentage rounded to the whole number, e.g. <c>74 %</c>.</summary>
    public static string WholePercent(decimal percent) =>
        string.Create(Culture, $"{Math.Round(percent, MidpointRounding.AwayFromZero):0} %");

    /// <summary>A score with the decimal comma and no trailing zeros, e.g. <c>18,5</c>.</summary>
    public static string Points(decimal value) => value.ToString("0.##", Culture);

    /// <summary><c>18,5 / 25 · 74 %</c>.</summary>
    public static string BestScore(CaseBestScoreResponse best)
    {
        ArgumentNullException.ThrowIfNull(best);
        return $"{Points(best.Score)} / {best.MaxPoints} · {WholePercent(best.Percent)}";
    }

    /// <summary><c>1 intento</c> / <c>2 intentos</c>.</summary>
    public static string Attempts(int count) => Count(count, "intento", "intentos");
}