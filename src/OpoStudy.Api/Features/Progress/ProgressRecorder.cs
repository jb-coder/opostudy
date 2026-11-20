using Microsoft.EntityFrameworkCore;

using OpoStudy.Contracts.Progress;
using OpoStudy.Domain.Common;
using OpoStudy.Domain.Users;
using OpoStudy.Infrastructure.Persistence;

namespace OpoStudy.Api.Features.Progress;

/// <summary>Progress after one answer: the daily progress and the XP earned.</summary>
internal sealed record AnswerProgress(DailyProgressResponse Daily, XpGainResponse Xp);

/// <summary>
/// Single place that records the user's progress (daily activity, streak and XP). It only changes tracked entities:
/// the caller saves, so the progress is stored in the same transaction as the action that caused it.
/// </summary>
internal sealed class ProgressRecorder(OpoStudyDbContext db)
{
    /// <summary>Counts one answered question for today (in the user's time zone) and grants its XP.</summary>
    public async Task<AnswerProgress> RecordAnswerAsync(User user, bool isCorrect, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        var today = user.TodayAt(now);
        var activity = await FindActivityAsync(user.Id, today, cancellationToken);
        if (activity is null)
        {
            activity = DailyActivity.Start(user.Id, today, user.DailyGoal);
            db.DailyActivities.Add(activity);
        }

        var levelBefore = user.Level;
        var goalMetNow = activity.RegisterAnswer(now);
        var bonus = 0;
        if (goalMetNow)
        {
            user.Streak.RegisterGoalMet(today);
            bonus = XpRules.ForGoalMet(user.Streak.CurrentAt(today));
        }

        // Every XP change also touches today's activity (xmin concurrency token), so concurrent answers cannot lose XP.
        var gained = XpRules.ForAnswer(isCorrect) + bonus;
        user.GainXp(gained);
        activity.AddXp(gained);

        return new AnswerProgress(
            new DailyProgressResponse(activity.AnsweredCount, activity.Goal, goalMetNow, user.Streak.CurrentAt(today)),
            new XpGainResponse(gained, bonus, user.Xp, user.Level, user.Level > levelBefore));
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
            var bonus = XpRules.ForGoalMet(user.Streak.CurrentAt(today));
            user.GainXp(bonus);
            activity.AddXp(bonus);
        }
    }

    /// <summary>Reads the user's level, XP into the level and XP earned today (in the user's time zone).</summary>
    public async Task<LevelResponse> GetLevelAsync(User user, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        var today = user.TodayAt(now);
        var userId = user.Id;
        var xpToday = await db.DailyActivities
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.Date == today)
            .Select(a => a.XpEarned)
            .FirstOrDefaultAsync(cancellationToken);

        var level = user.Level;
        var floor = XpRules.XpToReach(level);
        return new LevelResponse(level, user.Xp, user.Xp - floor, XpRules.XpToReach(level + 1) - floor, xpToday);
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