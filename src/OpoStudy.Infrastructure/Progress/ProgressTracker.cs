using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using OpoStudy.Domain.Users;
using OpoStudy.Infrastructure.Persistence;

namespace OpoStudy.Infrastructure.Progress;

/// <summary>Progress given by one corrected practical case: the XP (goal bonus included) and whether it met the daily goal.</summary>
public sealed record PracticalCaseProgress(int XpEarned, bool MetDailyGoal);

/// <summary>
/// Write side of the user's progress (daily activity, streak and XP), shared by the API and the Worker.
/// It only changes tracked entities: the caller saves, so the progress is stored in the same transaction as the action that caused it.
/// </summary>
public sealed class ProgressTracker(OpoStudyDbContext db)
{
    /// <summary>
    /// Settles the streak: spends freezes on the closed days that were not met, or loses the streak if there are not enough.
    /// Returns the number of days protected.
    /// </summary>
    public async Task<int> SettleStreakAsync(User user, DateOnly today, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        var days = user.Streak.Settle(today);
        foreach (var day in days)
        {
            var activity = await GetOrStartActivityAsync(user, day, cancellationToken);
            activity.Protect(now);
        }

        return days.Count;
    }

    /// <summary>Finds the activity of the day (also the ones added and not saved yet) or starts a new one with the user's goal.</summary>
    public async Task<DailyActivity> GetOrStartActivityAsync(User user, DateOnly date, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        var activity = await FindActivityAsync(user.Id, date, cancellationToken);
        if (activity is null)
        {
            activity = DailyActivity.Start(user.Id, date, user.DailyGoal);
            db.DailyActivities.Add(activity);
        }

        return activity;
    }

    /// <summary>Finds the activity of the day, in the tracker or in the database. Activities added by an earlier call and not saved yet are only in the tracker.</summary>
    public async Task<DailyActivity?> FindActivityAsync(Guid userId, DateOnly date, CancellationToken cancellationToken) =>
        db.DailyActivities.Local.FirstOrDefault(a => a.UserId == userId && a.Date == date)
        ?? await db.DailyActivities.FirstOrDefaultAsync(a => a.UserId == userId && a.Date == date, cancellationToken);

    /// <summary>
    /// Records a corrected practical case on the day it was submitted (in the user's time zone, like a late synced answer):
    /// XP proportional to the score, plus the goal bonus if it meets the goal of that day now.
    /// </summary>
    public async Task<PracticalCaseProgress> RecordPracticalCaseAsync(
        User user, decimal score, int maxPoints, DateTimeOffset submittedAt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        var day = user.TodayAt(submittedAt);
        await SettleStreakAsync(user, day, submittedAt, cancellationToken);
        var activity = await GetOrStartActivityAsync(user, day, cancellationToken);

        var metNow = activity.MeetGoal(submittedAt);
        var bonus = 0;
        if (metNow)
        {
            var lastMetBefore = user.Streak.LastMetOn;
            user.Streak.RegisterGoalMet(day);
            // A day the streak already kept or closed is ignored by RegisterGoalMet: the streak length then belongs to
            // later days, so the bonus is the one of a streak of 1 instead of an inflated one.
            var extended = user.Streak.LastMetOn == day && lastMetBefore != day;
            bonus = XpRules.ForGoalMet(extended ? Math.Max(1, user.Streak.CurrentAt(day)) : 1);
        }

        var xp = XpRules.ForPracticalCase(score, maxPoints) + bonus;
        if (xp > 0)
        {
            user.GainXp(xp);
            activity.AddXp(xp);
        }

        return new PracticalCaseProgress(xp, metNow);
    }
}

public static class ProgressServiceCollectionExtensions
{
    /// <summary>Registers <see cref="ProgressTracker"/>; the host registers the <see cref="OpoStudyDbContext"/>.</summary>
    public static IServiceCollection AddProgressTracking(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<ProgressTracker>();
        return services;
    }
}
