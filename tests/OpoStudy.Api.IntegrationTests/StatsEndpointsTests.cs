using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

using OpoStudy.Contracts.Progress;
using OpoStudy.Contracts.Quizzes;
using OpoStudy.Contracts.Stats;
using OpoStudy.Contracts.Users;
using OpoStudy.Domain.Gradings;
using OpoStudy.Domain.PracticalCases;
using OpoStudy.Domain.Questions;
using OpoStudy.Domain.Quizzes;
using OpoStudy.Domain.Users;
using OpoStudy.Infrastructure.Persistence;

using Shouldly;

namespace OpoStudy.Api.IntegrationTests;

public sealed class StatsEndpointsTests(ApiFactory factory)
{
    private static readonly Uri MeUri = new("/api/v1/me", UriKind.Relative);
    private static readonly Uri StreakUri = new("/api/v1/me/streak", UriKind.Relative);
    private static readonly Uri LevelUri = new("/api/v1/me/level", UriKind.Relative);
    private static readonly Uri QuizzesUri = new("/api/v1/quizzes", UriKind.Relative);

    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string NewUid() => $"uid-{Guid.NewGuid():N}";

    private static Uri StatsUri(Guid examId) => new($"/api/v1/me/stats?examId={examId}", UriKind.Relative);

    private static Uri ActivityUri(string from, string to) => new($"/api/v1/me/activity?from={from}&to={to}", UriKind.Relative);

    // ---- Authorization and validation ----

    [Fact]
    public async Task GetStats_WithoutToken_Returns401()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(StatsUri(Guid.NewGuid()), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetActivity_WithoutToken_Returns401()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(ActivityUri("2026-01-01", "2026-01-31"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/api/v1/me/stats")]
    [InlineData("/api/v1/me/stats?examId=00000000-0000-0000-0000-000000000000")]
    public async Task GetStats_MissingOrEmptyExamId_Returns400WithExamIdKey(string url)
    {
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.GetAsync(new Uri(url, UriKind.Relative), Ct);

        var errors = await ReadValidationErrorsAsync(response);
        errors.Keys.ShouldBe(["examId"]);
        errors["examId"].ShouldBe(["La oposición es obligatoria."]);
    }

    [Fact]
    public async Task GetStats_MalformedExamId_Returns400()
    {
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.GetAsync(new Uri("/api/v1/me/stats?examId=not-a-guid", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetStats_UnknownExam_Returns404ExamNotFound()
    {
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.GetAsync(StatsUri(Guid.NewGuid()), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "exam.not_found");
    }

    [Fact]
    public async Task GetStats_InactiveExam_Returns404ExamNotFound()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.GetAsync(StatsUri(content.InactiveExam.Id), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "exam.not_found");
    }

    [Fact]
    public async Task GetActivity_MissingBothDates_Returns400WithBothKeys()
    {
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.GetAsync(new Uri("/api/v1/me/activity", UriKind.Relative), Ct);

        var errors = await ReadValidationErrorsAsync(response);
        errors.Keys.Order().ShouldBe(["from", "to"]);
        errors["from"].ShouldBe(["La fecha es obligatoria."]);
        errors["to"].ShouldBe(["La fecha es obligatoria."]);
    }

    [Theory]
    [InlineData("/api/v1/me/activity?to=2026-01-31", "from")]
    [InlineData("/api/v1/me/activity?from=2026-01-01", "to")]
    public async Task GetActivity_MissingOneDate_Returns400WithThatKey(string url, string key)
    {
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.GetAsync(new Uri(url, UriKind.Relative), Ct);

        var errors = await ReadValidationErrorsAsync(response);
        errors.Keys.ShouldBe([key]);
        errors[key].ShouldBe(["La fecha es obligatoria."]);
    }

    [Fact]
    public async Task GetActivity_FromAfterTo_Returns400WithFromKey()
    {
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.GetAsync(ActivityUri("2026-01-10", "2026-01-09"), Ct);

        var errors = await ReadValidationErrorsAsync(response);
        errors.Keys.ShouldBe(["from"]);
        errors["from"].ShouldBe(["La fecha inicial no puede ser posterior a la final."]);
    }

    [Fact]
    public async Task GetActivity_93Days_Returns400WithToKey()
    {
        using var client = factory.CreateClientFor(NewUid());

        // 2026-01-01 .. 2026-04-03 are 93 days, both ends included.
        var response = await client.GetAsync(ActivityUri("2026-01-01", "2026-04-03"), Ct);

        var errors = await ReadValidationErrorsAsync(response);
        errors.Keys.ShouldBe(["to"]);
        errors["to"].ShouldBe(["El periodo no puede superar los 92 días."]);
    }

    [Fact]
    public async Task GetActivity_92Days_ReturnsOk()
    {
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.GetAsync(ActivityUri("2026-01-01", "2026-04-02"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetActivity_FromEqualsTo_ReturnsThatDay()
    {
        var uid = NewUid();
        using var client = factory.CreateClientFor(uid);
        var me = await GetMeAsync(client);
        await SeedActivityAsync(me.Id, new DateOnly(2026, 1, 10), answered: 3);
        await SeedActivityAsync(me.Id, new DateOnly(2026, 1, 11), answered: 4);

        var activity = await GetActivityAsync(client, "2026-01-10", "2026-01-10");

        activity.Days.Select(d => d.Date).ShouldBe([new DateOnly(2026, 1, 10)]);
    }

    [Fact]
    public async Task GetActivity_RangeEndingAtMaxValue_DoesNotOverflow()
    {
        using var client = factory.CreateClientFor(NewUid());

        var single = await client.GetAsync(ActivityUri("9999-12-31", "9999-12-31"), Ct);
        var tooLong = await client.GetAsync(ActivityUri("2025-01-01", "9999-12-31"), Ct);
        var reversed = await client.GetAsync(ActivityUri("9999-12-31", "0001-01-01"), Ct);

        single.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadValidationErrorsAsync(tooLong)).Keys.ShouldBe(["to"]);
        (await ReadValidationErrorsAsync(reversed)).Keys.ShouldBe(["from"]);
    }

    [Theory]
    [InlineData("from=2026-13-01&to=2026-01-31")]
    [InlineData("from=2026-01-01&to=nope")]
    [InlineData("from=01/01/2026&to=31/01/2026")]
    public async Task GetActivity_MalformedDates_Returns400(string query)
    {
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.GetAsync(new Uri($"/api/v1/me/activity?{query}", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---- Quiz statistics ----

    [Fact]
    public async Task GetStats_NewUser_ReturnsZerosAndEmptyLists()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());

        var stats = await GetStatsAsync(client, content.Exam.Id);

        stats.ExamId.ShouldBe(content.Exam.Id);
        stats.Quizzes.AnsweredCount.ShouldBe(0);
        stats.Quizzes.CorrectCount.ShouldBe(0);
        stats.Quizzes.FinishedQuizCount.ShouldBe(0);
        stats.Quizzes.AvailableTopicCount.ShouldBe(2);
        stats.Quizzes.Topics.ShouldBeEmpty();
        stats.PracticalCases.CompletedCount.ShouldBe(0);
        stats.PracticalCases.AveragePercent.ShouldBeNull();
        stats.PracticalCases.BestScores.ShouldBeEmpty();
        stats.PracticalCases.RecentScores.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetActivity_NewUser_ReturnsNoDaysAndTodayAndGoal()
    {
        var time = new FakeTimeProvider(Now);
        using var host = WithTime(time);
        using var client = ApiFactory.CreateClientFor(host, NewUid());

        var activity = await GetActivityAsync(client, "2026-01-01", "2026-01-31");

        activity.Days.ShouldBeEmpty();
        activity.Today.ShouldBe(new DateOnly(2026, 1, 15));
        activity.DailyGoal.ShouldBe(10);
    }

    [Fact]
    public async Task GetStats_AllQuizModes_CountsAnswersByQuestionTopic()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var me = await GetMeAsync(client);
        var t1 = content.ActiveQuestionsOf(content.Topic1).ToList();
        var t2 = content.ActiveQuestionsOf(content.Topic2).ToList();

        // By-topic quiz, finished: topic 1 gets 2 of 4 correct.
        await SeedAttemptAsync(me.Id, content.Exam.Id, content.Topic1.Id, QuizMode.ByTopic, Now, [(t1[0], true), (t1[1], true), (t1[2], false), (t1[3], false)]);
        // Random quiz in progress (no topic): answers 2 of 3, one question is left unanswered.
        await SeedAttemptAsync(me.Id, content.Exam.Id, null, QuizMode.Random, Now.AddMinutes(1), [(t2[0], true), (t1[0], false)], unanswered: [t2[1]]);
        // Review quiz without topic, finished: repeats a question already answered.
        await SeedAttemptAsync(me.Id, content.Exam.Id, null, QuizMode.Review, Now.AddMinutes(2), [(t1[2], true)]);

        var stats = await GetStatsAsync(client, content.Exam.Id);

        stats.Quizzes.AnsweredCount.ShouldBe(7);
        stats.Quizzes.CorrectCount.ShouldBe(4);
        stats.Quizzes.FinishedQuizCount.ShouldBe(2);
        stats.Quizzes.Topics.Sum(t => t.AnsweredCount).ShouldBe(stats.Quizzes.AnsweredCount);
        stats.Quizzes.Topics.Sum(t => t.CorrectCount).ShouldBe(stats.Quizzes.CorrectCount);
        var topic1 = stats.Quizzes.Topics.Single(t => t.TopicId == content.Topic1.Id);
        topic1.ShouldSatisfyAllConditions(
            t => t.AnsweredCount.ShouldBe(6),
            t => t.CorrectCount.ShouldBe(3),
            t => t.Number.ShouldBe(1),
            t => t.Title.ShouldBe("Topic one"),
            t => t.Section.ShouldBe("Block I"),
            t => t.LastAnsweredAt.ShouldBe(Now.AddMinutes(2)),
            t => t.AvailableQuestionCount.ShouldBe(4));
        var topic2 = stats.Quizzes.Topics.Single(t => t.TopicId == content.Topic2.Id);
        (topic2.AnsweredCount, topic2.CorrectCount, topic2.Section, topic2.AvailableQuestionCount).ShouldBe((1, 1, null, 3));
    }

    [Fact]
    public async Task GetStats_AnswersToInactiveQuestions_StillCountAndTopicWithoutActiveQuestionsIsUnavailable()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var me = await GetMeAsync(client);
        var inactiveInTopic1 = content.InactiveQuestionOf(content.Topic1);
        var inactiveInEmptyTopic = content.InactiveQuestionOf(content.EmptyTopic);
        await SeedAttemptAsync(me.Id, content.Exam.Id, null, QuizMode.Random, Now, [(inactiveInTopic1, true), (inactiveInEmptyTopic, false)]);

        var stats = await GetStatsAsync(client, content.Exam.Id);

        stats.Quizzes.AnsweredCount.ShouldBe(2);
        stats.Quizzes.AvailableTopicCount.ShouldBe(2);
        stats.Quizzes.Topics.Single(t => t.TopicId == content.EmptyTopic.Id).AvailableQuestionCount.ShouldBe(0);
        stats.Quizzes.Topics.Single(t => t.TopicId == content.Topic1.Id).AvailableQuestionCount.ShouldBe(4);
    }

    [Fact]
    public async Task GetStats_QuizWithoutAnswers_IsNotCountedAsFinishedNorAnswered()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var me = await GetMeAsync(client);
        var t2 = content.ActiveQuestionsOf(content.Topic2).ToList();
        await SeedAttemptAsync(me.Id, content.Exam.Id, content.Topic2.Id, QuizMode.ByTopic, Now, [], unanswered: t2);

        var stats = await GetStatsAsync(client, content.Exam.Id);

        stats.Quizzes.AnsweredCount.ShouldBe(0);
        stats.Quizzes.FinishedQuizCount.ShouldBe(0);
        stats.Quizzes.Topics.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetStats_Topics_AreOrderedByExactRatioThenAnsweredThenNumber()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var me = await GetMeAsync(client);
        var t1 = content.ActiveQuestionsOf(content.Topic1).ToList();
        var t2 = content.ActiveQuestionsOf(content.Topic2).ToList();
        var empty = content.InactiveQuestionOf(content.EmptyTopic);

        // Topic 1: 1/2, topic 2: 1/2 (same ratio and answers: topic number decides), empty topic: 2/4 (same ratio, more answers).
        await SeedAttemptAsync(me.Id, content.Exam.Id, null, QuizMode.Random, Now, [(t1[0], true), (t1[1], false), (t2[0], true), (t2[1], false), (empty, true)]);
        for (var i = 0; i < 3; i++)
        {
            await SeedAttemptAsync(me.Id, content.Exam.Id, null, QuizMode.Random, Now.AddMinutes(i + 1), [(empty, i == 0)]);
        }

        var stats = await GetStatsAsync(client, content.Exam.Id);

        stats.Quizzes.Topics.Select(t => t.TopicId).ShouldBe([content.EmptyTopic.Id, content.Topic1.Id, content.Topic2.Id]);
    }

    [Fact]
    public async Task GetStats_Topics_AreComparedWithExactRatiosNotIntegerDivision()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var me = await GetMeAsync(client);
        var t1 = content.ActiveQuestionsOf(content.Topic1).ToList();
        var t2 = content.ActiveQuestionsOf(content.Topic2).ToList();
        var empty = content.InactiveQuestionOf(content.EmptyTopic);

        // Topic 1: 1/3 (0.33), topic 2: 0/2 (0), empty topic: 1/1 (1). Integer division would tie topics 1 and 2 at 0.
        await SeedAttemptAsync(me.Id, content.Exam.Id, null, QuizMode.Random, Now, [(t1[0], true), (t1[1], false), (t1[2], false), (t2[0], false), (t2[1], false), (empty, true)]);

        var stats = await GetStatsAsync(client, content.Exam.Id);

        stats.Quizzes.Topics.Select(t => t.TopicId).ShouldBe([content.Topic2.Id, content.Topic1.Id, content.EmptyTopic.Id]);
    }

    [Fact]
    public async Task GetStats_Topics_TiedRatioPutsMoreAnsweredFirstEvenWithHigherNumber()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var me = await GetMeAsync(client);
        var t1 = content.ActiveQuestionsOf(content.Topic1).ToList();
        var t2 = content.ActiveQuestionsOf(content.Topic2).ToList();

        // Both at 0 %: topic 2 has more answers than topic 1, so it goes first despite its higher number.
        await SeedAttemptAsync(me.Id, content.Exam.Id, null, QuizMode.Random, Now, [(t1[0], false), (t2[0], false), (t2[1], false)]);

        var stats = await GetStatsAsync(client, content.Exam.Id);

        stats.Quizzes.Topics.Select(t => t.TopicId).ShouldBe([content.Topic2.Id, content.Topic1.Id]);
    }

    [Fact]
    public async Task GetStats_OtherUsersAndOtherExams_DoNotMix()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        using var other = factory.CreateClientFor(NewUid());
        var me = await GetMeAsync(client);
        var otherUser = await GetMeAsync(other);
        var t1 = content.ActiveQuestionsOf(content.Topic1).ToList();
        var otherExamQuestion = content.Questions.Values.Single(q => q.TopicId == content.OtherExamTopic.Id);

        await SeedAttemptAsync(me.Id, content.Exam.Id, null, QuizMode.Random, Now, [(t1[0], true)]);
        await SeedAttemptAsync(otherUser.Id, content.Exam.Id, null, QuizMode.Random, Now, [(t1[0], false), (t1[1], false)]);
        await SeedAttemptAsync(me.Id, content.OtherExam.Id, null, QuizMode.Random, Now, [(otherExamQuestion, true)]);

        var mine = await GetStatsAsync(client, content.Exam.Id);
        var theirs = await GetStatsAsync(other, content.Exam.Id);
        var mineOtherExam = await GetStatsAsync(client, content.OtherExam.Id);

        (mine.Quizzes.AnsweredCount, mine.Quizzes.CorrectCount, mine.Quizzes.FinishedQuizCount).ShouldBe((1, 1, 1));
        (theirs.Quizzes.AnsweredCount, theirs.Quizzes.CorrectCount, theirs.Quizzes.FinishedQuizCount).ShouldBe((2, 0, 1));
        (mineOtherExam.Quizzes.AnsweredCount, mineOtherExam.Quizzes.FinishedQuizCount).ShouldBe((1, 1));
        mineOtherExam.Quizzes.Topics.Select(t => t.TopicId).ShouldBe([content.OtherExamTopic.Id]);
    }

    // ---- Practical cases ----

    [Theory]
    [InlineData(GradingStatus.Pending)]
    [InlineData(GradingStatus.Processing)]
    [InlineData(GradingStatus.Failed)]
    public async Task GetStats_OnlyCompletedGradingsOfTheExamCount(GradingStatus otherStatus)
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        using var other = factory.CreateClientFor(NewUid());
        var me = await GetMeAsync(client);
        var otherUser = await GetMeAsync(other);
        var caseA = content.PracticalCases.CaseA;
        await SeedGradingAsync(me.Id, caseA, Now, 10m, GradingStatus.Completed);
        await SeedGradingAsync(me.Id, caseA, Now.AddMinutes(1), 9m, otherStatus);
        await SeedGradingAsync(me.Id, content.PracticalCases.CaseOfInactiveExam, Now.AddMinutes(4), 5m, GradingStatus.Completed);
        await SeedGradingAsync(otherUser.Id, caseA, Now.AddMinutes(5), 0m, GradingStatus.Completed);

        var stats = (await GetStatsAsync(client, content.Exam.Id)).PracticalCases;

        stats.CompletedCount.ShouldBe(1);
        stats.AveragePercent.ShouldBe(100m);
        stats.BestScores.Single().ShouldSatisfyAllConditions(
            b => b.PracticalCaseId.ShouldBe(caseA.Id),
            b => b.CaseTitle.ShouldBe("Case a"),
            b => b.Section.ShouldBe("Section a"),
            b => b.GradingCount.ShouldBe(1),
            b => b.Score.ShouldBe(10m),
            b => b.MaxPoints.ShouldBe(10),
            b => b.Percent.ShouldBe(100m));
        stats.RecentScores.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetStats_PracticalCases_ComputesAverageBestScoresAndOrder()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var me = await GetMeAsync(client);
        var caseA = content.PracticalCases.CaseA;
        var caseB = content.PracticalCases.CaseB;
        var a1 = await SeedGradingAsync(me.Id, caseA, Now, 10m, GradingStatus.Completed);
        var a2 = await SeedGradingAsync(me.Id, caseA, Now.AddMinutes(1), 7.5m, GradingStatus.Completed);
        var b1 = await SeedGradingAsync(me.Id, caseB, Now.AddMinutes(2), 5m, GradingStatus.Completed);

        var stats = (await GetStatsAsync(client, content.Exam.Id)).PracticalCases;

        stats.CompletedCount.ShouldBe(3);
        stats.AveragePercent.ShouldBe(69.4m); // (100 + 75 + 33.33) / 3 = 69.44
        stats.BestScores.Select(b => b.PracticalCaseId).ShouldBe([caseA.Id, caseB.Id]);
        stats.BestScores[0].ShouldSatisfyAllConditions(
            b => b.GradingCount.ShouldBe(2),
            b => b.BestGradingId.ShouldBe(a1),
            b => b.Percent.ShouldBe(100m));
        stats.BestScores[1].ShouldSatisfyAllConditions(
            b => b.GradingCount.ShouldBe(1),
            b => b.BestGradingId.ShouldBe(b1),
            b => b.MaxPoints.ShouldBe(15),
            b => b.Percent.ShouldBe(33.3m));
        stats.RecentScores.Select(r => r.GradingId).ShouldBe([a1, a2, b1]);
        stats.RecentScores[1].ShouldSatisfyAllConditions(
            r => r.CaseTitle.ShouldBe("Case a"),
            r => r.SubmittedAt.ShouldBe(Now.AddMinutes(1)),
            r => r.Score.ShouldBe(7.5m),
            r => r.Percent.ShouldBe(75m));
    }

    [Fact]
    public async Task GetStats_PracticalCases_BestScoreTieGoesToTheMostRecent()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var me = await GetMeAsync(client);
        var caseA = content.PracticalCases.CaseA;
        await SeedGradingAsync(me.Id, caseA, Now, 8m, GradingStatus.Completed);
        var latest = await SeedGradingAsync(me.Id, caseA, Now.AddDays(1), 8m, GradingStatus.Completed);
        await SeedGradingAsync(me.Id, caseA, Now.AddDays(2), 6m, GradingStatus.Completed);

        var stats = (await GetStatsAsync(client, content.Exam.Id)).PracticalCases;

        stats.BestScores.Single().BestGradingId.ShouldBe(latest);
    }

    [Fact]
    public async Task GetStats_PracticalCases_MoreThanTenGradings_ReturnsTheLastTenOldestFirst()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var me = await GetMeAsync(client);
        var ids = new List<Guid>();
        for (var i = 0; i < 12; i++)
        {
            ids.Add(await SeedGradingAsync(me.Id, content.PracticalCases.CaseA, Now.AddHours(i), 5m, GradingStatus.Completed));
        }

        var stats = (await GetStatsAsync(client, content.Exam.Id)).PracticalCases;

        stats.CompletedCount.ShouldBe(12);
        stats.RecentScores.Select(r => r.GradingId).ShouldBe(ids.Skip(2));
        stats.RecentScores.Select(r => r.SubmittedAt).ShouldBe(stats.RecentScores.Select(r => r.SubmittedAt).Order());
    }

    // ---- Activity ----

    [Fact]
    public async Task GetActivity_ReturnsInclusiveRangeWithXpGoalAndProtectedDaysOmittingEmptyRows()
    {
        var time = new FakeTimeProvider(Now);
        using var host = WithTime(time);
        using var client = ApiFactory.CreateClientFor(host, NewUid());
        using var other = ApiFactory.CreateClientFor(host, NewUid());
        var me = await GetMeAsync(client);
        var otherUser = await GetMeAsync(other);
        await SeedActivityAsync(me.Id, new DateOnly(2025, 12, 31), answered: 6); // before the range
        await SeedActivityAsync(me.Id, new DateOnly(2026, 1, 1), answered: 6, xp: 60); // first day, included
        await SeedActivityAsync(me.Id, new DateOnly(2026, 1, 2), answered: 0); // no activity: omitted
        await SeedActivityAsync(me.Id, new DateOnly(2026, 1, 3), answered: 0, xp: 25); // XP only
        await SeedActivityAsync(me.Id, new DateOnly(2026, 1, 4), answered: 5, goal: 5, xp: 90); // goal met
        await SeedActivityAsync(me.Id, new DateOnly(2026, 1, 5), answered: 0, isProtected: true); // protected only
        await SeedActivityAsync(me.Id, new DateOnly(2026, 1, 31), answered: 2, xp: 20); // last day, included
        await SeedActivityAsync(me.Id, new DateOnly(2026, 2, 1), answered: 2, xp: 20); // after the range
        await SeedActivityAsync(otherUser.Id, new DateOnly(2026, 1, 10), answered: 9, xp: 99);

        var activity = await GetActivityAsync(client, "2026-01-01", "2026-01-31");

        activity.Days.Select(d => d.Date).ShouldBe(
            [new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 3), new DateOnly(2026, 1, 4), new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 31)]);
        activity.Days.ShouldBe(
        [
            new DayActivityResponse(new DateOnly(2026, 1, 1), 6, 10, false, false, 60),
            new DayActivityResponse(new DateOnly(2026, 1, 3), 0, 10, false, false, 25),
            new DayActivityResponse(new DateOnly(2026, 1, 4), 5, 5, true, false, 90),
            new DayActivityResponse(new DateOnly(2026, 1, 5), 0, 10, false, true, 0),
            new DayActivityResponse(new DateOnly(2026, 1, 31), 2, 10, false, false, 20),
        ]);
        (await GetActivityAsync(other, "2026-01-01", "2026-01-31")).Days.Single().XpEarned.ShouldBe(99);
    }

    [Fact]
    public async Task GetActivity_FutureEnd_IsAllowedAndTodayIsInTheUserTimeZone()
    {
        // 23:30 UTC on the 15th is already the 16th in Madrid.
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 1, 15, 23, 30, 0, TimeSpan.Zero));
        using var host = WithTime(time);
        using var client = ApiFactory.CreateClientFor(host, NewUid());

        var activity = await GetActivityAsync(client, "2026-01-01", "2026-01-31");

        activity.Today.ShouldBe(new DateOnly(2026, 1, 16));
    }

    [Fact]
    public async Task GetStreak_LastDays_ExposeXpEarnedOfEachDay()
    {
        var time = new FakeTimeProvider(Now);
        using var host = WithTime(time);
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = ApiFactory.CreateClientFor(host, NewUid());
        var me = await GetMeAsync(client);
        await SeedActivityAsync(me.Id, new DateOnly(2026, 1, 13), answered: 3, xp: 33);
        var createResponse = await client.PostAsJsonAsync(QuizzesUri, new CreateQuizRequest(content.Exam.Id, content.Topic2.Id, 3, false), Ct);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var quiz = (await createResponse.Content.ReadFromJsonAsync<QuizResponse>(Ct))!;
        var question = quiz.Questions[0];
        var optionId = content.Questions[question.QuestionId].CorrectOption.Id;
        var answer = await client.PostAsJsonAsync(
            new Uri($"/api/v1/quizzes/{quiz.Id}/answers", UriKind.Relative),
            new SubmitAnswerRequest(question.QuestionId, optionId),
            Ct);
        answer.StatusCode.ShouldBe(HttpStatusCode.OK);

        var streak = await client.GetFromJsonAsync<StreakResponse>(StreakUri, Ct);
        var level = await client.GetFromJsonAsync<LevelResponse>(LevelUri, Ct);

        streak.ShouldNotBeNull();
        level.ShouldNotBeNull();
        streak.LastDays.Single(d => d.Date == new DateOnly(2026, 1, 13)).XpEarned.ShouldBe(33);
        streak.LastDays.Single(d => d.Date == new DateOnly(2026, 1, 14)).XpEarned.ShouldBe(0);
        var today = streak.LastDays[^1];
        today.XpEarned.ShouldBeGreaterThan(0);
        today.XpEarned.ShouldBe(level.XpToday);
    }

    // ---- Helpers ----

    private WebApplicationFactory<Program> WithTime(FakeTimeProvider time) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(time);
        }));

    private static async Task<Dictionary<string, string[]>> ReadValidationErrorsAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationBody>(Ct);
        problem.ShouldNotBeNull();
        problem.Errors.ShouldNotBeNull();
        return problem.Errors;
    }

    private static async Task<UserProfileResponse> GetMeAsync(HttpClient client)
    {
        var me = await client.GetFromJsonAsync<UserProfileResponse>(MeUri, Ct);
        return me ?? throw new InvalidOperationException("The profile response was empty.");
    }

    private static async Task<StatsResponse> GetStatsAsync(HttpClient client, Guid examId)
    {
        var stats = await client.GetFromJsonAsync<StatsResponse>(StatsUri(examId), Ct);
        return stats ?? throw new InvalidOperationException("The stats response was empty.");
    }

    private static async Task<ActivityResponse> GetActivityAsync(HttpClient client, string from, string to)
    {
        var activity = await client.GetFromJsonAsync<ActivityResponse>(ActivityUri(from, to), Ct);
        return activity ?? throw new InvalidOperationException("The activity response was empty.");
    }

    /// <summary>Creates a quiz with the given answers (and optionally questions left unanswered) straight in the database.</summary>
    private async Task SeedAttemptAsync(
        Guid userId,
        Guid examId,
        Guid? topicId,
        QuizMode mode,
        DateTimeOffset startedAt,
        IReadOnlyList<(Question Question, bool Correct)> answers,
        IReadOnlyList<Question>? unanswered = null)
    {
        var questions = answers.Select(a => a.Question).Concat(unanswered ?? []).ToList();
        var attempt = QuizAttempt.Create(userId, examId, topicId, mode, [.. questions.Select(q => q.Id)], startedAt);
        for (var i = 0; i < answers.Count; i++)
        {
            var (question, correct) = answers[i];
            var option = question.Options.First(o => o.IsCorrect == correct);
            attempt.Answer(question, option.Id, startedAt.AddSeconds(i));
        }

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpoStudyDbContext>();
        db.Add(attempt);
        await db.SaveChangesAsync(Ct);
    }

    /// <summary>Creates a grading with the given status; the awarded points fill the criteria in order until <paramref name="score"/> is spent.</summary>
    private async Task<Guid> SeedGradingAsync(Guid userId, PracticalCase practicalCase, DateTimeOffset submittedAt, decimal score, GradingStatus status)
    {
        var grading = Grading.Submit(userId, practicalCase, [.. practicalCase.Parts.Select(_ => "Answer")], submittedAt);
        if (status != GradingStatus.Pending)
        {
            grading.Start(submittedAt);
        }

        if (status == GradingStatus.Completed)
        {
            var remaining = score;
            var parts = grading.Answers.Select(answer => new PartOutcome(
                [.. answer.Criteria.Select(criterion =>
                {
                    var awarded = Math.Min(remaining, criterion.Points);
                    remaining -= awarded;
                    return awarded;
                })],
                "Feedback")).ToList();
            grading.Complete(new GradingOutcome(parts, "Feedback", "test", null, null), submittedAt);
        }
        else if (status == GradingStatus.Failed)
        {
            grading.Fail("grading.provider_error", submittedAt);
        }

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpoStudyDbContext>();
        db.Add(grading);
        await db.SaveChangesAsync(Ct);
        return grading.Id;
    }

    private async Task SeedActivityAsync(Guid userId, DateOnly date, int answered, int goal = 10, int xp = 0, bool isProtected = false)
    {
        var activity = DailyActivity.Start(userId, date, goal);
        for (var i = 0; i < answered; i++)
        {
            activity.RegisterAnswer(Now);
        }

        if (xp > 0)
        {
            activity.AddXp(xp);
        }

        if (isProtected)
        {
            activity.Protect(Now);
        }

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpoStudyDbContext>();
        db.Add(activity);
        await db.SaveChangesAsync(Ct);
    }

    private sealed record ValidationBody(Dictionary<string, string[]>? Errors);
}