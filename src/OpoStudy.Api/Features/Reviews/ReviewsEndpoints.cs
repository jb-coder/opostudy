using Microsoft.EntityFrameworkCore;

using OpoStudy.Api.Features.Users;
using OpoStudy.Contracts.Reviews;
using OpoStudy.Infrastructure.Persistence;

namespace OpoStudy.Api.Features.Reviews;

internal static class ReviewsEndpoints
{
    public static RouteGroupBuilder MapReviewsEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/reviews/due", GetDueReviews)
            .WithName("GetDueReviews")
            .WithTags("Reviews")
            .WithSummary("Preguntas que tocan repasar hoy (repaso espaciado), por oposición")
            .RequireAuthorization();

        return api;
    }

    private static async Task<IResult> GetDueReviews(
        CurrentUserService currentUser,
        OpoStudyDbContext db,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var user = await currentUser.GetOrCreateAsync(cancellationToken);
        var userId = user.Id;
        var today = user.TodayAt(timeProvider.GetUtcNow());

        var rows = await (
            from r in db.ReviewSchedules.AsNoTracking()
            join q in db.Questions on r.QuestionId equals q.Id
            join t in db.Topics on q.TopicId equals t.Id
            join e in db.Exams on t.ExamId equals e.Id
            where r.UserId == userId && r.DueOn <= today && q.IsActive && e.IsActive
            group r by new { e.Id, e.Name } into g
            select new { g.Key.Id, g.Key.Name, Count = g.Count() })
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        IReadOnlyList<ExamDueReviewsResponse> exams = [.. rows.Select(x => new ExamDueReviewsResponse(x.Id, x.Name, x.Count))];
        return TypedResults.Ok(new DueReviewSummaryResponse(exams.Sum(x => x.Count), exams));
    }
}
