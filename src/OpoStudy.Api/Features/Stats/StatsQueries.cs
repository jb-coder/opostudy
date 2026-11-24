using Microsoft.EntityFrameworkCore;

using OpoStudy.Contracts.Progress;
using OpoStudy.Contracts.Stats;
using OpoStudy.Domain.Common;
using OpoStudy.Domain.Gradings;
using OpoStudy.Domain.Quizzes;
using OpoStudy.Infrastructure.Persistence;

namespace OpoStudy.Api.Features.Stats;

/// <summary>Read-only statistics queries. Every query filters by the user and projects straight from SQL: attempts, answers and rubrics are never materialized.</summary>
internal static class StatsQueries
{
    public static async Task<QuizStatsResponse> GetQuizStatsAsync(OpoStudyDbContext db, Guid userId, Guid examId, CancellationToken cancellationToken)
    {
        // Counts per topic of the question (not of the quiz), so random quizzes and reviews count too.
        var answered = await (
            from x in db.Set<QuizAnswer>()
            join a in db.QuizAttempts on x.QuizAttemptId equals a.Id
            join q in db.Questions on x.QuestionId equals q.Id
            where a.UserId == userId && a.ExamId == examId && x.IsCorrect != null
            group x by q.TopicId into g
            select new
            {
                TopicId = g.Key,
                Answered = g.Count(),
                Correct = g.Count(x => x.IsCorrect == true),
                Last = g.Max(x => x.AnsweredAt),
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var topics = await db.Topics
            .AsNoTracking()
            .Where(t => t.ExamId == examId)
            .Select(t => new
            {
                t.Id,
                t.Number,
                t.Title,
                t.Section,
                Available = db.Questions.Count(q => q.TopicId == t.Id && q.IsActive),
            })
            .ToListAsync(cancellationToken);

        var finished = await db.QuizAttempts
            .AsNoTracking()
            .CountAsync(a => a.UserId == userId && a.ExamId == examId && a.FinishedAt != null, cancellationToken);

        var byTopic = topics.ToDictionary(t => t.Id);
        var rows = answered
            .Where(r => byTopic.ContainsKey(r.TopicId) && r.Last is not null)
            .Select(r =>
            {
                var t = byTopic[r.TopicId];
                return new TopicStatsResponse(t.Id, t.Number, t.Title, t.Section, r.Answered, r.Correct, r.Last!.Value, t.Available);
            })
            .ToList();

        // Weakest first. Ratios are compared by cross-multiplication (exact), then more answers, then topic number.
        rows.Sort((a, b) =>
        {
            var byRatio = ((long)a.CorrectCount * b.AnsweredCount).CompareTo((long)b.CorrectCount * a.AnsweredCount);
            if (byRatio != 0)
            {
                return byRatio;
            }

            var byAnswered = b.AnsweredCount.CompareTo(a.AnsweredCount);
            return byAnswered != 0 ? byAnswered : a.Number.CompareTo(b.Number);
        });

        return new QuizStatsResponse(
            rows.Sum(r => r.AnsweredCount),
            rows.Sum(r => r.CorrectCount),
            finished,
            topics.Count(t => t.Available > 0),
            rows);
    }

    public static async Task<PracticalCaseStatsResponse> GetPracticalCaseStatsAsync(OpoStudyDbContext db, Guid userId, Guid examId, CancellationToken cancellationToken)
    {
        var gradings = await (
            from g in db.Gradings
            join c in db.PracticalCases on g.PracticalCaseId equals c.Id
            where g.UserId == userId && c.ExamId == examId && g.Status == GradingStatus.Completed
                && g.Score != null && g.MaxPoints > 0
            select new Row(g.Id, c.Id, c.Title, c.Section, g.SubmittedAt, g.Score!.Value, g.MaxPoints))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (gradings.Count == 0)
        {
            return new PracticalCaseStatsResponse(0, null, [], []);
        }

        var average = Round(gradings.Average(r => r.Score * 100m / r.MaxPoints));

        var best = gradings
            .GroupBy(r => r.CaseId)
            .Select(group =>
            {
                var top = group.Aggregate((a, b) => IsBetter(b, a) ? b : a);
                return new CaseBestScoreResponse(top.CaseId, top.Title, top.Section, group.Count(), top.Id, top.Score, top.MaxPoints, Percent(top));
            })
            .OrderBy(b => b.CaseTitle, StringComparer.Ordinal)
            .ThenBy(b => b.PracticalCaseId)
            .ToList();

        var recent = gradings
            .OrderByDescending(r => r.SubmittedAt)
            .ThenByDescending(r => r.Id)
            .Take(Limits.RecentScoresCount)
            .Reverse()
            .Select(r => new CaseScoreResponse(r.Id, r.Title, r.SubmittedAt, r.Score, r.MaxPoints, Percent(r)))
            .ToList();

        return new PracticalCaseStatsResponse(gradings.Count, average, best, recent);
    }

    public static async Task<IReadOnlyList<DayActivityResponse>> GetActivityAsync(
        OpoStudyDbContext db, Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        await db.DailyActivities
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.Date >= from && a.Date <= to)
            .Where(a => a.AnsweredCount > 0 || a.XpEarned > 0 || a.GoalMetAt != null || a.FreezeUsedAt != null)
            .OrderBy(a => a.Date)
            .Select(a => new DayActivityResponse(a.Date, a.AnsweredCount, a.Goal, a.GoalMetAt != null, a.FreezeUsedAt != null, a.XpEarned))
            .ToListAsync(cancellationToken);

    /// <summary>Whether <paramref name="a"/> beats <paramref name="b"/>: greater exact percentage, or the same one and more recent.</summary>
    private static bool IsBetter(Row a, Row b)
    {
        var byRatio = (a.Score * b.MaxPoints).CompareTo(b.Score * a.MaxPoints);
        if (byRatio != 0)
        {
            return byRatio > 0;
        }

        var bySubmitted = a.SubmittedAt.CompareTo(b.SubmittedAt);
        return bySubmitted != 0 ? bySubmitted > 0 : a.Id.CompareTo(b.Id) > 0;
    }

    private static decimal Percent(Row r) => Round(r.Score * 100m / r.MaxPoints);

    private static decimal Round(decimal value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);

    private sealed record Row(Guid Id, Guid CaseId, string Title, string? Section, DateTimeOffset SubmittedAt, decimal Score, int MaxPoints);
}