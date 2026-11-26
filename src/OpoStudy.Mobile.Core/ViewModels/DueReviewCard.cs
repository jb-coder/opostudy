using System.Globalization;

namespace OpoStudy.Mobile.Core.ViewModels;

/// <summary>Texts of the "Repaso de hoy" (spaced review) card. Pure formatting, no state.</summary>
public static class DueReviewCard
{
    /// <summary>Title of the review quiz the card creates.</summary>
    public const string QuizTitle = "Repaso de hoy";

    /// <summary>Most questions of one review quiz.</summary>
    public const int MaxQuestions = 20;

    /// <summary>"1 pregunta para repasar hoy" / "{n} preguntas para repasar hoy".</summary>
    public static string CountText(int total) => total == 1
        ? "1 pregunta para repasar hoy"
        : string.Create(CultureInfo.InvariantCulture, $"{total} preguntas para repasar hoy");

    /// <summary>Number of questions of the review quiz for an exam with <paramref name="count"/> due questions.</summary>
    public static int QuestionCount(int count) => Math.Min(count, MaxQuestions);
}