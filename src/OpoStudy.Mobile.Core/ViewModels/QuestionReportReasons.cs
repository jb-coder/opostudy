namespace OpoStudy.Mobile.Core.ViewModels;

/// <summary>Reasons to report a question: the names the API expects and the labels the user sees.</summary>
public static class QuestionReportReasons
{
    public const string WrongAnswer = "WrongAnswer";
    public const string OutdatedLegislation = "OutdatedLegislation";
    public const string UnclearStatement = "UnclearStatement";
    public const string Typo = "Typo";
    public const string Other = "Other";

    /// <summary>The options in the order they are shown.</summary>
    public static IReadOnlyList<(string Reason, string Label)> All { get; } =
    [
        (WrongAnswer, "La respuesta correcta está mal"),
        (OutdatedLegislation, "Ley derogada o modificada"),
        (UnclearStatement, "Enunciado u opciones confusos"),
        (Typo, "Errata"),
        (Other, "Otro"),
    ];

    public static IReadOnlyList<string> Labels { get; } = [.. All.Select(r => r.Label)];

    /// <summary>The API name of the reason with that label, or <c>null</c> if there is none.</summary>
    public static string? ReasonOf(string label) => All.FirstOrDefault(r => r.Label == label).Reason;

    /// <summary>Whether the comment is mandatory for the reason.</summary>
    public static bool RequiresComment(string reason) => reason == Other;
}
