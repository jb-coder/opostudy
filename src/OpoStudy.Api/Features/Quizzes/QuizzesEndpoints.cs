using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

using OpoStudy.Api.Errors;
using OpoStudy.Api.Features.Users;
using OpoStudy.Contracts.Quizzes;
using OpoStudy.Domain.Common;
using OpoStudy.Domain.Questions;
using OpoStudy.Domain.Quizzes;
using OpoStudy.Infrastructure.Persistence;

namespace OpoStudy.Api.Features.Quizzes;

internal static class QuizzesEndpoints
{
    private const int DefaultTake = 20;
    private const int MaxTake = 50;

    public static RouteGroupBuilder MapQuizzesEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/quizzes", CreateQuiz)
            .WithName("CreateQuiz")
            .WithTags("Quizzes")
            .WithSummary("Crea un test por tema o aleatorio, sin revelar las respuestas")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        api.MapGet("/quizzes", ListQuizzes)
            .WithName("ListQuizzes")
            .WithTags("Quizzes")
            .WithSummary("Historial de tests del usuario, del más reciente al más antiguo")
            .ProducesValidationProblem();

        api.MapGet("/quizzes/{quizId:guid}", GetQuiz)
            .WithName("GetQuiz")
            .WithTags("Quizzes")
            .WithSummary("Lee un test propio para retomarlo")
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapPost("/quizzes/{quizId:guid}/answers", SubmitAnswer)
            .WithName("SubmitAnswer")
            .WithTags("Quizzes")
            .WithSummary("Responde una pregunta del test y devuelve la aclaración")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return api;
    }

    private static async Task<Results<Created<QuizResponse>, ValidationProblem, ProblemHttpResult>> CreateQuiz(
        CreateQuizRequest request,
        CurrentUserService currentUser,
        OpoStudyDbContext db,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.ExamId == Guid.Empty)
        {
            errors["examId"] = ["La oposición es obligatoria."];
        }

        if (request.TopicId == Guid.Empty)
        {
            errors["topicId"] = ["El tema no es válido."];
        }

        if (request.QuestionCount is < 1 or > Limits.MaxQuestionsPerQuiz)
        {
            errors["questionCount"] = [$"El número de preguntas debe estar entre 1 y {Limits.MaxQuestionsPerQuiz}."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var user = await currentUser.GetOrCreateAsync(cancellationToken);

        if (!await db.Exams.AnyAsync(e => e.Id == request.ExamId && e.IsActive, cancellationToken))
        {
            return DomainErrors.Problem(DomainErrors.ExamNotFound);
        }

        var examId = request.ExamId;
        IQueryable<Question> candidates;
        QuizMode mode;
        if (request.TopicId is { } topicId)
        {
            if (!await db.Topics.AnyAsync(t => t.Id == topicId && t.ExamId == examId, cancellationToken))
            {
                return DomainErrors.Problem(DomainErrors.TopicNotFound);
            }

            mode = QuizMode.ByTopic;
            candidates = db.Questions.Where(q => q.IsActive && q.TopicId == topicId);
        }
        else
        {
            mode = QuizMode.Random;
            candidates = db.Questions.Where(q => q.IsActive && db.Topics.Any(t => t.Id == q.TopicId && t.ExamId == examId));
        }

        var questionIds = await candidates
            .OrderBy(_ => EF.Functions.Random())
            .Select(q => q.Id)
            .Take(request.QuestionCount)
            .ToListAsync(cancellationToken);
        if (questionIds.Count == 0)
        {
            return DomainErrors.Problem(DomainErrors.QuizNoQuestionsAvailable);
        }

        var attempt = QuizAttempt.Create(user.Id, examId, request.TopicId, mode, questionIds, timeProvider.GetUtcNow());
        db.QuizAttempts.Add(attempt);
        await db.SaveChangesAsync(cancellationToken);

        var questions = await LoadQuestionsAsync(db, questionIds, cancellationToken);
        return TypedResults.Created($"/api/v1/quizzes/{attempt.Id}", BuildQuizResponse(attempt, questions));
    }

    private static async Task<Results<Ok<IReadOnlyList<QuizSummaryResponse>>, ValidationProblem>> ListQuizzes(
        CurrentUserService currentUser,
        OpoStudyDbContext db,
        CancellationToken cancellationToken,
        int skip = 0,
        int take = DefaultTake)
    {
        var errors = new Dictionary<string, string[]>();
        if (skip < 0)
        {
            errors["skip"] = ["El desplazamiento no puede ser negativo."];
        }

        if (take is < 1 or > MaxTake)
        {
            errors["take"] = [$"El tamaño de página debe estar entre 1 y {MaxTake}."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var user = await currentUser.GetOrCreateAsync(cancellationToken);
        var userId = user.Id;

        // Projection straight from the query: attempts and answers are never materialized.
        var rows = await db.QuizAttempts
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.StartedAt)
            .ThenByDescending(a => a.Id)
            .Skip(skip)
            .Take(take)
            .Select(a => new
            {
                a.Id,
                a.ExamId,
                a.TopicId,
                TopicTitle = db.Topics.Where(t => t.Id == a.TopicId).Select(t => t.Title).FirstOrDefault(),
                a.Mode,
                a.StartedAt,
                a.FinishedAt,
                QuestionCount = a.Answers.Count,
                CorrectCount = a.Answers.Count(x => x.IsCorrect == true),
                IncorrectCount = a.Answers.Count(x => x.IsCorrect == false),
            })
            .ToListAsync(cancellationToken);

        IReadOnlyList<QuizSummaryResponse> summaries =
        [
            .. rows.Select(r => new QuizSummaryResponse(
                r.Id,
                r.ExamId,
                r.TopicId,
                r.TopicTitle,
                r.Mode.ToString(),
                r.StartedAt,
                r.FinishedAt,
                r.QuestionCount,
                r.CorrectCount,
                r.IncorrectCount)),
        ];
        return TypedResults.Ok(summaries);
    }

    private static async Task<Results<Ok<QuizResponse>, ProblemHttpResult>> GetQuiz(
        Guid quizId,
        CurrentUserService currentUser,
        OpoStudyDbContext db,
        CancellationToken cancellationToken)
    {
        var user = await currentUser.GetOrCreateAsync(cancellationToken);

        var attempt = await db.QuizAttempts
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == quizId && q.UserId == user.Id, cancellationToken);
        if (attempt is null)
        {
            return DomainErrors.Problem(DomainErrors.QuizNotFound);
        }

        var questions = await LoadQuestionsAsync(db, [.. attempt.Answers.Select(a => a.QuestionId)], cancellationToken);
        return TypedResults.Ok(BuildQuizResponse(attempt, questions));
    }

    private static async Task<Results<Ok<AnswerFeedbackResponse>, ValidationProblem, ProblemHttpResult>> SubmitAnswer(
        Guid quizId,
        SubmitAnswerRequest request,
        CurrentUserService currentUser,
        OpoStudyDbContext db,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.QuestionId == Guid.Empty)
        {
            errors["questionId"] = ["La pregunta es obligatoria."];
        }

        if (request.OptionId == Guid.Empty)
        {
            errors["optionId"] = ["La opción es obligatoria."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var user = await currentUser.GetOrCreateAsync(cancellationToken);

        var attempt = await db.QuizAttempts
            .FirstOrDefaultAsync(q => q.Id == quizId && q.UserId == user.Id, cancellationToken);
        if (attempt is null)
        {
            return DomainErrors.Problem(DomainErrors.QuizNotFound);
        }

        if (!attempt.Answers.Any(a => a.QuestionId == request.QuestionId))
        {
            return DomainErrors.Problem(DomainErrors.QuizForeignQuestion);
        }

        var question = await db.Questions
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == request.QuestionId, cancellationToken);
        if (question is null)
        {
            return DomainErrors.Problem(DomainErrors.QuizForeignQuestion);
        }

        // Domain rule violations are translated by DomainRuleExceptionHandler; so is a concurrency conflict on save.
        var result = attempt.Answer(question, request.OptionId, timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        var feedback = new AnswerFeedback(
            request.OptionId,
            result.IsCorrect,
            result.CorrectOptionId,
            question.Explanation,
            question.LegalReference);
        return TypedResults.Ok(new AnswerFeedbackResponse(
            question.Id,
            feedback,
            attempt.IsFinished,
            attempt.CorrectCount,
            attempt.IncorrectCount,
            attempt.Answers.Count(a => a.IsAnswered),
            attempt.Answers.Count));
    }

    private static async Task<Dictionary<Guid, Question>> LoadQuestionsAsync(
        OpoStudyDbContext db,
        List<Guid> questionIds,
        CancellationToken cancellationToken) =>
        await db.Questions
            .AsNoTracking()
            .Where(q => questionIds.Contains(q.Id))
            .ToDictionaryAsync(q => q.Id, cancellationToken);

    private static QuizResponse BuildQuizResponse(QuizAttempt attempt, Dictionary<Guid, Question> questions)
    {
        var items = attempt.Answers
            .Select(answer =>
            {
                var question = questions[answer.QuestionId];
                AnswerFeedback? feedback = answer.IsAnswered
                    ? new AnswerFeedback(
                        answer.SelectedOptionId!.Value,
                        answer.IsCorrect == true,
                        question.CorrectOption.Id,
                        question.Explanation,
                        question.LegalReference)
                    : null;
                return new QuizQuestionResponse(
                    question.Id,
                    answer.Order,
                    question.Statement,
                    [.. question.Options.Select(o => new QuizOptionResponse(o.Id, o.Order, o.Text))],
                    feedback);
            })
            .ToList();

        return new QuizResponse(
            attempt.Id,
            attempt.ExamId,
            attempt.TopicId,
            attempt.Mode.ToString(),
            attempt.StartedAt,
            attempt.FinishedAt,
            attempt.CorrectCount,
            attempt.IncorrectCount,
            items);
    }
}