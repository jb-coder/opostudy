using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

using OpoStudy.Api.Errors;
using OpoStudy.Api.Features.Quizzes;
using OpoStudy.Api.Features.Users;
using OpoStudy.Contracts.Exams;
using OpoStudy.Contracts.Quizzes;
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

        api.MapGet("/exams/{examId:guid}/mistakes", GetMistakes)
            .WithName("GetExamMistakes")
            .WithTags("Exams")
            .WithSummary("Cuenta las preguntas falladas pendientes del usuario, en total y por tema")
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

    private static async Task<Results<Ok<MistakeSummaryResponse>, ProblemHttpResult>> GetMistakes(
        Guid examId,
        CurrentUserService currentUser,
        OpoStudyDbContext db,
        CancellationToken cancellationToken)
    {
        if (!await db.Exams.AnyAsync(e => e.Id == examId && e.IsActive, cancellationToken))
        {
            return DomainErrors.Problem(DomainErrors.ExamNotFound);
        }

        var user = await currentUser.GetOrCreateAsync(cancellationToken);

        var counts = await MistakeQueries.PendingMistakes(db, user.Id)
            .Join(db.Topics.Where(t => t.ExamId == examId), m => m.TopicId, t => t.Id, (m, t) => new { t.Id, t.Number })
            .GroupBy(x => new { x.Id, x.Number })
            .Select(g => new { g.Key.Id, g.Key.Number, Count = g.Count() })
            .OrderBy(x => x.Number)
            .ToListAsync(cancellationToken);

        IReadOnlyList<TopicMistakeCountResponse> topics = [.. counts.Select(c => new TopicMistakeCountResponse(c.Id, c.Count))];
        return TypedResults.Ok(new MistakeSummaryResponse(topics.Sum(t => t.Count), topics));
    }
}
