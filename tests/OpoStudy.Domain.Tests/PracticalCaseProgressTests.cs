using OpoStudy.Domain.Common;
using OpoStudy.Domain.Gradings;
using OpoStudy.Domain.PracticalCases;
using OpoStudy.Domain.Users;

using Shouldly;

namespace OpoStudy.Domain.Tests;

public sealed class PracticalCaseProgressTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Day = new(2026, 3, 10);

    [Theory]
    [InlineData(18.5, 25, 74)]
    [InlineData(25, 25, 100)]
    [InlineData(0, 25, 0)]
    [InlineData(0.5, 25, 2)]
    [InlineData(1.25, 25, 5)]
    [InlineData(1.125, 25, 5)]
    [InlineData(10, 20, 50)]
    [InlineData(30, 25, 100)]
    public void ForPracticalCase_Score_ReturnsRoundedAndCappedXp(double score, int maxPoints, int expected) =>
        XpRules.ForPracticalCase((decimal)score, maxPoints).ShouldBe(expected);

    [Theory]
    [InlineData(5, 0)]
    [InlineData(5, -1)]
    [InlineData(-1, 25)]
    public void ForPracticalCase_InvalidArguments_Throws(double score, int maxPoints) =>
        Should.Throw<ArgumentOutOfRangeException>(() => XpRules.ForPracticalCase((decimal)score, maxPoints));

    [Fact]
    public void MeetGoal_FirstTime_ReturnsTrueWithoutCountingAnswers()
    {
        var activity = DailyActivity.Start(Guid.NewGuid(), Day, 10);

        activity.MeetGoal(Now).ShouldBeTrue();

        activity.IsGoalMet.ShouldBeTrue();
        activity.GoalMetAt.ShouldBe(Now);
        activity.AnsweredCount.ShouldBe(0);
    }

    [Fact]
    public void MeetGoal_SecondTime_ReturnsFalse()
    {
        var activity = DailyActivity.Start(Guid.NewGuid(), Day, 10);
        activity.MeetGoal(Now);

        activity.MeetGoal(Now.AddHours(1)).ShouldBeFalse();

        activity.GoalMetAt.ShouldBe(Now);
    }

    [Fact]
    public void MeetGoal_AfterAnswersMetTheGoal_ReturnsFalse()
    {
        var activity = DailyActivity.Start(Guid.NewGuid(), Day, 5);
        for (var i = 0; i < 5; i++)
        {
            activity.RegisterAnswer(Now);
        }

        activity.MeetGoal(Now).ShouldBeFalse();
    }

    [Fact]
    public void MeetGoal_OnAProtectedDay_ReturnsTrue()
    {
        var activity = DailyActivity.Start(Guid.NewGuid(), Day, 10);
        activity.Protect(Now);

        activity.MeetGoal(Now).ShouldBeTrue();

        activity.IsProtected.ShouldBeTrue();
        activity.IsGoalMet.ShouldBeTrue();
    }

    // ---- Grading.RecordProgress ----

    private static Grading NewGrading()
    {
        var practicalCase = PracticalCase.Create(
            Guid.CreateVersion7(),
            "test/practical-cases/0001",
            "Title",
            null,
            "Statement",
            30,
            [new PracticalCasePartData("Part 0", "Model 0", [new GradingCriterionData("Criterion A", 2)])]);
        return Grading.Submit(Guid.CreateVersion7(), practicalCase, ["answer"], Now);
    }

    private static Grading NewCompleted()
    {
        var grading = NewGrading();
        grading.Start(Now);
        grading.Complete(new GradingOutcome([new PartOutcome([2m], "ok")], "ok", "test", null, null), Now);
        return grading;
    }

    [Fact]
    public void RecordProgress_WhenCompleted_StoresXpAndGoal()
    {
        var grading = NewCompleted();

        grading.RecordProgress(155, true);

        grading.XpEarned.ShouldBe(155);
        grading.MetDailyGoal.ShouldBeTrue();
    }

    [Fact]
    public void RecordProgress_BeforeProgressIsRecorded_LeavesXpNull()
    {
        var grading = NewCompleted();

        grading.XpEarned.ShouldBeNull();
        grading.MetDailyGoal.ShouldBeFalse();
    }

    [Fact]
    public void RecordProgress_WhenNotCompleted_Throws()
    {
        var grading = NewGrading();
        grading.Start(Now);

        Should.Throw<DomainRuleException>(() => grading.RecordProgress(10, false));
    }

    [Fact]
    public void RecordProgress_Twice_Throws()
    {
        var grading = NewCompleted();
        grading.RecordProgress(10, false);

        Should.Throw<InvalidOperationException>(() => grading.RecordProgress(10, false));
    }

    [Fact]
    public void RecordProgress_NegativeXp_Throws()
    {
        var grading = NewCompleted();

        Should.Throw<ArgumentOutOfRangeException>(() => grading.RecordProgress(-1, false));
    }
}