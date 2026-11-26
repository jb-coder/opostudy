using System.Globalization;

using OpoStudy.Contracts.MockExams;

namespace OpoStudy.Mobile.Core.MockExams;

/// <summary>Texts and number formats of the mock exam screens. Pure formatting, no state.</summary>
public static class MockExamTexts
{
    public const string ScreenTitle = "Simulacro de examen";

    public const string NeedsConnection = "Necesitas conexión para hacer un simulacro.";

    public const string Empty = "Esta oposición aún no tiene simulacros.";

    public const string TimeUp = "Tiempo agotado. Entregando el simulacro.";

    public const string TimeLow = "Quedan menos de 5 minutos";

    public static CultureInfo Culture { get; } = CultureInfo.GetCultureInfo("es-ES");

    /// <summary>Number as the user reads it: comma decimal, up to two decimals, no trailing zeros ("12,73", "20").</summary>
    public static string Number(decimal value) => value.ToString("0.##", Culture);

    /// <summary>Value stored in the preferences (culture independent).</summary>
    public static string StoredNumber(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    public static string Details(ExamExerciseResponse exercise)
    {
        ArgumentNullException.ThrowIfNull(exercise);

        return $"{exercise.QuestionCount} preguntas · {exercise.DurationMinutes} minutos · sobre {Number(exercise.MaxScore)} puntos";
    }

    public static string Penalty(decimal ratio) => ratio switch
    {
        0m => "Los fallos no restan",
        0.5m => "Cada fallo resta la mitad de un acierto",
        _ => $"Cada fallo resta {Number(ratio)} de un acierto",
    };

    public static string NotEnoughQuestions(int available, int required) =>
        $"Aún no hay preguntas suficientes ({available} de {required})";

    public static string StartConfirmation(int minutes, int questions) =>
        $"Tendrás {minutes} minutos para {questions} preguntas. Al acabar el tiempo se entrega solo.";

    /// <summary>"mm:ss" (minutes are not wrapped into hours: an exercise can last up to 240 minutes).</summary>
    public static string Clock(TimeSpan time)
    {
        var seconds = (int)Math.Max(0, Math.Ceiling(time.TotalSeconds));
        return string.Create(CultureInfo.InvariantCulture, $"{seconds / 60:00}:{seconds % 60:00}");
    }

    /// <summary>What the screen reader says about the time left; it changes once a minute, not every second.</summary>
    public static string RemainingDescription(TimeSpan remaining)
    {
        if (remaining <= TimeSpan.Zero)
        {
            return "Tiempo agotado";
        }

        if (remaining < TimeSpan.FromMinutes(1))
        {
            return "Queda menos de un minuto";
        }

        var minutes = (int)Math.Ceiling(remaining.TotalMinutes);
        return minutes == 1 ? "Queda 1 minuto" : $"Quedan {minutes} minutos";
    }

    public static string Announcement(int minutesLeft) =>
        minutesLeft == 1 ? "Queda 1 minuto" : $"Quedan {minutesLeft} minutos";

    public static string Progress(int number, int total, int answered) =>
        $"Pregunta {number} de {total} · {answered} {(answered == 1 ? "respondida" : "respondidas")}";

    public static string SubmitConfirmation(int blanks) => blanks switch
    {
        0 => "¿Entregar el simulacro?",
        1 => "¿Entregar el simulacro? Tienes 1 pregunta en blanco.",
        _ => $"¿Entregar el simulacro? Tienes {blanks} preguntas en blanco.",
    };

    public static string Breakdown(int correct, int incorrect, int blank) =>
        $"{correct} {(correct == 1 ? "acierto" : "aciertos")} · {incorrect} {(incorrect == 1 ? "fallo" : "fallos")} · {blank} en blanco";

    public static string NetCalculation(int correct, int incorrect, decimal penaltyRatio)
    {
        var net = correct - (incorrect * penaltyRatio);
        return $"Netas: {correct} − {incorrect} × {Number(penaltyRatio)} = {Number(net)}";
    }

    public static string ScoreLine(decimal score, decimal maxScore) => $"{Number(score)} / {Number(maxScore)}";

    public static string TimeUsed(TimeSpan used, int durationMinutes) =>
        $"Tiempo: {Clock(used)} de {Clock(TimeSpan.FromMinutes(durationMinutes))}";

    public static string Outcome(string outcome) => outcome switch
    {
        "Correct" => "Acertada",
        "Incorrect" => "Fallada",
        _ => "En blanco",
    };
}