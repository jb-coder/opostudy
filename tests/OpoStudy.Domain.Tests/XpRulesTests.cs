using OpoStudy.Domain.Users;

using Shouldly;

namespace OpoStudy.Domain.Tests;

public sealed class XpRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 10, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(true, 10)]
    [InlineData(false, 2)]
    public void ForAnswer_CorrectOrNot_ReturnsXp(bool isCorrect, int expected) =>
        XpRules.ForAnswer(isCorrect).ShouldBe(expected);

    [Theory]
    [InlineData(1, 55)]
    [InlineData(9, 95)]
    [InlineData(10, 100)]
    [InlineData(30, 100)]
    public void ForGoalMet_Streak_ReturnsBonusWithCap(int streak, int expected) =>
        XpRules.ForGoalMet(streak).ShouldBe(expected);

    [Fact]
    public void ForGoalMet_ZeroStreak_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => XpRules.ForGoalMet(0));

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 100)]
    [InlineData(3, 300)]
    [InlineData(5, 1000)]
    [InlineData(10, 4500)]
    [InlineData(20, 19000)]
    public void XpToReach_Level_ReturnsThreshold(int level, int expected) =>
        XpRules.XpToReach(level).ShouldBe(expected);

    [Fact]
    public void XpToReach_ZeroLevel_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => XpRules.XpToReach(0));

    [Theory]
    [InlineData(0, 1)]
    [InlineData(99, 1)]
    [InlineData(100, 2)]
    [InlineData(299, 2)]
    [InlineData(300, 3)]
    [InlineData(4499, 9)]
    [InlineData(4500, 10)]
    [InlineData(495000, 100)]
    public void LevelFor_TotalXp_ReturnsLevel(int xp, int expected) =>
        XpRules.LevelFor(xp).ShouldBe(expected);

    [Fact]
    public void LevelFor_NegativeXp_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => XpRules.LevelFor(-1));

    [Fact]
    public void LevelFor_ThresholdsOfLevels2To200_AreExactAtTheEdges()
    {
        for (var level = 2; level <= 200; level++)
        {
            var threshold = XpRules.XpToReach(level);
            XpRules.LevelFor(threshold).ShouldBe(level);
            XpRules.LevelFor(threshold - 1).ShouldBe(level - 1);
        }
    }

    [Fact]
    public void GainXp_PositiveAmount_AddsToTotalAndLevel()
    {
        var user = User.Create("uid", Now);

        user.GainXp(100);

        user.Xp.ShouldBe(100);
        user.Level.ShouldBe(2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void GainXp_NonPositiveAmount_Throws(int amount) =>
        Should.Throw<ArgumentOutOfRangeException>(() => User.Create("uid", Now).GainXp(amount));

    [Fact]
    public void AddXp_PositiveAmount_Accumulates()
    {
        var activity = DailyActivity.Start(Guid.NewGuid(), new DateOnly(2026, 3, 10), 10);

        activity.AddXp(10);
        activity.AddXp(2);

        activity.XpEarned.ShouldBe(12);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddXp_NonPositiveAmount_Throws(int amount) =>
        Should.Throw<ArgumentOutOfRangeException>(
            () => DailyActivity.Start(Guid.NewGuid(), new DateOnly(2026, 3, 10), 10).AddXp(amount));
}
