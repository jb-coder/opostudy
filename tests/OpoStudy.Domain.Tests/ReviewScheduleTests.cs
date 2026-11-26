using OpoStudy.Domain.Common;
using OpoStudy.Domain.Quizzes;

using Shouldly;

namespace OpoStudy.Domain.Tests;

public sealed class ReviewScheduleTests
{
    private static readonly Guid UserId = Guid.CreateVersion7();
    private static readonly Guid QuestionId = Guid.CreateVersion7();
    private static readonly DateOnly Day = new(2026, 3, 10);
    private static readonly DateTimeOffset At = new(2026, 3, 10, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(6, 64)]
    public void IntervalDays_Box_DoublesWithEachBox(int box, int days) =>
        ReviewSchedule.IntervalDays(box).ShouldBe(days);

    [Fact]
    public void Start_Correct_GoesToBox1DueInTwoDays()
    {
        var schedule = ReviewSchedule.Start(UserId, QuestionId, true, Day, At);

        schedule.Box.ShouldBe(1);
        schedule.DueOn.ShouldBe(Day.AddDays(2));
        schedule.LastAnsweredAt.ShouldBe(At);
    }

    [Fact]
    public void Start_Wrong_GoesToBox0DueTomorrow()
    {
        var schedule = ReviewSchedule.Start(UserId, QuestionId, false, Day, At);

        schedule.Box.ShouldBe(0);
        schedule.DueOn.ShouldBe(Day.AddDays(1));
    }

    [Fact]
    public void Start_EmptyIds_ThrowsDomainRule()
    {
        Should.Throw<DomainRuleException>(() => ReviewSchedule.Start(Guid.Empty, QuestionId, true, Day, At));
        Should.Throw<DomainRuleException>(() => ReviewSchedule.Start(UserId, Guid.Empty, true, Day, At));
    }

    [Fact]
    public void Register_CorrectWhenDue_ClimbsOneBoxAndMovesTheDate()
    {
        var schedule = ReviewSchedule.Start(UserId, QuestionId, true, Day, At);
        var dueDay = Day.AddDays(2);

        var changed = schedule.Register(true, dueDay, At.AddDays(2));

        changed.ShouldBeTrue();
        schedule.Box.ShouldBe(2);
        schedule.DueOn.ShouldBe(dueDay.AddDays(4));
        schedule.LastAnsweredAt.ShouldBe(At.AddDays(2));
    }

    [Fact]
    public void Register_CorrectWhenOverdue_UsesTheAnswerDayForTheNextDate()
    {
        var schedule = ReviewSchedule.Start(UserId, QuestionId, true, Day, At);
        var lateDay = Day.AddDays(10);

        schedule.Register(true, lateDay, At.AddDays(10));

        schedule.Box.ShouldBe(2);
        schedule.DueOn.ShouldBe(lateDay.AddDays(4));
    }

    [Fact]
    public void Register_CorrectAtBoxSix_StaysAtSixWithSixtyFourDays()
    {
        var schedule = ReviewSchedule.Start(UserId, QuestionId, true, Day, At);
        var day = Day;
        for (var i = 0; i < 8; i++)
        {
            day = schedule.DueOn;
            schedule.Register(true, day, At.AddDays(day.DayNumber - Day.DayNumber));
        }

        schedule.Box.ShouldBe(ReviewSchedule.MaxBox);
        schedule.DueOn.ShouldBe(day.AddDays(64));
    }

    [Fact]
    public void Register_Wrong_GoesBackToBox0DueTomorrow()
    {
        var schedule = ReviewSchedule.Start(UserId, QuestionId, true, Day, At);
        schedule.Register(true, schedule.DueOn, At.AddDays(2));
        var today = Day.AddDays(3);

        var changed = schedule.Register(false, today, At.AddDays(3));

        changed.ShouldBeTrue();
        schedule.Box.ShouldBe(0);
        schedule.DueOn.ShouldBe(today.AddDays(1));
    }

    [Fact]
    public void Register_CorrectBeforeDue_ChangesNothing()
    {
        var schedule = ReviewSchedule.Start(UserId, QuestionId, true, Day, At);
        var due = schedule.DueOn;

        var changed = schedule.Register(true, Day, At.AddHours(1));

        changed.ShouldBeFalse();
        schedule.Box.ShouldBe(1);
        schedule.DueOn.ShouldBe(due);
        schedule.LastAnsweredAt.ShouldBe(At);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Register_AnswerOlderThanLast_ChangesNothing(bool isCorrect)
    {
        var schedule = ReviewSchedule.Start(UserId, QuestionId, true, Day, At);
        var due = schedule.DueOn;

        var changed = schedule.Register(isCorrect, Day.AddDays(5), At.AddMinutes(-1));

        changed.ShouldBeFalse();
        schedule.Box.ShouldBe(1);
        schedule.DueOn.ShouldBe(due);
    }

    [Fact]
    public void IsDueOn_OnAndAfterDueDate_IsTrue()
    {
        var schedule = ReviewSchedule.Start(UserId, QuestionId, false, Day, At);

        schedule.IsDueOn(Day).ShouldBeFalse();
        schedule.IsDueOn(Day.AddDays(1)).ShouldBeTrue();
        schedule.IsDueOn(Day.AddDays(9)).ShouldBeTrue();
    }
}