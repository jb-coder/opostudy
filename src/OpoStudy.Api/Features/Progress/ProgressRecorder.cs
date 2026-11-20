using Microsoft.EntityFrameworkCore;

using OpoStudy.Contracts.Progress;
using OpoStudy.Domain.Common;
using OpoStudy.Domain.Users;
using OpoStudy.Infrastructure.Persistence;

namespace OpoStudy.Api.Features.Progress;

/// <summary>
/// Single place that records the user's progress (daily activity and streak). It only changes tracked entities:
/// the caller saves, so the progress is stored in the same transaction as the action that caused it.
/// </summary>
internal sealed class ProgressRecorder(OpoStudyDbContext db)
{
    /// <summary>Counts one answered question for today (in the user's time zone).</summary>
    public async Task<DailyProgressResponse> RecordAnswerAsync(User user, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        var today = user.TodayAt(now);
        var activity = await FindActivityAsync(user.Id, today, cancellationToken);
        if (activity is null)
        {
            activity = DailyActivity.Start(user.Id, today, user.DailyGoal);
            db.DailyActivities.Add(activity);
        }

        var goalMetNow = activity.RegisterAnswer(now);
        if (goalMetNow)
        {
            user.Streak.RegisterGoalMet(today);
        }

        return new DailyProgressResponse(activity.AnsweredCount, activity.Goal, goalMetNow, user.Streak.CurrentAt(today));
    }

    /// <summary>Changes the daily goal. It applies from today: past days are untouched and a met day stays met.</summary>
    public async Task ChangeDailyGoalAsync(User user, int dailyGoal, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        user.SetDailyGoal(dailyGoal);
        var today = user.TodayAt(now);
        var activity = await FindActivityAsync(user.Id, today, cancellationToken);
        if (activity is not null && activity.ChangeGoal(dailyGoal, now))
        {
            user.Streak.RegisterGoalMet(today);
        }
    }

    /// <summary>Reads today's progress, the streak and the last <see cref="Limits.StreakHistoryDays"/> days.</summary>
    public async Task<StreakResponse> GetStreakAsync(User user, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        var today = user.TodayAt(now);
        var from = today.AddDays(1 - Limits.StreakHistoryDays);
        var userId = user.Id;
        var activities = await db.DailyActivities
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.Date >= from && a.Date <= today)
            .ToDictionaryAsync(a => a.Date, cancellationToken);

        var lastDays = Enumerable.Range(0, Limits.StreakHistoryDays)
            .Select(offset =>
            {
                var date = from.AddDays(offset);
                return activities.TryGetValue(date, out var a)
                    ? new DayActivityResponse(date, a.AnsweredCount, a.Goal, a.IsGoalMet)
                    : new DayActivityResponse(date, 0, user.DailyGoal, false);
            })
            .ToList();
        var todayActivity = lastDays[^1];

        return new StreakResponse(
            user.DailyGoal,
            today,
            todayActivity.AnsweredCount,
            todayActivity.GoalMet,
            user.Streak.CurrentAt(today),
            user.Streak.Longest,
            lastDays);
    }

    private Task<DailyActivity?> FindActivityAsync(Guid userId, DateOnly date, CancellationToken cancellationToken) =>
        db.DailyActivities.FirstOrDefaultAsync(a => a.UserId == userId && a.Date == date, cancellationToken);
}