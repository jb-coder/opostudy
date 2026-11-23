using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using OpoStudy.Domain.Exams;
using OpoStudy.Domain.Gradings;
using OpoStudy.Domain.PracticalCases;
using OpoStudy.Domain.Users;
using OpoStudy.Infrastructure.Gradings;
using OpoStudy.Infrastructure.Persistence;
using OpoStudy.Infrastructure.Progress;

using Shouldly;

namespace OpoStudy.Infrastructure.Tests;

/// <summary>
/// The queue is global to the database, so these tests live in one class (xunit runs a class sequentially)
/// and each one starts by emptying the gradings table.
/// </summary>
public sealed class GradingProcessorTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Start = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static GradingOptions Options(int maxAttempts = 3, TimeSpan? corrector = null) => new()
    {
        MaxAttempts = maxAttempts,
        RetryDelays = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5)],
        CorrectorTimeout = corrector ?? TimeSpan.FromSeconds(30),
        LeaseTimeout = TimeSpan.FromMinutes(10),
    };

    private static GradingOutcome ValidOutcome(GradingRequest request) =>
        new(
            [.. request.Parts.Select(p => new PartOutcome([.. p.Criteria.Select(c => (decimal)c.Points)], "part feedback"))],
            "general feedback",
            "stub",
            5,
            7);

    private static ILlmCorrector StubCorrector(Func<GradingRequest, CancellationToken, Task<GradingOutcome>> grade)
    {
        var corrector = Substitute.For<ILlmCorrector>();
        corrector.GradeAsync(Arg.Any<GradingRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => grade(call.Arg<GradingRequest>(), call.Arg<CancellationToken>()));
        return corrector;
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static GradingProcessor NewProcessor(OpoStudyDbContext db, ILlmCorrector corrector, FakeTimeProvider time, GradingOptions? options = null) =>
        new(db, corrector, new ProgressTracker(db), Microsoft.Extensions.Options.Options.Create(options ?? Options()), time, NullLogger<GradingProcessor>.Instance);

    private async Task<Grading> SeedGradingAsync(DateTimeOffset now, Action<Grading>? configure = null)
    {
        var exam = Exam.Create(Unique("exam"), "Programador", "Diputación de Málaga", "C1");
        var practicalCase = PracticalCase.Create(
            exam.Id,
            Unique("case"),
            "Case",
            null,
            "Case statement",
            30,
            [
                new PracticalCasePartData("Part 0", "Model 0", [new GradingCriterionData("A", 2), new GradingCriterionData("B", 3)]),
                new PracticalCasePartData("Part 1", "Model 1", [new GradingCriterionData("C", 4)]),
            ]);
        var user = User.Create(Unique("uid"), now);
        var grading = Grading.Submit(user.Id, practicalCase, [new string('a', 400), "short"], now);
        configure?.Invoke(grading);

        await using var db = postgres.CreateContext();
        db.AddRange(exam, practicalCase, user, grading);
        await db.SaveChangesAsync(Ct);
        return grading;
    }

    private async Task<Grading> ReloadAsync(Guid id)
    {
        await using var db = postgres.CreateContext();
        return await db.Gradings.AsNoTracking().SingleAsync(g => g.Id == id, Ct);
    }

    private async Task ClearQueueAsync()
    {
        await using var db = postgres.CreateContext();
        await db.Gradings.ExecuteDeleteAsync(Ct);
    }

    [Fact]
    public async Task ProcessNextAsync_WithEmptyQueue_ReturnsFalse()
    {
        await ClearQueueAsync();
        var time = new FakeTimeProvider(Start);
        await using var db = postgres.CreateContext();

        var processed = await NewProcessor(db, new FakeLlmCorrector(), time).ProcessNextAsync(Ct);

        processed.ShouldBeFalse();
    }

    [Fact]
    public async Task ProcessNextAsync_WithPendingGrading_CompletesItAndStoresTheRubricScores()
    {
        await ClearQueueAsync();
        var time = new FakeTimeProvider(Start);
        var seeded = await SeedGradingAsync(Start);
        await using var db = postgres.CreateContext();

        var processed = await NewProcessor(db, new FakeLlmCorrector(), time).ProcessNextAsync(Ct);

        processed.ShouldBeTrue();
        await using var check = postgres.CreateContext();
        var grading = await check.Gradings.AsNoTracking().SingleAsync(g => g.Id == seeded.Id, Ct);
        grading.Status.ShouldBe(GradingStatus.Completed);
        grading.Attempts.ShouldBe(1);
        grading.Corrector.ShouldBe("fake");
        grading.CompletedAt.ShouldBe(Start);

        // 400 characters earn the maximum in part 0; "short" (5 of 300) earns floor(4 * 5/300 * 2) / 2 = 0.
        grading.Answers[0].Criteria.Select(c => c.Awarded).ShouldBe([2m, 3m]);
        grading.Answers[1].Criteria.Select(c => c.Awarded).ShouldBe([0m]);
        grading.Score.ShouldBe(5m);
        grading.Answers.Select(a => a.Score).ShouldBe([5m, 0m]);
    }

    [Fact]
    public async Task ProcessNextAsync_PassesCurrentContentAndAnswersWithoutAnyUserData()
    {
        await ClearQueueAsync();
        var time = new FakeTimeProvider(Start);
        var seeded = await SeedGradingAsync(Start);
        GradingRequest? received = null;
        var corrector = StubCorrector((request, _) =>
        {
            received = request;
            return Task.FromResult(ValidOutcome(request));
        });
        await using var db = postgres.CreateContext();

        await NewProcessor(db, corrector, time).ProcessNextAsync(Ct);

        received.ShouldNotBeNull();
        received.CaseStatement.ShouldBe("Case statement");
        received.Parts.Select(p => p.Statement).ShouldBe(["Part 0", "Part 1"]);
        received.Parts.Select(p => p.ModelAnswer).ShouldBe(["Model 0", "Model 1"]);
        received.Parts.Select(p => p.Answer).ShouldBe([seeded.Answers[0].Text, "short"]);
        received.Parts[0].Criteria.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ProcessNextAsync_WithPendingGradingDueInTheFuture_DoesNotTakeIt()
    {
        await ClearQueueAsync();
        var time = new FakeTimeProvider(Start);
        var seeded = await SeedGradingAsync(Start.AddMinutes(1));
        await using var db = postgres.CreateContext();

        var processed = await NewProcessor(db, new FakeLlmCorrector(), time).ProcessNextAsync(Ct);

        processed.ShouldBeFalse();
        (await ReloadAsync(seeded.Id)).Status.ShouldBe(GradingStatus.Pending);
    }

    [Fact]
    public async Task ProcessNextAsync_WithTransientErrors_RetriesWithTheConfiguredDelaysAndThenFails()
    {
        await ClearQueueAsync();
        var time = new FakeTimeProvider(Start);
        var seeded = await SeedGradingAsync(Start);
        var corrector = StubCorrector((_, _) => throw new LlmTransientException("429"));

        await using (var db = postgres.CreateContext())
        {
            (await NewProcessor(db, corrector, time).ProcessNextAsync(Ct)).ShouldBeTrue();
        }

        var afterFirst = await ReloadAsync(seeded.Id);
        afterFirst.Status.ShouldBe(GradingStatus.Pending);
        afterFirst.Attempts.ShouldBe(1);
        afterFirst.NextAttemptAt.ShouldBe(Start.AddMinutes(1));

        await using (var db = postgres.CreateContext())
        {
            (await NewProcessor(db, corrector, time).ProcessNextAsync(Ct)).ShouldBeFalse();
        }

        time.Advance(TimeSpan.FromMinutes(1));
        await using (var db = postgres.CreateContext())
        {
            (await NewProcessor(db, corrector, time).ProcessNextAsync(Ct)).ShouldBeTrue();
        }

        var afterSecond = await ReloadAsync(seeded.Id);
        afterSecond.Status.ShouldBe(GradingStatus.Pending);
        afterSecond.Attempts.ShouldBe(2);
        afterSecond.NextAttemptAt.ShouldBe(Start.AddMinutes(1).AddMinutes(5));

        time.Advance(TimeSpan.FromMinutes(5));
        await using (var db = postgres.CreateContext())
        {
            (await NewProcessor(db, corrector, time).ProcessNextAsync(Ct)).ShouldBeTrue();
        }

        var final = await ReloadAsync(seeded.Id);
        final.Status.ShouldBe(GradingStatus.Failed);
        final.Attempts.ShouldBe(3);
        final.FailureCode.ShouldBe("grading.provider_unavailable");
    }

    [Fact]
    public async Task ProcessNextAsync_WhenTheCorrectorTimesOut_RetriesIt()
    {
        await ClearQueueAsync();
        var time = new FakeTimeProvider(Start);
        var seeded = await SeedGradingAsync(Start);
        var corrector = StubCorrector(async (request, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return ValidOutcome(request);
        });
        await using var db = postgres.CreateContext();

        var processed = await NewProcessor(db, corrector, time, Options(corrector: TimeSpan.FromMilliseconds(100))).ProcessNextAsync(Ct);

        processed.ShouldBeTrue();
        var grading = await ReloadAsync(seeded.Id);
        grading.Status.ShouldBe(GradingStatus.Pending);
        grading.NextAttemptAt.ShouldBe(Start.AddMinutes(1));
    }

    [Fact]
    public async Task ProcessNextAsync_WhenShuttingDown_PropagatesCancellationAndLeavesItProcessing()
    {
        await ClearQueueAsync();
        var time = new FakeTimeProvider(Start);
        var seeded = await SeedGradingAsync(Start);
        using var shutdown = new CancellationTokenSource();
        var corrector = StubCorrector(async (request, token) =>
        {
            await shutdown.CancelAsync();
            await Task.Delay(Timeout.Infinite, token);
            return ValidOutcome(request);
        });
        await using var db = postgres.CreateContext();

        await Should.ThrowAsync<OperationCanceledException>(() => NewProcessor(db, corrector, time).ProcessNextAsync(shutdown.Token));

        (await ReloadAsync(seeded.Id)).Status.ShouldBe(GradingStatus.Processing);
    }

    [Fact]
    public async Task ProcessNextAsync_WithFinalCorrectorError_FailsWithProviderError()
    {
        await ClearQueueAsync();
        var time = new FakeTimeProvider(Start);
        var seeded = await SeedGradingAsync(Start);
        var corrector = StubCorrector((_, _) => throw new InvalidOperationException("boom"));
        await using var db = postgres.CreateContext();

        (await NewProcessor(db, corrector, time).ProcessNextAsync(Ct)).ShouldBeTrue();

        var grading = await ReloadAsync(seeded.Id);
        grading.Status.ShouldBe(GradingStatus.Failed);
        grading.FailureCode.ShouldBe("grading.provider_error");
        grading.Attempts.ShouldBe(1);
    }

    [Fact]
    public async Task ProcessNextAsync_WithInvalidOutcome_FailsWithInvalidResult()
    {
        await ClearQueueAsync();
        var time = new FakeTimeProvider(Start);
        var seeded = await SeedGradingAsync(Start);
        var corrector = StubCorrector((_, _) => Task.FromResult(new GradingOutcome([new PartOutcome([1m], "f")], "f", "stub", null, null)));
        await using var db = postgres.CreateContext();

        (await NewProcessor(db, corrector, time).ProcessNextAsync(Ct)).ShouldBeTrue();

        var grading = await ReloadAsync(seeded.Id);
        grading.Status.ShouldBe(GradingStatus.Failed);
        grading.FailureCode.ShouldBe("grading.invalid_result");
        grading.Score.ShouldBeNull();
    }

    [Fact]
    public async Task ProcessNextAsync_WithExpiredLease_ClaimsItAgainAndCountsAnotherAttempt()
    {
        await ClearQueueAsync();
        var time = new FakeTimeProvider(Start);
        var seeded = await SeedGradingAsync(Start, g => g.Start(Start));
        time.Advance(TimeSpan.FromMinutes(11));
        await using var db = postgres.CreateContext();

        (await NewProcessor(db, new FakeLlmCorrector(), time).ProcessNextAsync(Ct)).ShouldBeTrue();

        var grading = await ReloadAsync(seeded.Id);
        grading.Status.ShouldBe(GradingStatus.Completed);
        grading.Attempts.ShouldBe(2);
    }

    [Fact]
    public async Task ProcessNextAsync_RequestBuildFails_MarksFailed()
    {
        await ClearQueueAsync();
        var time = new FakeTimeProvider(Start);
        var seeded = await SeedGradingAsync(Start);
        await using (var setup = postgres.CreateContext())
        {
            // The case lost its second part after the submission, so the request cannot be built.
            await setup.Database.ExecuteSqlInterpolatedAsync(
                $"""DELETE FROM practical_case_parts WHERE practical_case_id = {seeded.PracticalCaseId} AND "order" = 1""", Ct);
        }

        var corrector = Substitute.For<ILlmCorrector>();
        await using var db = postgres.CreateContext();

        (await NewProcessor(db, corrector, time).ProcessNextAsync(Ct)).ShouldBeTrue();

        var grading = await ReloadAsync(seeded.Id);
        grading.Status.ShouldBe(GradingStatus.Failed);
        grading.FailureCode.ShouldBe("grading.provider_error");
        await corrector.DidNotReceive().GradeAsync(Arg.Any<GradingRequest>(), Arg.Any<CancellationToken>());
        await using var check = postgres.CreateContext();
        (await check.Gradings.AnyAsync(g => g.Status == GradingStatus.Pending || g.Status == GradingStatus.Processing, Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task ProcessNextAsync_ExpiredLeaseAtMaxAttempts_MarksFailedWithoutCallingCorrector()
    {
        await ClearQueueAsync();
        var time = new FakeTimeProvider(Start);
        var seeded = await SeedGradingAsync(Start, g =>
        {
            g.Start(Start);
            g.Retry(Start, TimeSpan.Zero);
            g.Start(Start);
            g.Retry(Start, TimeSpan.Zero);
            g.Start(Start);
        });
        time.Advance(TimeSpan.FromMinutes(11));
        var corrector = Substitute.For<ILlmCorrector>();
        await using var db = postgres.CreateContext();

        (await NewProcessor(db, corrector, time).ProcessNextAsync(Ct)).ShouldBeTrue();

        var grading = await ReloadAsync(seeded.Id);
        grading.Status.ShouldBe(GradingStatus.Failed);
        grading.FailureCode.ShouldBe("grading.provider_unavailable");
        await corrector.DidNotReceive().GradeAsync(Arg.Any<GradingRequest>(), Arg.Any<CancellationToken>());
        await using var check = postgres.CreateContext();
        (await check.Gradings.AnyAsync(g => g.Status == GradingStatus.Pending || g.Status == GradingStatus.Processing, Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task ProcessNextAsync_WithValidLease_DoesNotTakeIt()
    {
        await ClearQueueAsync();
        var time = new FakeTimeProvider(Start);
        var seeded = await SeedGradingAsync(Start, g => g.Start(Start));
        time.Advance(TimeSpan.FromMinutes(9));
        await using var db = postgres.CreateContext();

        (await NewProcessor(db, new FakeLlmCorrector(), time).ProcessNextAsync(Ct)).ShouldBeFalse();

        (await ReloadAsync(seeded.Id)).Status.ShouldBe(GradingStatus.Processing);
    }

    [Fact]
    public async Task ProcessNextAsync_WithSeveralPending_TakesTheOldestFirst()
    {
        await ClearQueueAsync();
        var time = new FakeTimeProvider(Start.AddHours(1));
        var newer = await SeedGradingAsync(Start.AddMinutes(10));
        var older = await SeedGradingAsync(Start);
        await using var db = postgres.CreateContext();

        await NewProcessor(db, new FakeLlmCorrector(), time).ProcessNextAsync(Ct);

        (await ReloadAsync(older.Id)).Status.ShouldBe(GradingStatus.Completed);
        (await ReloadAsync(newer.Id)).Status.ShouldBe(GradingStatus.Pending);
    }

    [Fact]
    public async Task ProcessNextAsync_TwoProcessorsWithTwoGradings_EachTakesADifferentOne()
    {
        await ClearQueueAsync();
        var time = new FakeTimeProvider(Start);
        var first = await SeedGradingAsync(Start);
        var second = await SeedGradingAsync(Start);
        await using var db1 = postgres.CreateContext();
        await using var db2 = postgres.CreateContext();

        var results = await Task.WhenAll(
            NewProcessor(db1, new FakeLlmCorrector(), time).ProcessNextAsync(Ct),
            NewProcessor(db2, new FakeLlmCorrector(), time).ProcessNextAsync(Ct));

        results.ShouldBe([true, true]);
        var firstDone = await ReloadAsync(first.Id);
        var secondDone = await ReloadAsync(second.Id);
        firstDone.Status.ShouldBe(GradingStatus.Completed);
        secondDone.Status.ShouldBe(GradingStatus.Completed);
        firstDone.Attempts.ShouldBe(1);
        secondDone.Attempts.ShouldBe(1);
    }

    [Fact]
    public async Task ProcessNextAsync_TwoProcessorsWithOneGrading_OnlyOneTakesIt()
    {
        await ClearQueueAsync();
        var time = new FakeTimeProvider(Start);
        var seeded = await SeedGradingAsync(Start);
        await using var db1 = postgres.CreateContext();
        await using var db2 = postgres.CreateContext();

        var results = await Task.WhenAll(
            NewProcessor(db1, new FakeLlmCorrector(), time).ProcessNextAsync(Ct),
            NewProcessor(db2, new FakeLlmCorrector(), time).ProcessNextAsync(Ct));

        results.Count(r => r).ShouldBe(1);
        (await ReloadAsync(seeded.Id)).Attempts.ShouldBe(1);
    }

    [Fact]
    public async Task Gradings_WithAnotherInProgressOfTheSameUser_ViolateThePartialUniqueIndex()
    {
        await ClearQueueAsync();
        var seeded = await SeedGradingAsync(Start);
        var exam = Exam.Create(Unique("exam"), "Programador", "Diputación de Málaga", "C1");
        var practicalCase = PracticalCase.Create(
            exam.Id, Unique("case"), "Case", null, "Statement", 30, [new PracticalCasePartData("P", "M", [new GradingCriterionData("A", 1)])]);
        var second = Grading.Submit(seeded.UserId, practicalCase, ["answer"], Start);
        await using var db = postgres.CreateContext();
        db.AddRange(exam, practicalCase, second);

        var exception = await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));

        exception.InnerException.ShouldBeOfType<Npgsql.PostgresException>()
            .ConstraintName.ShouldBe(DbConstraintNames.GradingsOneInProgressPerUserIndex);
    }

    [Fact]
    public async Task Gradings_OnceCompleted_AllowTheUserToHaveAnotherInProgress()
    {
        await ClearQueueAsync();
        var time = new FakeTimeProvider(Start);
        var seeded = await SeedGradingAsync(Start);
        await using (var db = postgres.CreateContext())
        {
            await NewProcessor(db, new FakeLlmCorrector(), time).ProcessNextAsync(Ct);
        }

        var exam = Exam.Create(Unique("exam"), "Programador", "Diputación de Málaga", "C1");
        var practicalCase = PracticalCase.Create(
            exam.Id, Unique("case"), "Case", null, "Statement", 30, [new PracticalCasePartData("P", "M", [new GradingCriterionData("A", 1)])]);
        var second = Grading.Submit(seeded.UserId, practicalCase, ["answer"], Start);
        await using var save = postgres.CreateContext();
        save.AddRange(exam, practicalCase, second);

        await save.SaveChangesAsync(Ct);
    }

    // ---- Progress (spec 0021) ----

    private static readonly DateOnly Day = new(2026, 3, 10);

    private async Task<(User User, DailyActivity? Activity)> LoadProgressAsync(Guid userId, DateOnly day)
    {
        await using var db = postgres.CreateContext();
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == userId, Ct);
        var activity = await db.DailyActivities.AsNoTracking().SingleOrDefaultAsync(a => a.UserId == userId && a.Date == day, Ct);
        return (user, activity);
    }

    private async Task ProcessWithAsync(FakeTimeProvider time, ILlmCorrector? corrector = null, params IInterceptor[] interceptors)
    {
        await using var db = postgres.CreateContext(interceptors);
        await NewProcessor(db, corrector ?? new FakeLlmCorrector(), time).ProcessNextAsync(Ct);
    }

    [Fact]
    public async Task ProcessNextAsync_WhenCompleted_GivesXpGoalBonusAndStreak()
    {
        await ClearQueueAsync();
        var time = new FakeTimeProvider(Start);
        var seeded = await SeedGradingAsync(Start);

        await ProcessWithAsync(time);

        // Score 5 of 9 (the rubric): round(100 * 5 / 9) = 56, plus the goal bonus of a streak of 1 (55).
        var (user, activity) = await LoadProgressAsync(seeded.UserId, Day);
        user.Xp.ShouldBe(111);
        user.Streak.CurrentAt(Day).ShouldBe(1);
        activity.ShouldNotBeNull();
        activity.XpEarned.ShouldBe(111);
        activity.IsGoalMet.ShouldBeTrue();
        activity.AnsweredCount.ShouldBe(0);
        var grading = await ReloadAsync(seeded.Id);
        grading.Status.ShouldBe(GradingStatus.Completed);
        grading.XpEarned.ShouldBe(111);
        grading.MetDailyGoal.ShouldBeTrue();
    }

    [Fact]
    public async Task ProcessNextAsync_WhenTheDayWasAlreadyMet_GivesNoBonus()
    {
        await ClearQueueAsync();
        var time = new FakeTimeProvider(Start);
        var seeded = await SeedGradingAsync(Start);
        await using (var setup = postgres.CreateContext())
        {
            var met = DailyActivity.Start(seeded.UserId, Day, 10);
            met.MeetGoal(Start);
            setup.DailyActivities.Add(met);
            await setup.SaveChangesAsync(Ct);
        }

        await ProcessWithAsync(time);

        var (user, activity) = await LoadProgressAsync(seeded.UserId, Day);
        user.Xp.ShouldBe(56);
        activity.ShouldNotBeNull();
        activity.XpEarned.ShouldBe(56);
        var grading = await ReloadAsync(seeded.Id);
        grading.XpEarned.ShouldBe(56);
        grading.MetDailyGoal.ShouldBeFalse();
    }

    [Fact]
    public async Task ProcessNextAsync_SubmittedBeforeMidnightAndCompletedAfter_CountsForTheSubmissionDay()
    {
        await ClearQueueAsync();
        // Madrid is UTC+1 on 10 March: 22:59 UTC is 23:59 on the 10th and 23:01 UTC is 00:01 on the 11th.
        var submittedAt = new DateTimeOffset(2026, 3, 10, 22, 59, 0, TimeSpan.Zero);
        var time = new FakeTimeProvider(submittedAt.AddMinutes(2));
        var seeded = await SeedGradingAsync(submittedAt);

        await ProcessWithAsync(time);

        (await LoadProgressAsync(seeded.UserId, Day)).Activity.ShouldNotBeNull();
        (await LoadProgressAsync(seeded.UserId, Day.AddDays(1))).Activity.ShouldBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProcessNextAsync_WhenItFails_GivesNoXpAndCreatesNoActivity(bool invalidResult)
    {
        await ClearQueueAsync();
        var time = new FakeTimeProvider(Start);
        var seeded = await SeedGradingAsync(Start);
        var corrector = StubCorrector((_, _) => invalidResult
            ? Task.FromResult(new GradingOutcome([], "x", "stub", null, null))
            : throw new InvalidOperationException("boom"));

        await ProcessWithAsync(time, corrector);

        var (user, activity) = await LoadProgressAsync(seeded.UserId, Day);
        user.Xp.ShouldBe(0);
        activity.ShouldBeNull();
        var grading = await ReloadAsync(seeded.Id);
        grading.Status.ShouldBe(GradingStatus.Failed);
        grading.XpEarned.ShouldBeNull();
    }

    [Fact]
    public async Task ProcessNextAsync_WithConcurrentChangeOfTheUser_RetriesWithoutCallingTheCorrectorAgain()
    {
        await ClearQueueAsync();
        var time = new FakeTimeProvider(Start);
        var seeded = await SeedGradingAsync(Start);
        var calls = 0;
        var corrector = StubCorrector((request, _) =>
        {
            calls++;
            return Task.FromResult(ValidOutcome(request));
        });
        var interceptor = new ConcurrentUserChange(postgres, seeded.UserId);

        await ProcessWithAsync(time, corrector, interceptor);

        interceptor.Fired.ShouldBeTrue();
        calls.ShouldBe(1);
        var (user, _) = await LoadProgressAsync(seeded.UserId, Day);
        // The concurrent 25 XP plus the 155 of the grading (full marks: 100, plus the goal bonus 55).
        user.Xp.ShouldBe(180);
        (await ReloadAsync(seeded.Id)).XpEarned.ShouldBe(155);
    }

    [Fact]
    public async Task ProcessNextAsync_WhenTheDayActivityIsCreatedConcurrently_RetriesAndGrantsTheXpOnce()
    {
        await ClearQueueAsync();
        var time = new FakeTimeProvider(Start);
        var seeded = await SeedGradingAsync(Start);
        var calls = 0;
        var corrector = StubCorrector((request, _) =>
        {
            calls++;
            return Task.FromResult(ValidOutcome(request));
        });
        var interceptor = new ConcurrentUserChange(postgres, seeded.UserId, insertActivity: true);

        await ProcessWithAsync(time, corrector, interceptor);

        interceptor.Fired.ShouldBeTrue();
        calls.ShouldBe(1);
        var (user, activity) = await LoadProgressAsync(seeded.UserId, Day);
        var grading = await ReloadAsync(seeded.Id);
        grading.Status.ShouldBe(GradingStatus.Completed);
        // The concurrent activity had already met the day: full marks (100) and no bonus.
        grading.XpEarned.ShouldBe(100);
        user.Xp.ShouldBe(100);
        activity.ShouldNotBeNull();
        activity.XpEarned.ShouldBe(100);
    }

    [Fact]
    public async Task ProcessNextAsync_WhenTheSubmitDayIsAlreadyClosedByTheStreak_GivesTheBonusOfAStreakOfOne()
    {
        await ClearQueueAsync();
        var time = new FakeTimeProvider(Start);
        var seeded = await SeedGradingAsync(Start);
        await using (var setup = postgres.CreateContext())
        {
            var user = await setup.Users.SingleAsync(u => u.Id == seeded.UserId, Ct);
            for (var d = 8; d <= 12; d++)
            {
                user.Streak.RegisterGoalMet(new DateOnly(2026, 3, d));
            }

            await setup.SaveChangesAsync(Ct);
        }

        await ProcessWithAsync(time);

        var (user2, _) = await LoadProgressAsync(seeded.UserId, Day);
        // Score 56 + the bonus of a streak of 1 (55), not the one of the current streak of 5.
        user2.Xp.ShouldBe(111);
        user2.Streak.LastLength.ShouldBe(5);
        user2.Streak.LastMetOn.ShouldBe(new DateOnly(2026, 3, 12));
        (await ReloadAsync(seeded.Id)).MetDailyGoal.ShouldBeTrue();
    }

    /// <summary>Changes the user from another connection right before the completed grading is saved for the first time.</summary>
    private sealed class ConcurrentUserChange(PostgresFixture postgres, Guid userId, bool insertActivity = false) : SaveChangesInterceptor
    {
        public bool Fired { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var completing = eventData.Context!.ChangeTracker.Entries<Grading>().Any(e => e.Entity.Status == GradingStatus.Completed);
            if (completing && !Fired)
            {
                Fired = true;
                await using var other = postgres.CreateContext();
                if (insertActivity)
                {
                    var met = DailyActivity.Start(userId, new DateOnly(2026, 3, 10), 10);
                    met.MeetGoal(new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero));
                    other.DailyActivities.Add(met);
                }
                else
                {
                    var user = await other.Users.SingleAsync(u => u.Id == userId, cancellationToken);
                    user.GainXp(25);
                }

                await other.SaveChangesAsync(cancellationToken);
            }

            return result;
        }
    }
}
