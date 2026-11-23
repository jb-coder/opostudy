using Microsoft.EntityFrameworkCore;

using OpoStudy.Contracts.Progress;
using OpoStudy.Domain.Common;
using OpoStudy.Domain.Users;
using OpoStudy.Infrastructure.Persistence;
using OpoStudy.Infrastructure.Progress;

namespace OpoStudy.Api.Features.Progress;

/// <summary>Progress after one answer: the daily progress and the XP earned.</summary>
internal sealed record AnswerProgress(DailyProgressResponse Daily, XpGainResponse Xp);

/// <summary>
/// Single place that records the user's progress (daily activity, streak and XP). It only changes tracked entities:
/// the caller saves, so the progress is stored in the same transaction as the action that caused it.
/// </summary>
internal sealed class ProgressRecorder(OpoStudyDbContext db, ProgressTracker tracker)
{
    /// <summary>
    /// Counts one answered question on the day of <paramref name="now"/> (in the user's time zone) and grants its XP.
    /// <paramref name="now"/> is the time of the answer, not the server's: an answer synced late passes its own time.
    /// </summary>
    public async Task<AnswerProgress> RecordAnswerAsync(User user, bool isCorrect, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        var today = user.TodayAt(now);
        await SettleStreakAsync(user, today, now, cancellationToken);
        var activity = await tracker.GetOrStartActivityAsync(user, today, cancellationToken);

        var levelBefore = user.Level;
        var goalMetNow = activity.RegisterAnswer(now);
        var bonus = 0;
        if (goalMetNow)
        {
            user.Streak.RegisterGoalMet(today);
            // A late answer for an already settled day: RegisterGoalMet ignores it and CurrentAt can be 0.
            bonus = XpRules.ForGoalMet(Math.Max(1, user.Streak.CurrentAt(today)));
        }

        // Every XP change also touches today's activity (xmin concurrency token), so concurrent answers cannot lose XP.
        var gained = XpRules.ForAnswer(isCorrect) + bonus;
        user.GainXp(gained);
        activity.AddXp(gained);

        return new AnswerProgress(
            new DailyProgressResponse(activity.AnsweredCount, activity.Goal, goalMetNow, user.Streak.CurrentAt(today)),
            new XpGainResponse(gained, bonus, user.Xp, user.Level, user.Level > levelBefore));
    }

    /// <summary>
    /// Settles the streak: spends freezes on the closed days that were not met, or loses the streak if there are not enough.
    /// Returns the number of days protected.
    /// </summary>
    public Task<int> SettleStreakAsync(User user, DateOnly today, DateTimeOffset now, CancellationToken cancellationToken) =>
        tracker.SettleStreakAsync(user, today, now, cancellationToken);

    /// <summary>Buys a streak freeze. The streak is settled first, so a lost streak is not recovered. Returns the days protected by the settlement.</summary>
    public async Task<int> BuyStreakFreezeAsync(User user, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        var protectedDays = await SettleStreakAsync(user, user.TodayAt(now), now, cancellationToken);
        user.BuyStreakFreeze();
        return protectedDays;
    }

    /// <summary>Changes the daily goal. It applies from today: past days are untouched and a met day stays met. Returns the days protected by the settlement.</summary>
    public async Task<int> ChangeDailyGoalAsync(User user, int dailyGoal, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        var today = user.TodayAt(now);
        var protectedDays = await SettleStreakAsync(user, today, now, cancellationToken);
        user.SetDailyGoal(dailyGoal);
        var activity = await tracker.FindActivityAsync(user.Id, today, cancellationToken);
        if (activity is not null && activity.ChangeGoal(dailyGoal, now))
        {
            user.Streak.RegisterGoalMet(today);
            var bonus = XpRules.ForGoalMet(user.Streak.CurrentAt(today));
            user.GainXp(bonus);
            activity.AddXp(bonus);
        }

        return protectedDays;
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
        return new LevelResponse(level, user.Xp, user.Xp - floor, XpRules.XpToReach(level + 1) - floor, xpToday, user.SpendableXp);
    }

    /// <summary>
    /// Reads today's progress, the streak and the last <see cref="Limits.StreakHistoryDays"/> days.
    /// The caller settles and saves first; <paramref name="freezesUsedNow"/> are the days that settlement protected.
    /// </summary>
    public async Task<StreakResponse> GetStreakAsync(User user, DateTimeOffset now, int freezesUsedNow, CancellationToken cancellationToken)
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
                    ? new DayActivityResponse(date, a.AnsweredCount, a.Goal, a.IsGoalMet, a.IsProtected)
                    : new DayActivityResponse(date, 0, user.DailyGoal, false, false);
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
            lastDays,
            user.Streak.Freezes,
            Limits.MaxStreakFreezes,
            XpRules.StreakFreezePrice,
            freezesUsedNow);
    }
}
