using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

using OpoStudy.Api.Errors;
using OpoStudy.Contracts.PracticalCases;
using OpoStudy.Domain.PracticalCases;
using OpoStudy.Infrastructure.Persistence;

namespace OpoStudy.Api.Features.PracticalCases;

internal static class PracticalCasesEndpoints
{
    public static RouteGroupBuilder MapPracticalCasesEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/exams/{examId:guid}/practical-cases", ListPracticalCases)
            .WithName("ListExamPracticalCases")
            .WithTags("PracticalCases")
            .WithSummary("Lista los supuestos prácticos activos de una oposición")
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/practical-cases/{id:guid}", GetPracticalCase)
            .WithName("GetPracticalCase")
            .WithTags("PracticalCases")
            .WithSummary("Devuelve un supuesto práctico con sus apartados, sin la rúbrica ni la respuesta modelo")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return api;
    }

    private static async Task<Results<Ok<IReadOnlyList<PracticalCaseSummaryResponse>>, ProblemHttpResult>> ListPracticalCases(
        Guid examId,
        OpoStudyDbContext db,
        CancellationToken cancellationToken)
    {
        if (!await db.Exams.AnyAsync(e => e.Id == examId && e.IsActive, cancellationToken))
        {
            return DomainErrors.Problem(DomainErrors.ExamNotFound);
        }

        IReadOnlyList<PracticalCaseSummaryResponse> cases = await db.PracticalCases
            .AsNoTracking()
            .Where(c => c.ExamId == examId && c.IsActive)
            .OrderBy(c => c.Key)
            .Select(c => new PracticalCaseSummaryResponse(
                c.Id,
                c.Title,
                c.Parts.Count,
                c.Parts.Sum(p => p.MaxPoints),
                c.EstimatedMinutes))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(cases);
    }

    private static async Task<Results<Ok<PracticalCaseResponse>, ProblemHttpResult>> GetPracticalCase(
        Guid id,
        OpoStudyDbContext db,
        CancellationToken cancellationToken)
    {
        var practicalCase = await db.PracticalCases
            .AsNoTracking()
            .Where(c => c.Id == id && c.IsActive && db.Exams.Any(e => e.Id == c.ExamId && e.IsActive))
            .Select(c => new
            {
                c.Id,
                c.ExamId,
                c.Title,
                c.Statement,
                c.EstimatedMinutes,
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (practicalCase is null)
        {
            return DomainErrors.Problem(DomainErrors.PracticalCaseNotFound);
        }

        IReadOnlyList<PracticalCasePartResponse> parts = await db.Set<PracticalCasePart>()
            .AsNoTracking()
            .Where(p => p.PracticalCaseId == id)
            .OrderBy(p => p.Order)
            .Select(p => new PracticalCasePartResponse(p.Order, p.Statement, p.MaxPoints))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new PracticalCaseResponse(
            practicalCase.Id,
            practicalCase.ExamId,
            practicalCase.Title,
            practicalCase.Statement,
            practicalCase.EstimatedMinutes,
            parts.Sum(p => p.MaxPoints),
            parts));
    }
}
