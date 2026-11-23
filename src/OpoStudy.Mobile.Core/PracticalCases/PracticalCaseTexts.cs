using System.Globalization;

using OpoStudy.Contracts.Gradings;

namespace OpoStudy.Mobile.Core.PracticalCases;

/// <summary>User-facing texts (Spanish) and formats of the practical case screens.</summary>
public static class PracticalCaseTexts
{
    public const string NeedsConnection = "Los supuestos prácticos necesitan conexión";

    public const string OtherSection = "Otros supuestos";

    public const string NoCases = "Aún no hay supuestos para esta oposición";

    public const string DraftMark = "Borrador";

    public const string Saved = "Guardado en el dispositivo";

    public const string SubmitOffline = "No hay conexión. Tu respuesta está guardada en el dispositivo; envíala cuando vuelvas a tener red.";

    public const string StatusQueued = "En cola. Empezará en unos segundos.";

    public const string StatusProcessing = "Corrigiendo tu supuesto… Suele tardar menos de un minuto.";

    public const string StatusRetrying = "El corrector no respondía. Lo reintentaremos en unos minutos.";

    public const string StatusCompleted = "¡Corrección lista!";

    public const string CompletedDetailLater = "El detalle por apartado llegará en una próxima versión.";

    public const string FailedNotCounted = "Esta corrección no cuenta en tu cuota.";

    public const string ProviderUnavailable = "El corrector no está disponible ahora mismo.";

    public const string FailedGeneric = "No se ha podido corregir tu supuesto.";

    public const string PollOffline = "Sin conexión. Seguimos intentándolo…";

    public const string InProgressHint = "Puedes salir: la corrección sigue en el servidor. Mira el estado desde la lista de supuestos.";

    public const string ProviderUnavailableCode = "grading.provider_unavailable";

    public static CultureInfo Culture { get; } = CultureInfo.GetCultureInfo("es-ES");

    public static string Quota(GradingQuotaResponse quota)
    {
        ArgumentNullException.ThrowIfNull(quota);
        return quota.Remaining <= 0
            ? $"Has agotado tus {quota.Limit} correcciones de este mes"
            : $"Te quedan {quota.Remaining} de {quota.Limit} correcciones este mes";
    }

    public static string QuotaResets(GradingQuotaResponse quota)
    {
        ArgumentNullException.ThrowIfNull(quota);
        return $"Se renuevan el {quota.ResetsAt.ToLocalTime().ToString("d 'de' MMMM", Culture)}";
    }

    public static string QuotaShort(GradingQuotaResponse quota)
    {
        ArgumentNullException.ThrowIfNull(quota);
        return $"{quota.Remaining}/{quota.Limit} este mes";
    }

    public static string CaseDetails(int partCount, int maxPoints, int estimatedMinutes) =>
        $"{partCount} apartados · {maxPoints} puntos · ~{estimatedMinutes} min";

    public static string CaseHeader(int estimatedMinutes, int maxPoints) => $"~{estimatedMinutes} min · {maxPoints} puntos";

    public static string PartTitle(int order, int maxPoints) => $"Apartado {order + 1} · {maxPoints} puntos";

    public static string CharacterCount(int length) => $"{length} / {PracticalCaseLimits.AnswerMaxLength}";

    public static string CharacterCountNearLimit(int length) =>
        $"{CharacterCount(length)} · Te quedan {PracticalCaseLimits.AnswerMaxLength - length}";

    public static string DraftRecovered(DateTimeOffset savedAt) =>
        $"Borrador recuperado ({savedAt.ToLocalTime().ToString("d MMM HH:mm", Culture)})";

    public static string InProgressCard(string caseTitle) => $"Corrección en curso: {caseTitle}";

    public static string Submitted(DateTimeOffset submittedAt) =>
        $"Enviado {submittedAt.ToLocalTime().ToString("d MMM HH:mm", Culture)}";

    /// <summary>A score or points value with the Spanish decimal comma and no trailing zeros (<c>25</c>, <c>18,5</c>).</summary>
    public static string Points(decimal value) => value.ToString("0.##", Culture);

    public static string Score(decimal? score, int maxPoints) => $"{Points(score ?? 0)} / {maxPoints} puntos";

    public static string ScoreAlert(decimal? score, int maxPoints) => $"Has sacado {Points(score ?? 0)} de {maxPoints} puntos.";

    public static string Failure(string? failureCode) => failureCode == ProviderUnavailableCode ? ProviderUnavailable : FailedGeneric;

    public static string ConfirmSubmitMessage(GradingQuotaResponse? quota, int blankParts)
    {
        var usage = quota is null
            ? "Usarás 1 de tus correcciones de este mes."
            : $"Usarás 1 de tus {quota.Remaining} correcciones restantes de este mes.";
        var blanks = blankParts switch
        {
            <= 0 => "Los apartados en blanco puntúan 0.",
            1 => "Hay 1 apartado en blanco y puntuará 0.",
            _ => $"Hay {blankParts} apartados en blanco y puntuarán 0.",
        };
        return $"{usage} {blanks}";
    }
}