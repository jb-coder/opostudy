using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

using OpoStudy.Api.Errors;
using OpoStudy.Contracts.Exams;
using OpoStudy.Infrastructure.Persistence;

namespace OpoStudy.Api.Features.Exams;

internal static class ExamsEndpoints
{
    public static RouteGroupBuilder MapExamsEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/exams", ListExams)
            .WithName("ListExams")
            .WithTags("Exams")
            .WithSummary("Lista las oposiciones activas");

        api.MapGet("/exams/{examId:guid}/topics", ListTopics)
            .WithName("ListExamTopics")
            .WithTags("Exams")
            .WithSummary("Lista los temas de una oposición con su número de preguntas activas")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return api;
    }

    private static async Task<Ok<IReadOnlyList<ExamResponse>>> ListExams(OpoStudyDbContext db, CancellationToken cancellationToken)
    {
        IReadOnlyList<ExamResponse> exams = await db.Exams
            .AsNoTracking()
            .Where(e => e.IsActive)
            .OrderBy(e => e.Name)
            .Select(e => new ExamResponse(e.Id, e.Slug, e.Name, e.Organization, e.Group))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(exams);
    }

    private static async Task<Results<Ok<IReadOnlyList<TopicResponse>>, ProblemHttpResult>> ListTopics(
        Guid examId,
        OpoStudyDbContext db,
        CancellationToken cancellationToken)
    {
        if (!await db.Exams.AnyAsync(e => e.Id == examId && e.IsActive, cancellationToken))
        {
            return DomainErrors.Problem(DomainErrors.ExamNotFound);
        }

        IReadOnlyList<TopicResponse> topics = await db.Topics
            .AsNoTracking()
            .Where(t => t.ExamId == examId)
            .OrderBy(t => t.Number)
            .Select(t => new TopicResponse(
                t.Id,
                t.Number,
                t.Title,
                t.Section,
                db.Questions.Count(q => q.TopicId == t.Id && q.IsActive)))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(topics);
    }
}