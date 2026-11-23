using OpoStudy.Domain.Common;
using OpoStudy.Domain.Users;

using Shouldly;

namespace OpoStudy.Domain.Tests;

public sealed class StreakFreezeTests
{
    private static readonly DateOnly Monday = new(2026, 3, 9);
    private static readonly DateTimeOffset Now = new(2026, 3, 12, 10, 0, 0, TimeSpan.Zero);

    /// <summary>A streak of <paramref name="length"/> days ending on <see cref="Monday"/> with <paramref name="freezes"/> freezes.</summary>
    private static Streak StreakWith(int length, int freezes)
    {
        var streak = new Streak();
        for (var i = length - 1; i >= 0; i--)
        {
            streak.RegisterGoalMet(Monday.AddDays(-i));
        }

        for (var i = 0; i < freezes; i++)
        {
            streak.AddFreeze();
        }

        return streak;
    }

    // ---- Streak.Settle ----

    [Fact]
    public void Settle_NoStreak_DoesNothing()
    {
        var streak = new Streak();
        streak.AddFreeze();

        var days = streak.Settle(Monday.AddDays(10));

        days.ShouldBeEmpty();
        streak.Freezes.ShouldBe(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Settle_GoalMetTodayOrYesterday_DoesNothing(int daysAfterMonday)
    {
        var streak = StreakWith(3, 1);

        var days = streak.Settle(Monday.AddDays(daysAfterMonday));

        days.ShouldBeEmpty();
        streak.Freezes.ShouldBe(1);
        streak.CurrentAt(Monday.AddDays(daysAfterMonday)).ShouldBe(3);
    }

    [Fact]
    public void Settle_OneMissedDayWithFreeze_SpendsItAndKeepsStreak()
    {
        var streak = StreakWith(5, 1);
        var wednesday = Monday.AddDays(2);

        var days = streak.Settle(wednesday);

        days.ShouldBe([Monday.AddDays(1)]);
        streak.Freezes.ShouldBe(0);
        streak.ProtectedThrough.ShouldBe(Monday.AddDays(1));
        streak.CurrentAt(wednesday).ShouldBe(5);
    }

    [Fact]
    public void Settle_TwoMissedDaysWithTwoFreezes_SpendsBoth()
    {
        var streak = StreakWith(5, 2);

        var days = streak.Settle(Monday.AddDays(3));

        days.ShouldBe([Monday.AddDays(1), Monday.AddDays(2)]);
        streak.Freezes.ShouldBe(0);
        streak.CurrentAt(Monday.AddDays(3)).ShouldBe(5);
    }

    [Fact]
    public void Settle_MoreMissedDaysThanFreezes_LosesStreakAndKeepsFreezes()
    {
        var streak = StreakWith(5, 1);

        var days = streak.Settle(Monday.AddDays(3));

        days.ShouldBeEmpty();
        streak.Freezes.ShouldBe(1);
        streak.LastLength.ShouldBe(0);
        streak.CurrentAt(Monday.AddDays(3)).ShouldBe(0);
        streak.Longest.ShouldBe(5);
    }

    [Fact]
    public void Settle_CalledTwiceTheSameDay_SpendsOnlyOnce()
    {
        var streak = StreakWith(5, 2);
        var wednesday = Monday.AddDays(2);
        streak.Settle(wednesday);

        var second = streak.Settle(wednesday);

        second.ShouldBeEmpty();
        streak.Freezes.ShouldBe(1);
    }

    [Fact]
    public void Settle_GapAfterPreviousProtectedDay_CountsOnlyNewMissedDays()
    {
        var streak = StreakWith(5, 2);
        streak.Settle(Monday.AddDays(2));

        var days = streak.Settle(Monday.AddDays(3));

        days.ShouldBe([Monday.AddDays(2)]);
        streak.Freezes.ShouldBe(0);
        streak.CurrentAt(Monday.AddDays(3)).ShouldBe(5);
    }

    [Fact]
    public void Settle_LostStreakWithFreezes_DoesNotSpendOnLaterCalls()
    {
        var streak = StreakWith(5, 1);
        streak.Settle(Monday.AddDays(3));

        var days = streak.Settle(Monday.AddDays(8));

        days.ShouldBeEmpty();
        streak.Freezes.ShouldBe(1);
    }

    // ---- RegisterGoalMet and CurrentAt with protected days ----

    [Fact]
    public void RegisterGoalMet_AfterProtectedDay_ContinuesStreakAndLongest()
    {
        var streak = StreakWith(5, 1);
        var wednesday = Monday.AddDays(2);
        streak.Settle(wednesday);

        streak.RegisterGoalMet(wednesday);

        streak.LastLength.ShouldBe(6);
        streak.Longest.ShouldBe(6);
        streak.LastMetOn.ShouldBe(wednesday);
    }

    [Fact]
    public void Settle_ProtectedDay_DoesNotChangeLongest()
    {
        var streak = StreakWith(5, 1);

        streak.Settle(Monday.AddDays(2));

        streak.Longest.ShouldBe(5);
        streak.LastLength.ShouldBe(5);
    }

    [Fact]
    public void RegisterGoalMet_OnAProtectedDay_IsIgnored()
    {
        var streak = StreakWith(5, 1);
        streak.Settle(Monday.AddDays(2));

        streak.RegisterGoalMet(Monday.AddDays(1));

        streak.LastLength.ShouldBe(5);
        streak.LastMetOn.ShouldBe(Monday);
    }

    [Fact]
    public void RegisterGoalMet_AfterLostStreak_RestartsAtOne()
    {
        var streak = StreakWith(5, 0);
        streak.Settle(Monday.AddDays(3));

        streak.RegisterGoalMet(Monday.AddDays(3));

        streak.LastLength.ShouldBe(1);
        streak.Longest.ShouldBe(5);
    }

    [Fact]
    public void CurrentAt_ProtectedYesterday_KeepsStreakAlive()
    {
        var streak = StreakWith(4, 1);
        streak.Settle(Monday.AddDays(2));

        streak.CurrentAt(Monday.AddDays(2)).ShouldBe(4);
        streak.CurrentAt(Monday.AddDays(3)).ShouldBe(0);
    }

    // ---- AddFreeze and User.BuyStreakFreeze ----

    [Fact]
    public void AddFreeze_AtMaximum_ThrowsFreezesFull()
    {
        var streak = StreakWith(1, Limits.MaxStreakFreezes);

        var error = Should.Throw<DomainRuleException>(streak.AddFreeze);

        error.Code.ShouldBe("streak.freezes_full");
        streak.Freezes.ShouldBe(Limits.MaxStreakFreezes);
    }

    private static User UserWithXp(int xp)
    {
        var user = User.Create("uid-freeze", Now);
        if (xp > 0)
        {
            user.GainXp(xp);
        }

        return user;
    }

    [Fact]
    public void NewUser_HasNoFreezesAndAllXpSpendable()
    {
        var user = UserWithXp(130);

        user.Streak.Freezes.ShouldBe(0);
        user.XpSpent.ShouldBe(0);
        user.SpendableXp.ShouldBe(130);
    }

    [Fact]
    public void BuyStreakFreeze_EnoughXp_SpendsPriceAndKeepsLevel()
    {
        var user = UserWithXp(450);
        var level = user.Level;

        user.BuyStreakFreeze();

        user.Streak.Freezes.ShouldBe(1);
        user.XpSpent.ShouldBe(XpRules.StreakFreezePrice);
        user.SpendableXp.ShouldBe(250);
        user.Xp.ShouldBe(450);
        user.Level.ShouldBe(level);
    }

    [Theory]
    [InlineData(199, false)]
    [InlineData(200, true)]
    public void BuyStreakFreeze_AroundThePrice_OnlyWorksFromThePrice(int xp, bool succeeds)
    {
        var user = UserWithXp(xp);

        if (succeeds)
        {
            user.BuyStreakFreeze();
            user.Streak.Freezes.ShouldBe(1);
        }
        else
        {
            Should.Throw<DomainRuleException>(user.BuyStreakFreeze).Code.ShouldBe("xp.insufficient");
            user.Streak.Freezes.ShouldBe(0);
            user.XpSpent.ShouldBe(0);
        }
    }

    [Fact]
    public void BuyStreakFreeze_AtMaximumWithPlentyOfXp_ThrowsFreezesFull()
    {
        var user = UserWithXp(2000);
        user.BuyStreakFreeze();
        user.BuyStreakFreeze();

        var error = Should.Throw<DomainRuleException>(user.BuyStreakFreeze);

        error.Code.ShouldBe("streak.freezes_full");
        user.XpSpent.ShouldBe(400);
    }

    [Fact]
    public void BuyStreakFreeze_AtMaximumWithoutXp_ReportsFreezesFullFirst()
    {
        var user = UserWithXp(400);
        user.BuyStreakFreeze();
        user.BuyStreakFreeze();

        Should.Throw<DomainRuleException>(user.BuyStreakFreeze).Code.ShouldBe("streak.freezes_full");
    }

    // ---- DailyActivity.Protect ----

    private static DailyActivity NewActivity() => DailyActivity.Start(Guid.NewGuid(), Monday, 10);

    [Fact]
    public void Protect_EmptyDay_MarksItProtected()
    {
        var activity = NewActivity();

        activity.Protect(Now);

        activity.IsProtected.ShouldBeTrue();
        activity.FreezeUsedAt.ShouldBe(Now);
        activity.IsGoalMet.ShouldBeFalse();
    }

    [Fact]
    public void Protect_PartialDay_KeepsAnsweredCount()
    {
        var activity = NewActivity();
        activity.RegisterAnswer(Now);
        activity.RegisterAnswer(Now);

        activity.Protect(Now);

        activity.IsProtected.ShouldBeTrue();
        activity.AnsweredCount.ShouldBe(2);
    }

    [Fact]
    public void Protect_GoalMetDay_Throws()
    {
        var activity = NewActivity();
        for (var i = 0; i < 10; i++)
        {
            activity.RegisterAnswer(Now);
        }

        Should.Throw<InvalidOperationException>(() => activity.Protect(Now));
    }

    [Fact]
    public void Protect_AlreadyProtectedDay_Throws()
    {
        var activity = NewActivity();
        activity.Protect(Now);

        Should.Throw<InvalidOperationException>(() => activity.Protect(Now));
    }
}