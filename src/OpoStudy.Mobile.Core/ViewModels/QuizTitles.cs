using System.Globalization;

using OpoStudy.Contracts.Quizzes;

namespace OpoStudy.Mobile.Core.ViewModels;

/// <summary>Titles of a quiz and texts about mistakes (reviews). Pure formatting, no state.</summary>
public static class QuizTitles
{
    public const string RandomQuiz = "Test aleatorio";
    public const string ReviewAll = "Repaso de falladas";
    public const string ReviewMode = "Review";
    public const string MockExamMode = "MockExam";
    public const string MockExam = "Simulacro";

    /// <summary>Title of a quiz about one topic; a review of that topic when <paramref name="mistakesOnly"/>.</summary>
    public static string ForTopic(string topicTitle, bool mistakesOnly) =>
        mistakesOnly ? $"Repaso: {topicTitle}" : topicTitle;

    /// <summary>Title of a quiz over the whole exam.</summary>
    public static string ForExam(bool mistakesOnly) => mistakesOnly ? ReviewAll : RandomQuiz;

    /// <summary>Title of a quiz from the history, built like the topics screen builds it.</summary>
    public static string For(QuizSummaryResponse quiz)
    {
        ArgumentNullException.ThrowIfNull(quiz);

        if (quiz.Mode == MockExamMode)
        {
            return MockExam;
        }

        var review = quiz.Mode == ReviewMode;
        return quiz.TopicTitle is { } topic ? ForTopic(topic, review) : ForExam(review);
    }

    /// <summary>"1 pregunta fallada" / "{n} preguntas falladas".</summary>
    public static string MistakeTotalText(int count) => count == 1
        ? "1 pregunta fallada"
        : string.Create(CultureInfo.InvariantCulture, $"{count} preguntas falladas");

    /// <summary>"1 fallada" / "{n} falladas".</summary>
    public static string TopicMistakeText(int count) => count == 1
        ? "1 fallada"
        : string.Create(CultureInfo.InvariantCulture, $"{count} falladas");

    /// <summary>Line of the review summary about the mistakes that are left.</summary>
    public static string MistakesLeftText(int count) => count switch
    {
        <= 0 => "¡Has repasado todas tus preguntas falladas!",
        1 => "Te queda 1 pregunta fallada",
        _ => string.Create(CultureInfo.InvariantCulture, $"Te quedan {count} preguntas falladas"),
    };
}
