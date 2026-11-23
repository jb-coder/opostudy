using OpoStudy.Domain.Common;
using OpoStudy.Domain.Gradings;
using OpoStudy.Domain.PracticalCases;

using Shouldly;

namespace OpoStudy.Domain.Tests;

public sealed class GradingTests
{
    private static readonly Guid UserId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    // Two parts: criteria 2 + 3 (5 points) and 4 (4 points).
    private static PracticalCase NewCase() =>
        PracticalCase.Create(
            Guid.CreateVersion7(),
            "test/practical-cases/0001",
            "Title",
            null,
            "Statement",
            30,
            [
                new PracticalCasePartData("Part 0", "Model 0", [new GradingCriterionData("Criterion A", 2), new GradingCriterionData("Criterion B", 3)]),
                new PracticalCasePartData("Part 1", "Model 1", [new GradingCriterionData("Criterion C", 4)]),
            ]);

    private static Grading NewGrading(params string[] answers) =>
        Grading.Submit(UserId, NewCase(), answers.Length == 0 ? ["answer 0", "answer 1"] : answers, Now);

    private static Grading NewProcessing()
    {
        var grading = NewGrading();
        grading.Start(Now);
        return grading;
    }

    private static GradingOutcome Outcome(
        decimal[]? part0 = null,
        decimal[]? part1 = null,
        string feedback = "General feedback",
        string corrector = "test",
        int? inputTokens = 10,
        int? outputTokens = 20) =>
        new(
            [new PartOutcome(part0 ?? [1.5m, 3m], "Feedback 0"), new PartOutcome(part1 ?? [4m], "Feedback 1")],
            feedback,
            corrector,
            inputTokens,
            outputTokens);

    private static void InvalidResult(Grading grading, GradingOutcome outcome)
    {
        Should.Throw<DomainRuleException>(() => grading.Complete(outcome, Now)).Code.ShouldBe("grading.invalid_result");
        grading.Status.ShouldBe(GradingStatus.Processing);
        grading.Score.ShouldBeNull();
        grading.Answers.ShouldAllBe(a => a.Score == null && a.Criteria.All(c => c.Awarded == null));
    }

    [Fact]
    public void Submit_WithValidAnswers_CreatesPendingGradingWithRubricCopy()
    {
        var practicalCase = NewCase();

        var grading = Grading.Submit(UserId, practicalCase, ["  first ", "second"], Now);

        grading.UserId.ShouldBe(UserId);
        grading.PracticalCaseId.ShouldBe(practicalCase.Id);
        grading.Status.ShouldBe(GradingStatus.Pending);
        grading.SubmittedAt.ShouldBe(Now);
        grading.NextAttemptAt.ShouldBe(Now);
        grading.Attempts.ShouldBe(0);
        grading.MaxPoints.ShouldBe(9);
        grading.Score.ShouldBeNull();
        grading.Answers.Select(a => a.Order).ShouldBe([0, 1]);
        grading.Answers.Select(a => a.Text).ShouldBe(["first", "second"]);
        grading.Answers.Select(a => a.MaxPoints).ShouldBe([5, 4]);
        grading.Answers[0].Criteria.ShouldBe([new CriterionScore("Criterion A", 2, null), new CriterionScore("Criterion B", 3, null)]);
        grading.Answers.ShouldAllBe(a => a.GradingId == grading.Id);
    }

    [Fact]
    public void Submit_WithOneBlankAnswer_KeepsItEmpty()
    {
        var grading = NewGrading("answer", "   ");

        grading.Answers[1].Text.ShouldBe(string.Empty);
    }

    [Fact]
    public void Submit_WithWrongNumberOfAnswers_Throws()
    {
        Should.Throw<DomainRuleException>(() => NewGrading("only one")).Code.ShouldBe("grading.invalid_answer_count");
        Should.Throw<DomainRuleException>(() => NewGrading("a", "b", "c")).Code.ShouldBe("grading.invalid_answer_count");
    }

    [Fact]
    public void Submit_WithTooLongAnswer_Throws()
    {
        var tooLong = new string('x', Limits.PartAnswer + 1);

        Should.Throw<DomainRuleException>(() => NewGrading("ok", tooLong)).Code.ShouldBe("grading.invalid_answer");
    }

    [Fact]
    public void Submit_WithAnswerOfMaxLength_Succeeds()
    {
        var grading = NewGrading(new string('x', Limits.PartAnswer), "ok");

        grading.Answers[0].Text.Length.ShouldBe(Limits.PartAnswer);
    }

    [Fact]
    public void Submit_WithAllAnswersBlank_Throws()
    {
        Should.Throw<DomainRuleException>(() => NewGrading("", "  ")).Code.ShouldBe("grading.empty");
    }

    [Fact]
    public void Submit_WithInactiveCase_Throws()
    {
        var practicalCase = NewCase();
        practicalCase.Deactivate();

        Should.Throw<DomainRuleException>(() => Grading.Submit(UserId, practicalCase, ["a", "b"], Now))
            .Code.ShouldBe("grading.case_inactive");
    }

    [Fact]
    public void Start_FromPending_MovesToProcessingAndCountsAttempt()
    {
        var grading = NewGrading();
        var later = Now.AddMinutes(1);

        grading.Start(later);

        grading.Status.ShouldBe(GradingStatus.Processing);
        grading.Attempts.ShouldBe(1);
        grading.StartedAt.ShouldBe(later);
    }

    [Fact]
    public void Start_FromExpiredProcessing_CountsAnotherAttempt()
    {
        var grading = NewProcessing();

        grading.Start(Now.AddMinutes(20));

        grading.Status.ShouldBe(GradingStatus.Processing);
        grading.Attempts.ShouldBe(2);
    }

    [Fact]
    public void Start_FromCompletedOrFailed_Throws()
    {
        var completed = NewProcessing();
        completed.Complete(Outcome(), Now);
        var failed = NewProcessing();
        failed.Fail("grading.provider_error", Now);

        Should.Throw<DomainRuleException>(() => completed.Start(Now)).Code.ShouldBe("grading.invalid_status");
        Should.Throw<DomainRuleException>(() => failed.Start(Now)).Code.ShouldBe("grading.invalid_status");
    }

    [Fact]
    public void Complete_WithValidOutcome_ComputesScores()
    {
        var grading = NewProcessing();
        var later = Now.AddSeconds(30);

        grading.Complete(Outcome(), later);

        grading.Status.ShouldBe(GradingStatus.Completed);
        grading.CompletedAt.ShouldBe(later);
        grading.Score.ShouldBe(8.5m);
        grading.Answers.Select(a => a.Score).ShouldBe([4.5m, 4m]);
        grading.Answers[0].Criteria.Select(c => c.Awarded).ShouldBe([1.5m, 3m]);
        grading.Answers[0].Feedback.ShouldBe("Feedback 0");
        grading.Feedback.ShouldBe("General feedback");
        grading.Corrector.ShouldBe("test");
        grading.InputTokens.ShouldBe(10);
        grading.OutputTokens.ShouldBe(20);
        grading.FailureCode.ShouldBeNull();
    }

    [Fact]
    public void Complete_FromPending_Throws()
    {
        var grading = NewGrading();

        Should.Throw<DomainRuleException>(() => grading.Complete(Outcome(), Now)).Code.ShouldBe("grading.invalid_status");
    }

    [Fact]
    public void Complete_WithWrongNumberOfParts_ThrowsAndChangesNothing()
    {
        var grading = NewProcessing();

        InvalidResult(grading, new GradingOutcome([new PartOutcome([1m, 1m], "f")], "f", "test", null, null));
    }

    [Fact]
    public void Complete_WithWrongNumberOfCriteria_ThrowsAndChangesNothing()
    {
        var grading = NewProcessing();

        InvalidResult(grading, Outcome(part0: [1m]));
        InvalidResult(grading, Outcome(part1: [1m, 1m]));
    }

    [Theory]
    [InlineData(-0.5)]
    [InlineData(2.5)]
    [InlineData(1.25)]
    [InlineData(0.1)]
    public void Complete_WithInvalidPoints_ThrowsAndChangesNothing(double awarded)
    {
        var grading = NewProcessing();

        // The first criterion is worth 2 points: negative, above it and not a multiple of 0.5 are all invalid.
        InvalidResult(grading, Outcome(part0: [(decimal)awarded, 1m]));
    }

    [Fact]
    public void Complete_WithTooLongFeedback_ThrowsAndChangesNothing()
    {
        var grading = NewProcessing();
        var tooLong = new string('x', Limits.GradingFeedback + 1);

        InvalidResult(grading, Outcome(feedback: tooLong));
        InvalidResult(grading, new GradingOutcome([new PartOutcome([1m, 1m], tooLong), new PartOutcome([1m], "f")], "f", "test", null, null));
    }

    [Fact]
    public void Complete_WithInvalidCorrectorOrTokens_Throws()
    {
        var grading = NewProcessing();

        InvalidResult(grading, Outcome(corrector: " "));
        InvalidResult(grading, Outcome(corrector: new string('x', Limits.Corrector + 1)));
        InvalidResult(grading, Outcome(inputTokens: -1));
    }

    [Fact]
    public void Complete_WithMaximumPoints_AcceptsThem()
    {
        var grading = NewProcessing();

        grading.Complete(Outcome(part0: [2m, 3m], part1: [4m]), Now);

        grading.Score.ShouldBe(9m);
    }

    [Fact]
    public void Retry_FromProcessing_MovesBackToPendingWithDelay()
    {
        var grading = NewProcessing();

        grading.Retry(Now, TimeSpan.FromMinutes(5));

        grading.Status.ShouldBe(GradingStatus.Pending);
        grading.NextAttemptAt.ShouldBe(Now.AddMinutes(5));
        grading.Attempts.ShouldBe(1);
    }

    [Fact]
    public void Retry_FromPending_Throws()
    {
        var grading = NewGrading();

        Should.Throw<DomainRuleException>(() => grading.Retry(Now, TimeSpan.Zero)).Code.ShouldBe("grading.invalid_status");
    }

    [Fact]
    public void Fail_FromProcessing_StoresCodeAndCompletionTime()
    {
        var grading = NewProcessing();

        grading.Fail("grading.provider_unavailable", Now.AddMinutes(1));

        grading.Status.ShouldBe(GradingStatus.Failed);
        grading.FailureCode.ShouldBe("grading.provider_unavailable");
        grading.CompletedAt.ShouldBe(Now.AddMinutes(1));
    }

    [Fact]
    public void Fail_FromPendingOrCompleted_Throws()
    {
        var pending = NewGrading();
        var completed = NewProcessing();
        completed.Complete(Outcome(), Now);

        Should.Throw<DomainRuleException>(() => pending.Fail("x", Now)).Code.ShouldBe("grading.invalid_status");
        Should.Throw<DomainRuleException>(() => completed.Fail("x", Now)).Code.ShouldBe("grading.invalid_status");
    }

    [Fact]
    public void PeriodAt_InUserTimeZone_StartsAndEndsAtLocalMonthBoundaries()
    {
        // 2026-03-31 12:00 UTC is already April 1st in Auckland (UTC+13 until April 5th).
        var (start, resetsAt) = GradingQuota.PeriodAt("Pacific/Auckland", new DateTimeOffset(2026, 3, 31, 12, 0, 0, TimeSpan.Zero));

        start.ShouldBe(new DateTimeOffset(2026, 3, 31, 11, 0, 0, TimeSpan.Zero));
        resetsAt.ShouldBe(new DateTimeOffset(2026, 4, 30, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void PeriodAt_InUtcAtEndOfYear_ResetsOnNextYear()
    {
        var (start, resetsAt) = GradingQuota.PeriodAt("UTC", new DateTimeOffset(2026, 12, 31, 23, 59, 0, TimeSpan.Zero));

        start.ShouldBe(new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero));
        resetsAt.ShouldBe(new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero));
    }
}