using System.Collections.Frozen;

using Microsoft.AspNetCore.Http.HttpResults;

using OpoStudy.Domain.Common;
using OpoStudy.Domain.Users;

namespace OpoStudy.Api.Errors;

/// <summary>Maps stable error codes to HTTP status and a user-facing (Spanish) message that never includes the code.</summary>
internal static class DomainErrors
{
    public const string ExamNotFound = "exam.not_found";
    public const string TopicNotFound = "topic.not_found";
    public const string QuizNotFound = "quiz.not_found";
    public const string QuizForeignQuestion = "quiz.foreign_question";
    public const string QuizNoQuestionsAvailable = "quiz.no_questions_available";
    public const string QuizNoMistakes = "quiz.no_mistakes";
    public const string QuizConcurrentUpdate = "quiz.concurrent_update";
    public const string InvalidRequest = "request.invalid";
    public const string ConcurrencyConflict = "concurrency.conflict";

    private const string FallbackMessage = "No se ha podido completar la operación.";

    private static readonly FrozenDictionary<string, (int StatusCode, string Message)> Errors =
        new Dictionary<string, (int StatusCode, string Message)>
        {
            ["quiz.finished"] = (StatusCodes.Status409Conflict, "El test ya está terminado."),
            ["quiz.already_answered"] = (StatusCodes.Status409Conflict, "Ya has respondido a esta pregunta."),
            [QuizConcurrentUpdate] = (StatusCodes.Status409Conflict, "El test se ha modificado desde otra petición. Vuelve a cargarlo."),
            [ConcurrencyConflict] = (StatusCodes.Status409Conflict, "Los datos se han modificado desde otra petición. Vuelve a intentarlo."),
            ["streak.freezes_full"] = (StatusCodes.Status422UnprocessableEntity, $"Ya tienes el máximo de {Limits.MaxStreakFreezes} protectores."),
            ["xp.insufficient"] = (StatusCodes.Status422UnprocessableEntity, $"Necesitas {XpRules.StreakFreezePrice} XP disponibles para comprar un protector."),
            [QuizForeignQuestion] = (StatusCodes.Status422UnprocessableEntity, "La pregunta no forma parte de este test."),
            ["quiz.foreign_option"] = (StatusCodes.Status422UnprocessableEntity, "La opción elegida no corresponde a la pregunta."),
            [QuizNoQuestionsAvailable] = (StatusCodes.Status422UnprocessableEntity, "No hay preguntas disponibles para este test."),
            [QuizNoMistakes] = (StatusCodes.Status422UnprocessableEntity, "No tienes preguntas falladas pendientes de repasar aquí."),
            ["quiz.invalid_question_count"] = (StatusCodes.Status422UnprocessableEntity, "Un test debe tener entre 1 y 100 preguntas."),
            [ExamNotFound] = (StatusCodes.Status404NotFound, "La oposición no existe."),
            [TopicNotFound] = (StatusCodes.Status404NotFound, "El tema no existe."),
            [QuizNotFound] = (StatusCodes.Status404NotFound, "El test no existe."),
        }.ToFrozenDictionary();

    public static (int StatusCode, string Message) Describe(string code) =>
        Errors.TryGetValue(code, out var error) ? error : (StatusCodes.Status422UnprocessableEntity, FallbackMessage);

    /// <summary>ProblemDetails with <c>title</c> = user message and the stable <c>code</c> as an extension.</summary>
    public static ProblemHttpResult Problem(string code)
    {
        var (statusCode, message) = Describe(code);
        return TypedResults.Problem(
            title: message,
            statusCode: statusCode,
            extensions: new Dictionary<string, object?> { ["code"] = code });
    }
}