using OpoStudy.Domain.Common;
using OpoStudy.Domain.Users;

using Shouldly;

namespace OpoStudy.Domain.Tests;

public sealed class StreakAndDailyActivityTests
{
    private static readonly DateOnly Day = new(2026, 3, 10);

    // ---- Streak ----

    [Fact]
    public void RegisterGoalMet_FirstDay_StartsStreakOfOne()
    {
        var streak = new Streak();

        streak.RegisterGoalMet(Day);

        streak.LastLength.ShouldBe(1);
        streak.Longest.ShouldBe(1);
        streak.LastMetOn.ShouldBe(Day);
    }

    [Fact]
    public void RegisterGoalMet_ConsecutiveDays_IncrementsStreak()
    {
        var streak = new Streak();

        streak.RegisterGoalMet(Day);
        streak.RegisterGoalMet(Day.AddDays(1));
        streak.RegisterGoalMet(Day.AddDays(2));

        streak.LastLength.ShouldBe(3);
        streak.Longest.ShouldBe(3);
    }

    [Fact]
    public void RegisterGoalMet_SameDayTwice_DoesNothing()
    {
        var streak = new Streak();
        streak.RegisterGoalMet(Day);

        streak.RegisterGoalMet(Day);

        streak.LastLength.ShouldBe(1);
    }

    [Fact]
    public void RegisterGoalMet_AfterGap_RestartsButKeepsLongest()
    {
        var streak = new Streak();
        streak.RegisterGoalMet(Day);
        streak.RegisterGoalMet(Day.AddDays(1));

        streak.RegisterGoalMet(Day.AddDays(3));

        streak.LastLength.ShouldBe(1);
        streak.Longest.ShouldBe(2);
    }

    [Fact]
    public void RegisterGoalMet_EarlierDay_IsIgnored()
    {
        var streak = new Streak();
        streak.RegisterGoalMet(Day);

        streak.RegisterGoalMet(Day.AddDays(-1));

        streak.LastMetOn.ShouldBe(Day);
        streak.LastLength.ShouldBe(1);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 3)]
    [InlineData(2, 0)]
    [InlineData(5, 0)]
    public void CurrentAt_DependsOnDaysSinceLastMet(int daysLater, int expected)
    {
        var streak = new Streak();
        streak.RegisterGoalMet(Day);
        streak.RegisterGoalMet(Day.AddDays(1));
        streak.RegisterGoalMet(Day.AddDays(2));

        streak.CurrentAt(Day.AddDays(2 + daysLater)).ShouldBe(expected);
    }

    [Fact]
    public void CurrentAt_WithoutAnyDayMet_IsZero() => new Streak().CurrentAt(Day).ShouldBe(0);

    // ---- DailyActivity ----

    [Fact]
    public void RegisterAnswer_ReachingGoal_ReturnsTrueOnlyOnce()
    {
        var activity = DailyActivity.Start(Guid.NewGuid(), Day, 5);
        var now = DateTimeOffset.UtcNow;

        var results = Enumerable.Range(0, 7).Select(_ => activity.RegisterAnswer(now)).ToList();

        results.ShouldBe([false, false, false, false, true, false, false]);
        activity.AnsweredCount.ShouldBe(7);
        activity.IsGoalMet.ShouldBeTrue();
        activity.GoalMetAt.ShouldNotBeNull();
    }

    [Fact]
    public void ChangeGoal_Raising_KeepsDayPending()
    {
        var activity = DailyActivity.Start(Guid.NewGuid(), Day, 5);
        activity.RegisterAnswer(DateTimeOffset.UtcNow);

        var met = activity.ChangeGoal(20, DateTimeOffset.UtcNow);

        met.ShouldBeFalse();
        activity.Goal.ShouldBe(20);
        activity.IsGoalMet.ShouldBeFalse();
    }

    [Fact]
    public void ChangeGoal_LoweringToAnsweredCount_MeetsGoal()
    {
        var activity = DailyActivity.Start(Guid.NewGuid(), Day, 20);
        for (var i = 0; i < 6; i++)
        {
            activity.RegisterAnswer(DateTimeOffset.UtcNow);
        }

        var met = activity.ChangeGoal(5, DateTimeOffset.UtcNow);

        met.ShouldBeTrue();
        activity.IsGoalMet.ShouldBeTrue();
    }

    [Fact]
    public void ChangeGoal_WhenAlreadyMet_DoesNothing()
    {
        var activity = DailyActivity.Start(Guid.NewGuid(), Day, 5);
        for (var i = 0; i < 5; i++)
        {
            activity.RegisterAnswer(DateTimeOffset.UtcNow);
        }

        var met = activity.ChangeGoal(50, DateTimeOffset.UtcNow);

        met.ShouldBeFalse();
        activity.Goal.ShouldBe(5);
        activity.IsGoalMet.ShouldBeTrue();
    }

    // ---- User ----

    [Fact]
    public void Create_NewUser_HasDefaults()
    {
        var user = User.Create("uid", DateTimeOffset.UtcNow);

        user.DailyGoal.ShouldBe(10);
        user.TimeZone.ShouldBe("Europe/Madrid");
        user.Streak.Longest.ShouldBe(0);
        user.Streak.LastMetOn.ShouldBeNull();
    }

    [Theory]
    [InlineData(5)]
    [InlineData(100)]
    public void SetDailyGoal_WithinLimits_Changes(int goal)
    {
        var user = User.Create("uid", DateTimeOffset.UtcNow);

        user.SetDailyGoal(goal);

        user.DailyGoal.ShouldBe(goal);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(101)]
    [InlineData(0)]
    public void SetDailyGoal_OutOfRange_Throws(int goal)
    {
        var user = User.Create("uid", DateTimeOffset.UtcNow);

        Should.Throw<DomainRuleException>(() => user.SetDailyGoal(goal)).Code.ShouldBe("user.invalid_daily_goal");
        user.DailyGoal.ShouldBe(10);
    }

    [Fact]
    public void SetTimeZone_Valid_Changes()
    {
        var user = User.Create("uid", DateTimeOffset.UtcNow);

        user.SetTimeZone("Atlantic/Canary");

        user.TimeZone.ShouldBe("Atlantic/Canary");
    }

    [Theory]
    [InlineData("Mars/Olympus")]
    [InlineData("")]
    [InlineData("   ")]
    public void SetTimeZone_Invalid_Throws(string zone)
    {
        var user = User.Create("uid", DateTimeOffset.UtcNow);

        Should.Throw<DomainRuleException>(() => user.SetTimeZone(zone)).Code.ShouldBe("user.invalid_time_zone");
    }

    [Fact]
    public void SetTimeZone_TooLong_Throws()
    {
        var user = User.Create("uid", DateTimeOffset.UtcNow);

        Should.Throw<DomainRuleException>(() => user.SetTimeZone(new string('A', Limits.TimeZone + 1)))
            .Code.ShouldBe("user.invalid_time_zone");
    }

    [Fact]
    public void TodayAt_AroundMidnightInWinter_UsesMadridTime()
    {
        var user = User.Create("uid", DateTimeOffset.UtcNow);

        user.TodayAt(new DateTimeOffset(2026, 1, 15, 22, 30, 0, TimeSpan.Zero)).ShouldBe(new DateOnly(2026, 1, 15));
        user.TodayAt(new DateTimeOffset(2026, 1, 15, 23, 30, 0, TimeSpan.Zero)).ShouldBe(new DateOnly(2026, 1, 16));
    }

    [Fact]
    public void TodayAt_AroundMidnightInSummer_UsesMadridTime()
    {
        var user = User.Create("uid", DateTimeOffset.UtcNow);

        user.TodayAt(new DateTimeOffset(2026, 7, 15, 21, 30, 0, TimeSpan.Zero)).ShouldBe(new DateOnly(2026, 7, 15));
        user.TodayAt(new DateTimeOffset(2026, 7, 15, 22, 30, 0, TimeSpan.Zero)).ShouldBe(new DateOnly(2026, 7, 16));
    }

    [Fact]
    public void TodayAt_WithCanaryTimeZone_IsOneHourBehind()
    {
        var user = User.Create("uid", DateTimeOffset.UtcNow);
        user.SetTimeZone("Atlantic/Canary");

        user.TodayAt(new DateTimeOffset(2026, 1, 15, 23, 30, 0, TimeSpan.Zero)).ShouldBe(new DateOnly(2026, 1, 15));
    }
}