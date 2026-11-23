using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using OpoStudy.Api.Errors;
using OpoStudy.Api.Features.Users;
using OpoStudy.Contracts.Gradings;
using OpoStudy.Domain.Common;
using OpoStudy.Domain.Gradings;
using OpoStudy.Domain.PracticalCases;
using OpoStudy.Infrastructure.Gradings;
using OpoStudy.Infrastructure.Persistence;

namespace OpoStudy.Api.Features.Gradings;

internal static class GradingsEndpoints
{
    private const int DefaultTake = 20;
    private const int MaxTake = 50;

    public static RouteGroupBuilder MapGradingsEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/practical-cases/{id:guid}/gradings", SubmitGrading)
            .WithName("SubmitGrading")
            .WithTags("Gradings")
            .WithSummary("Envía las respuestas a un supuesto práctico para que se corrijan de forma asíncrona")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        api.MapGet("/gradings/{id:guid}", GetGrading)
            .WithName("GetGrading")
            .WithTags("Gradings")
            .WithSummary("Lee una corrección propia; la rúbrica y la respuesta modelo solo salen cuando está completada")
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/gradings", ListGradings)
            .WithName("ListGradings")
            .WithTags("Gradings")
            .WithSummary("Historial de correcciones del usuario, de la más reciente a la más antigua")
            .ProducesValidationProblem();

        api.MapGet("/me/grading-quota", GetGradingQuota)
            .WithName("GetGradingQuota")
            .WithTags("Gradings")
            .WithSummary("Cuota mensual de correcciones del usuario");

        return api;
    }

    private static async Task<Results<Accepted<GradingResponse>, ProblemHttpResult>> SubmitGrading(
        Guid id,
        SubmitGradingRequest request,
        CurrentUserService currentUser,
        OpoStudyDbContext db,
        IOptions<GradingOptions> options,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (request.Answers is null || request.Answers.Count > Limits.MaxCaseParts)
        {
            return DomainErrors.Problem(DomainErrors.GradingInvalidAnswerCount);
        }

        var user = await currentUser.GetOrCreateAsync(cancellationToken);

        var practicalCase = await db.PracticalCases
            .Where(c => c.Id == id && c.IsActive && db.Exams.Any(e => e.Id == c.ExamId && e.IsActive))
            .FirstOrDefaultAsync(cancellationToken);
        if (practicalCase is null)
        {
            return DomainErrors.Problem(DomainErrors.PracticalCaseNotFound);
        }

        var userId = user.Id;
        if (await db.Gradings.AnyAsync(
                g => g.UserId == userId && (g.Status == GradingStatus.Pending || g.Status == GradingStatus.Processing),
                cancellationToken))
        {
            return DomainErrors.Problem(DomainErrors.GradingAlreadyPending);
        }

        var now = timeProvider.GetUtcNow();
        var limit = options.Value.MonthlyQuota;
        if (await CountUsedAsync(db, user, now, cancellationToken) >= limit)
        {
            return DomainErrors.Problem(DomainErrors.GradingQuotaExceeded, $"Has agotado tus {limit} correcciones de este mes.");
        }

        // The domain validates the answers. If another request created a grading in progress meanwhile, the partial
        // unique index rejects this insert and the exception handler answers 409.
        var grading = Grading.Submit(userId, practicalCase, request.Answers, now);
        db.Gradings.Add(grading);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Accepted($"/api/v1/gradings/{grading.Id}", ToResponse(grading, practicalCase));
    }

    private static async Task<Results<Ok<GradingResponse>, ProblemHttpResult>> GetGrading(
        Guid id,
        CurrentUserService currentUser,
        OpoStudyDbContext db,
        CancellationToken cancellationToken)
    {
        var user = await currentUser.GetOrCreateAsync(cancellationToken);
        var userId = user.Id;

        // Ownership filter: a grading of another user looks exactly like one that does not exist.
        var grading = await db.Gradings
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId, cancellationToken);
        if (grading is null)
        {
            return DomainErrors.Problem(DomainErrors.GradingNotFound);
        }

        var practicalCase = await db.PracticalCases
            .AsNoTracking()
            .SingleAsync(c => c.Id == grading.PracticalCaseId, cancellationToken);
        return TypedResults.Ok(ToResponse(grading, practicalCase));
    }

    private static async Task<Results<Ok<IReadOnlyList<GradingSummaryResponse>>, ValidationProblem>> ListGradings(
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

        // Projection straight from the query: gradings and answers are never materialized.
        var rows = await db.Gradings
            .AsNoTracking()
            .Where(g => g.UserId == userId)
            .OrderByDescending(g => g.SubmittedAt)
            .ThenByDescending(g => g.Id)
            .Skip(skip)
            .Take(take)
            .Select(g => new
            {
                g.Id,
                g.PracticalCaseId,
                CaseTitle = db.PracticalCases.Where(c => c.Id == g.PracticalCaseId).Select(c => c.Title).First(),
                Section = db.PracticalCases.Where(c => c.Id == g.PracticalCaseId).Select(c => c.Section).First(),
                g.Status,
                g.SubmittedAt,
                g.MaxPoints,
                g.Score,
            })
            .ToListAsync(cancellationToken);

        IReadOnlyList<GradingSummaryResponse> summaries =
        [
            .. rows.Select(r => new GradingSummaryResponse(
                r.Id, r.PracticalCaseId, r.CaseTitle, r.Section, r.Status.ToString(), r.SubmittedAt, r.MaxPoints, r.Score)),
        ];
        return TypedResults.Ok(summaries);
    }

    private static async Task<Ok<GradingQuotaResponse>> GetGradingQuota(
        CurrentUserService currentUser,
        OpoStudyDbContext db,
        IOptions<GradingOptions> options,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var user = await currentUser.GetOrCreateAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var limit = options.Value.MonthlyQuota;
        var used = await CountUsedAsync(db, user, now, cancellationToken);
        var (_, resetsAt) = GradingQuota.PeriodAt(user.TimeZone, now);
        return TypedResults.Ok(new GradingQuotaResponse(limit, used, Math.Max(0, limit - used), resetsAt));
    }

    /// <summary>Gradings of the calendar month of the user that count against the quota: failed ones do not.</summary>
    private static Task<int> CountUsedAsync(OpoStudyDbContext db, Domain.Users.User user, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var (start, end) = GradingQuota.PeriodAt(user.TimeZone, now);
        var userId = user.Id;
        return db.Gradings.CountAsync(
            g => g.UserId == userId && g.SubmittedAt >= start && g.SubmittedAt < end && g.Status != GradingStatus.Failed,
            cancellationToken);
    }

    private static GradingResponse ToResponse(Grading grading, PracticalCase practicalCase)
    {
        var parts = practicalCase.Parts;
        var completed = grading.Status == GradingStatus.Completed;

        // The rubric and the model answers stay hidden until the grading is completed.
        var answers = grading.Answers
            .Select(a =>
            {
                // The case may have changed since the submission: a missing part must not turn the read into a 500.
                var part = parts.FirstOrDefault(p => p.Order == a.Order);
                return new GradingAnswerResponse(
                    a.Order,
                    part?.Statement ?? string.Empty,
                    a.Text,
                    a.MaxPoints,
                    completed ? a.Score : null,
                    completed ? a.Feedback : null,
                    completed ? part?.ModelAnswer : null,
                    completed ? [.. a.Criteria.Select(c => new CriterionScoreResponse(c.Description, c.Points, c.Awarded ?? 0m))] : null);
            })
            .ToList();

        return new GradingResponse(
            grading.Id,
            grading.PracticalCaseId,
            practicalCase.Title,
            grading.Status.ToString(),
            grading.Attempts,
            grading.SubmittedAt,
            grading.CompletedAt,
            grading.MaxPoints,
            completed ? grading.Score : null,
            completed ? grading.Feedback : null,
            grading.FailureCode,
            answers);
    }
}