using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

using OpoStudy.Api.Errors;
using OpoStudy.Api.Features.Progress;
using OpoStudy.Api.Features.Users;
using OpoStudy.Contracts.MockExams;
using OpoStudy.Contracts.Quizzes;
using OpoStudy.Domain.Common;
using OpoStudy.Domain.Questions;
using OpoStudy.Domain.Quizzes;
using OpoStudy.Infrastructure.Persistence;

namespace OpoStudy.Api.Features.MockExams;

internal static class MockExamsEndpoints
{
    public static RouteGroupBuilder MapMockExamsEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/exams/{examId:guid}/exercises", ListExercises)
            .WithName("ListExamExercises")
            .WithTags("MockExams")
            .WithSummary("Lista los ejercicios de una oposición con las preguntas disponibles para cada simulacro")
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapPost("/mock-exams", CreateMockExam)
            .WithName("CreateMockExam")
            .WithTags("MockExams")
            .WithSummary("Crea un simulacro de examen de un ejercicio, sin revelar las respuestas")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        api.MapPost("/mock-exams/{mockExamId:guid}/submission", SubmitMockExam)
            .WithName("SubmitMockExam")
            .WithTags("MockExams")
            .WithSummary("Entrega el simulacro con las preguntas respondidas y devuelve la nota")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        api.MapGet("/mock-exams/{mockExamId:guid}/result", GetResult)
            .WithName("GetMockExamResult")
            .WithTags("MockExams")
            .WithSummary("Lee el resultado de un simulacro entregado")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return api;
    }

    private static async Task<Results<Ok<IReadOnlyList<ExamExerciseResponse>>, ProblemHttpResult>> ListExercises(
        Guid examId,
        OpoStudyDbContext db,
        CancellationToken cancellationToken)
    {
        if (!await db.Exams.AnyAsync(e => e.Id == examId && e.IsActive, cancellationToken))
        {
            return DomainErrors.Problem(DomainErrors.ExamNotFound);
        }

        var rows = await db.ExamExercises
            .AsNoTracking()
            .Where(x => x.ExamId == examId)
            .OrderBy(x => x.Number)
            .Select(x => new
            {
                x.Number,
                x.Title,
                x.QuestionCount,
                x.DurationMinutes,
                x.MaxScore,
                x.PenaltyRatio,
                Available = db.Questions.Count(q => q.IsActive
                    && db.Topics.Any(t => t.Id == q.TopicId && t.ExamId == examId && t.Section != null && x.Sections.Contains(t.Section))),
            })
            .ToListAsync(cancellationToken);

        IReadOnlyList<ExamExerciseResponse> exercises =
        [
            .. rows.Select(r => new ExamExerciseResponse(
                r.Number,
                r.Title,
                r.QuestionCount,
                r.DurationMinutes,
                r.MaxScore,
                r.PenaltyRatio,
                Math.Round(r.MaxScore / 2, 2, MidpointRounding.AwayFromZero),
                r.Available)),
        ];
        return TypedResults.Ok(exercises);
    }

    private static async Task<Results<Created<MockExamResponse>, ValidationProblem, ProblemHttpResult>> CreateMockExam(
        CreateMockExamRequest request,
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

        if (request.ExerciseNumber < 1)
        {
            errors["exerciseNumber"] = ["El número de ejercicio no es válido."];
        }

        if (request.PassScore is <= 0)
        {
            errors["passScore"] = ["La nota para aprobar debe ser mayor que 0."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var user = await currentUser.GetOrCreateAsync(cancellationToken);

        var examId = request.ExamId;
        if (!await db.Exams.AnyAsync(e => e.Id == examId && e.IsActive, cancellationToken))
        {
            return DomainErrors.Problem(DomainErrors.ExamNotFound);
        }

        var exercise = await db.ExamExercises
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ExamId == examId && x.Number == request.ExerciseNumber, cancellationToken);
        if (exercise is null)
        {
            return DomainErrors.Problem(DomainErrors.ExerciseNotFound);
        }

        // An invalid pass score (above the maximum, more than two decimals) is translated to 422 by the exception handler.
        var rules = exercise.ToRules(request.PassScore);

        var sections = exercise.Sections;
        var questionIds = await db.Questions
            .Where(q => q.IsActive
                && db.Topics.Any(t => t.Id == q.TopicId && t.ExamId == examId && t.Section != null && sections.Contains(t.Section)))
            .Select(q => q.Id)
            .OrderBy(_ => EF.Functions.Random())
            .Take(rules.QuestionCount)
            .ToListAsync(cancellationToken);
        if (questionIds.Count < rules.QuestionCount)
        {
            return DomainErrors.Problem(DomainErrors.MockExamNotEnoughQuestions);
        }

        var attempt = QuizAttempt.CreateMockExam(user.Id, examId, rules, questionIds, timeProvider.GetUtcNow());
        db.QuizAttempts.Add(attempt);
        await db.SaveChangesAsync(cancellationToken);

        var questions = await LoadQuestionsAsync(db, questionIds, cancellationToken);
        var response = new MockExamResponse(
            attempt.Id,
            attempt.ExamId,
            rules.ExerciseNumber,
            rules.Title,
            attempt.StartedAt,
            attempt.DeadlineAt!.Value,
            (int)rules.Duration.TotalMinutes,
            rules.MaxScore,
            rules.PenaltyRatio,
            rules.PassScore,
            [.. attempt.Answers.Select(a => ToQuestionResponse(questions[a.QuestionId], a.Order))]);
        return TypedResults.Created($"/api/v1/mock-exams/{attempt.Id}/result", response);
    }

    private static async Task<Results<Ok<SubmitMockExamResponse>, ValidationProblem, ProblemHttpResult>> SubmitMockExam(
        Guid mockExamId,
        SubmitMockExamRequest request,
        CurrentUserService currentUser,
        ProgressRecorder progress,
        OpoStudyDbContext db,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var answers = request.Answers;
        var errors = new Dictionary<string, string[]>();
        if (answers is null)
        {
            errors["answers"] = ["Las respuestas son obligatorias."];
        }
        else if (answers.Count > Limits.MaxQuestionsPerQuiz)
        {
            errors["answers"] = [$"No puede haber más de {Limits.MaxQuestionsPerQuiz} respuestas."];
        }
        else if (answers.Any(a => a is null || a.QuestionId == Guid.Empty || a.OptionId == Guid.Empty)
            || answers.Select(a => a.QuestionId).Distinct().Count() != answers.Count)
        {
            errors["answers"] = ["Las respuestas deben tener pregunta y opción, y no repetir la pregunta."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var user = await currentUser.GetOrCreateAsync(cancellationToken);

        var attempt = await db.QuizAttempts
            .FirstOrDefaultAsync(a => a.Id == mockExamId && a.UserId == user.Id && a.Mode == QuizMode.MockExam, cancellationToken);
        if (attempt is null)
        {
            return DomainErrors.Problem(DomainErrors.MockExamNotFound);
        }

        var questionIds = attempt.Answers.Select(a => a.QuestionId).ToList();
        var questions = await LoadQuestionsAsync(db, questionIds, cancellationToken);
        var selections = answers!.ToDictionary(a => a.QuestionId, a => a.OptionId);

        // Domain rule violations (foreign question or option, already submitted) and concurrency conflicts
        // are translated by DomainRuleExceptionHandler.
        var now = timeProvider.GetUtcNow();
        var submitted = attempt.Submit([.. questions.Values], selections, now);

        var xpGained = 0;
        foreach (var item in submitted)
        {
            var recorded = await progress.RecordAnswerAsync(user, item.IsCorrect, now, cancellationToken);
            xpGained += recorded.Xp.Gained;
        }

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(new SubmitMockExamResponse(BuildResult(attempt, questions), xpGained));
    }

    private static async Task<Results<Ok<MockExamResultResponse>, ProblemHttpResult>> GetResult(
        Guid mockExamId,
        CurrentUserService currentUser,
        OpoStudyDbContext db,
        CancellationToken cancellationToken)
    {
        var user = await currentUser.GetOrCreateAsync(cancellationToken);

        var attempt = await db.QuizAttempts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == mockExamId && a.UserId == user.Id && a.Mode == QuizMode.MockExam, cancellationToken);
        if (attempt is null)
        {
            return DomainErrors.Problem(DomainErrors.MockExamNotFound);
        }

        if (!attempt.IsFinished)
        {
            return DomainErrors.Problem(DomainErrors.MockExamNotSubmitted);
        }

        var questions = await LoadQuestionsAsync(db, [.. attempt.Answers.Select(a => a.QuestionId)], cancellationToken);
        return TypedResults.Ok(BuildResult(attempt, questions));
    }

    // Questions of an attempt always exist (the importer deactivates, never deletes); inactive ones are loaded on purpose.
    private static async Task<Dictionary<Guid, Question>> LoadQuestionsAsync(
        OpoStudyDbContext db,
        List<Guid> questionIds,
        CancellationToken cancellationToken) =>
        await db.Questions
            .AsNoTracking()
            .Where(q => questionIds.Contains(q.Id))
            .ToDictionaryAsync(q => q.Id, cancellationToken);

    private static MockExamQuestionResponse ToQuestionResponse(Question question, int order) =>
        new(question.Id, order, question.Statement, [.. question.Options.Select(o => new QuizOptionResponse(o.Id, o.Order, o.Text))]);

    private static MockExamResultResponse BuildResult(QuizAttempt attempt, Dictionary<Guid, Question> questions)
    {
        var rules = attempt.MockExam!;
        var items = attempt.Answers
            .Select(answer =>
            {
                var question = questions[answer.QuestionId];
                var outcome = answer.IsCorrect switch
                {
                    true => "Correct",
                    false => "Incorrect",
                    null => "Blank",
                };
                return new MockExamResultItem(
                    question.Id,
                    answer.Order,
                    question.Statement,
                    [.. question.Options.Select(o => new QuizOptionResponse(o.Id, o.Order, o.Text))],
                    answer.SelectedOptionId,
                    question.CorrectOption.Id,
                    outcome,
                    question.Explanation,
                    question.LegalReference);
            })
            .ToList();

        return new MockExamResultResponse(
            attempt.Id,
            rules.ExerciseNumber,
            rules.Title,
            attempt.StartedAt,
            attempt.FinishedAt!.Value,
            attempt.IsSubmittedLate,
            rules.QuestionCount,
            attempt.CorrectCount,
            attempt.IncorrectCount,
            attempt.BlankCount,
            rules.PenaltyRatio,
            attempt.Score!.Value,
            rules.MaxScore,
            rules.PassScore,
            attempt.Passed,
            items);
    }
}