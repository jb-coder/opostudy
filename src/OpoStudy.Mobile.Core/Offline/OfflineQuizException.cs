namespace OpoStudy.Mobile.Core.Offline;

/// <summary>A quiz cannot be created offline. <see cref="Exception.Message"/> is a user-facing (Spanish) text.</summary>
public sealed class OfflineQuizException : Exception
{
    /// <summary>The same text as the API's <c>quiz.no_questions_available</c>.</summary>
    public const string NoQuestionsAvailable = "No hay preguntas disponibles para este test.";

    public const string NoContentDownloaded = "Sin conexión y sin contenido descargado. Abre una oposición con conexión para poder estudiar sin ella.";

    public OfflineQuizException(string message)
        : base(message)
    {
    }

    public OfflineQuizException()
        : this(NoQuestionsAvailable)
    {
    }

    public OfflineQuizException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}