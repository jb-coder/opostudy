using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

using OpoStudy.Contracts.Progress;
using OpoStudy.Contracts.Quizzes;
using OpoStudy.Domain.Questions;
using OpoStudy.Infrastructure.Persistence;

using Shouldly;

namespace OpoStudy.Api.IntegrationTests;

public sealed class QuizSyncEndpointsTests(ApiFactory factory)
{
    private static readonly Uri SyncUri = new("/api/v1/quizzes/sync", UriKind.Relative);
    private static readonly Uri QuizzesUri = new("/api/v1/quizzes", UriKind.Relative);
    private static readonly Uri StreakUri = new("/api/v1/me/streak", UriKind.Relative);
    private static readonly Uri LevelUri = new("/api/v1/me/level", UriKind.Relative);
    private static readonly DateTimeOffset Today = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string NewUid() => $"uid-{Guid.NewGuid():N}";

    private static Uri QuizUri(Guid quizId) => new($"/api/v1/quizzes/{quizId}", UriKind.Relative);

    private static Uri AnswersUri(Guid quizId) => new($"/api/v1/quizzes/{quizId}/answers", UriKind.Relative);

    // ---- Creation ----

    [Fact]
    public async Task SyncQuiz_WithoutToken_Returns401()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(SyncUri, NewRequest(Guid.NewGuid(), Guid.NewGuid(), null, "Random", [Guid.NewGuid()], []), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SyncQuiz_NewByTopicQuiz_CreatesItWithItsQuestionsAndAnswers()
    {
        var time = new FakeTimeProvider(Today);
        await using var app = WithTime(time);
        var content = await ContentSeeder.SeedAsync(app.Services);
        using var client = ApiFactory.CreateClientFor(app, NewUid());
        var questions = content.ActiveQuestionsOf(content.Topic1).ToList();
        var answeredAt = Today.AddMinutes(-30);
        var request = NewRequest(
            Guid.CreateVersion7(),
            content.Exam.Id,
            content.Topic1.Id,
            "ByTopic",
            [.. questions.Select(q => q.Id)],
            [Correct(questions[0], answeredAt), Wrong(questions[1], answeredAt.AddMinutes(1))],
            startedAt: Today.AddHours(-1));

        var response = await client.PostAsJsonAsync(SyncUri, request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SyncQuizResponse>(Ct);
        body.ShouldBe(new SyncQuizResponse(request.Id, 2, 0, 0, 12, false));
        var quiz = await client.GetFromJsonAsync<QuizResponse>(QuizUri(request.Id), Ct);
        quiz.ShouldNotBeNull();
        quiz.Mode.ShouldBe("ByTopic");
        quiz.TopicId.ShouldBe(content.Topic1.Id);
        quiz.StartedAt.ShouldBe(request.StartedAt);
        quiz.Questions.Select(q => q.QuestionId).ShouldBe(request.QuestionIds);
        quiz.Questions[0].Feedback.ShouldNotBeNull().IsCorrect.ShouldBeTrue();
        quiz.Questions[1].Feedback.ShouldNotBeNull().IsCorrect.ShouldBeFalse();
        quiz.Questions[2].Feedback.ShouldBeNull();
        (await GetAnsweredAtAsync(app, request.Id, questions[0].Id)).ShouldBe(answeredAt);
    }

    [Fact]
    public async Task SyncQuiz_AnsweringEveryQuestion_FinishesTheQuiz()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var questions = content.ActiveQuestionsOf(content.Topic2).ToList();
        var now = DateTimeOffset.UtcNow;
        var request = NewRequest(
            Guid.CreateVersion7(),
            content.Exam.Id,
            null,
            "Random",
            [.. questions.Select(q => q.Id)],
            [.. questions.Select((q, i) => Correct(q, now.AddSeconds(-30 + i)))]);

        var response = await client.PostAsJsonAsync(SyncUri, request, Ct);

        var body = await response.Content.ReadFromJsonAsync<SyncQuizResponse>(Ct);
        body.ShouldNotBeNull();
        body.AcceptedCount.ShouldBe(3);
        body.QuizFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task SyncQuiz_RepeatedRequest_IsIdempotentAndDoesNotChangeXp()
    {
        var time = new FakeTimeProvider(Today);
        await using var app = WithTime(time);
        var content = await ContentSeeder.SeedAsync(app.Services);
        using var client = ApiFactory.CreateClientFor(app, NewUid());
        var questions = content.ActiveQuestionsOf(content.Topic1).ToList();
        var request = NewRequest(
            Guid.CreateVersion7(),
            content.Exam.Id,
            content.Topic1.Id,
            "ByTopic",
            [.. questions.Select(q => q.Id)],
            [Correct(questions[0], Today.AddMinutes(-5)), Correct(questions[1], Today.AddMinutes(-4))]);
        (await client.PostAsJsonAsync(SyncUri, request, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var xpAfterFirst = (await client.GetFromJsonAsync<LevelResponse>(LevelUri, Ct)).ShouldNotBeNull().TotalXp;

        var response = await client.PostAsJsonAsync(SyncUri, request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SyncQuizResponse>(Ct);
        body.ShouldBe(new SyncQuizResponse(request.Id, 0, 2, 0, 0, false));
        (await client.GetFromJsonAsync<LevelResponse>(LevelUri, Ct)).ShouldNotBeNull().TotalXp.ShouldBe(xpAfterFirst);
    }

    // ---- Existing quizzes ----

    [Fact]
    public async Task SyncQuiz_ExtendingAnOnlineQuiz_AddsOnlyTheNewAnswers()
    {
        var time = new FakeTimeProvider(Today);
        await using var app = WithTime(time);
        var content = await ContentSeeder.SeedAsync(app.Services);
        using var client = ApiFactory.CreateClientFor(app, NewUid());
        var quiz = await CreateOnlineQuizAsync(client, content.Exam.Id, content.Topic1.Id, 4);
        var questions = quiz.Questions.Select(q => content.Questions[q.QuestionId]).ToList();
        var first = await client.PostAsJsonAsync(AnswersUri(quiz.Id), new SubmitAnswerRequest(questions[0].Id, questions[0].CorrectOption.Id), Ct);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        time.Advance(TimeSpan.FromMinutes(5));

        // ExamId, TopicId, Mode and StartedAt are ignored for an existing quiz.
        var request = NewRequest(
            quiz.Id,
            Guid.NewGuid(),
            null,
            "Whatever",
            [.. questions.Select(q => q.Id)],
            [Correct(questions[0], Today), Correct(questions[1], Today.AddMinutes(2)), Wrong(questions[2], Today.AddMinutes(3))]);
        var response = await client.PostAsJsonAsync(SyncUri, request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SyncQuizResponse>(Ct);
        body.ShouldNotBeNull();
        (body.AcceptedCount, body.DuplicateCount, body.RejectedCount, body.QuizFinished).ShouldBe((2, 1, 0, false));
        var stored = await client.GetFromJsonAsync<QuizResponse>(QuizUri(quiz.Id), Ct);
        stored.ShouldNotBeNull();
        (stored.CorrectCount, stored.IncorrectCount).ShouldBe((2, 1));
    }

    [Fact]
    public async Task SyncQuiz_OnlineReviewQuiz_CanBeFinishedOffline()
    {
        var time = new FakeTimeProvider(Today);
        await using var app = WithTime(time);
        var content = await ContentSeeder.SeedAsync(app.Services);
        using var client = ApiFactory.CreateClientFor(app, NewUid());
        var quiz = await CreateOnlineQuizAsync(client, content.Exam.Id, content.Topic1.Id, 2);
        var wrong = content.Questions[quiz.Questions[0].QuestionId];
        var answered = await client.PostAsJsonAsync(AnswersUri(quiz.Id), new SubmitAnswerRequest(wrong.Id, wrong.Options.First(o => !o.IsCorrect).Id), Ct);
        answered.StatusCode.ShouldBe(HttpStatusCode.OK);
        var reviewResponse = await client.PostAsJsonAsync(QuizzesUri, new CreateQuizRequest(content.Exam.Id, null, 5, true), Ct);
        reviewResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var review = await reviewResponse.Content.ReadFromJsonAsync<QuizResponse>(Ct);
        review.ShouldNotBeNull();
        review.Mode.ShouldBe("Review");

        var request = NewRequest(
            review.Id,
            content.Exam.Id,
            null,
            "Review",
            [.. review.Questions.Select(q => q.QuestionId)],
            [Correct(wrong, Today)]);
        var response = await client.PostAsJsonAsync(SyncUri, request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SyncQuizResponse>(Ct);
        body.ShouldNotBeNull();
        body.AcceptedCount.ShouldBe(1);
        body.QuizFinished.ShouldBeTrue();
    }

    [Fact]
    public async Task SyncQuiz_ExistingQuizWithDifferentQuestions_Returns409()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var quiz = await CreateOnlineQuizAsync(client, content.Exam.Id, content.Topic1.Id, 4);
        var ids = quiz.Questions.Select(q => q.QuestionId).ToList();
        var reordered = NewRequest(quiz.Id, content.Exam.Id, content.Topic1.Id, "ByTopic", [.. ids.AsEnumerable().Reverse()], []);
        var fewer = NewRequest(quiz.Id, content.Exam.Id, content.Topic1.Id, "ByTopic", [.. ids.Take(3)], []);

        await (await client.PostAsJsonAsync(SyncUri, reordered, Ct)).ShouldBeProblemAsync(HttpStatusCode.Conflict, "quiz.sync_conflict");
        await (await client.PostAsJsonAsync(SyncUri, fewer, Ct)).ShouldBeProblemAsync(HttpStatusCode.Conflict, "quiz.sync_conflict");
    }

    [Fact]
    public async Task SyncQuiz_QuizOfAnotherUser_Returns409AndDoesNotModifyIt()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var owner = factory.CreateClientFor(NewUid());
        using var intruder = factory.CreateClientFor(NewUid());
        var quiz = await CreateOnlineQuizAsync(owner, content.Exam.Id, content.Topic1.Id, 4);
        var questions = quiz.Questions.Select(q => content.Questions[q.QuestionId]).ToList();
        var request = NewRequest(
            quiz.Id,
            content.Exam.Id,
            content.Topic1.Id,
            "ByTopic",
            [.. questions.Select(q => q.Id)],
            [Correct(questions[0], DateTimeOffset.UtcNow)]);

        var response = await intruder.PostAsJsonAsync(SyncUri, request, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "quiz.sync_conflict");
        var stored = await owner.GetFromJsonAsync<QuizResponse>(QuizUri(quiz.Id), Ct);
        stored.ShouldNotBeNull();
        stored.Questions.ShouldAllBe(q => q.Feedback == null);
    }

    // ---- Validation ----

    [Fact]
    public async Task SyncQuiz_WithEmptyIds_ReturnsValidationProblem()
    {
        using var client = factory.CreateClientFor(NewUid());
        var request = NewRequest(Guid.Empty, Guid.Empty, null, "Random", [Guid.Empty], []);

        var response = await client.PostAsJsonAsync(SyncUri, request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadAsStringAsync(Ct);
        json.ShouldContain("\"id\"");
        json.ShouldContain("\"examId\"");
        json.ShouldContain("\"questionIds\"");
    }

    [Fact]
    public async Task SyncQuiz_WithNoQuestions_ReturnsValidationProblem()
    {
        using var client = factory.CreateClientFor(NewUid());
        var request = NewRequest(Guid.CreateVersion7(), Guid.NewGuid(), null, "Random", [], []);

        var response = await client.PostAsJsonAsync(SyncUri, request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("\"questionIds\"");
    }

    [Fact]
    public async Task SyncQuiz_WithRepeatedQuestions_ReturnsValidationProblem()
    {
        using var client = factory.CreateClientFor(NewUid());
        var questionId = Guid.NewGuid();
        var request = NewRequest(Guid.CreateVersion7(), Guid.NewGuid(), null, "Random", [questionId, questionId], []);

        var response = await client.PostAsJsonAsync(SyncUri, request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("\"questionIds\"");
    }

    [Fact]
    public async Task SyncQuiz_WithMoreThanTheMaximumQuestions_ReturnsValidationProblem()
    {
        using var client = factory.CreateClientFor(NewUid());
        var ids = Enumerable.Range(0, 101).Select(_ => Guid.NewGuid()).ToList();
        var request = NewRequest(Guid.CreateVersion7(), Guid.NewGuid(), null, "Random", ids, []);

        var response = await client.PostAsJsonAsync(SyncUri, request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("\"questionIds\"");
    }

    [Fact]
    public async Task SyncQuiz_WithMoreAnswersThanQuestions_ReturnsValidationProblem()
    {
        using var client = factory.CreateClientFor(NewUid());
        var q1 = Guid.NewGuid();
        var q2 = Guid.NewGuid();
        var request = NewRequest(
            Guid.CreateVersion7(),
            Guid.NewGuid(),
            null,
            "Random",
            [q1],
            [new SyncAnswerRequest(q1, Guid.NewGuid(), Today), new SyncAnswerRequest(q2, Guid.NewGuid(), Today)]);

        var response = await client.PostAsJsonAsync(SyncUri, request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("\"answers\"");
    }

    [Fact]
    public async Task SyncQuiz_WithRepeatedOrEmptyAnswers_ReturnsValidationProblem()
    {
        using var client = factory.CreateClientFor(NewUid());
        var q1 = Guid.NewGuid();
        var q2 = Guid.NewGuid();
        var repeated = NewRequest(
            Guid.CreateVersion7(),
            Guid.NewGuid(),
            null,
            "Random",
            [q1, q2],
            [new SyncAnswerRequest(q1, Guid.NewGuid(), Today), new SyncAnswerRequest(q1, Guid.NewGuid(), Today)]);
        var empty = NewRequest(
            Guid.CreateVersion7(),
            Guid.NewGuid(),
            null,
            "Random",
            [q1],
            [new SyncAnswerRequest(Guid.Empty, Guid.NewGuid(), Today)]);

        (await client.PostAsJsonAsync(SyncUri, repeated, Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync(SyncUri, empty, Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SyncQuiz_NewReviewQuiz_ReturnsValidationProblem()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var request = NewRequest(
            Guid.CreateVersion7(),
            content.Exam.Id,
            null,
            "Review",
            [.. content.ActiveQuestionsOf(content.Topic1).Select(q => q.Id)],
            []);

        var response = await client.PostAsJsonAsync(SyncUri, request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("\"mode\"");
    }

    [Fact]
    public async Task SyncQuiz_NewQuizWithTopicInconsistentWithMode_ReturnsValidationProblem()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var ids = content.ActiveQuestionsOf(content.Topic1).Select(q => q.Id).ToList();
        var byTopicWithoutTopic = NewRequest(Guid.CreateVersion7(), content.Exam.Id, null, "ByTopic", ids, []);
        var randomWithTopic = NewRequest(Guid.CreateVersion7(), content.Exam.Id, content.Topic1.Id, "Random", ids, []);

        (await client.PostAsJsonAsync(SyncUri, byTopicWithoutTopic, Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync(SyncUri, randomWithTopic, Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---- Content checks ----

    [Fact]
    public async Task SyncQuiz_UnknownOrInactiveExam_Returns404()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var ids = content.ActiveQuestionsOf(content.Topic1).Select(q => q.Id).ToList();

        var unknown = await client.PostAsJsonAsync(SyncUri, NewRequest(Guid.CreateVersion7(), Guid.NewGuid(), null, "Random", ids, []), Ct);
        var inactive = await client.PostAsJsonAsync(SyncUri, NewRequest(Guid.CreateVersion7(), content.InactiveExam.Id, null, "Random", ids, []), Ct);

        await unknown.ShouldBeProblemAsync(HttpStatusCode.NotFound, "exam.not_found");
        await inactive.ShouldBeProblemAsync(HttpStatusCode.NotFound, "exam.not_found");
    }

    [Fact]
    public async Task SyncQuiz_TopicOfAnotherExam_Returns404()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var ids = content.ActiveQuestionsOf(content.Topic1).Select(q => q.Id).ToList();
        var request = NewRequest(Guid.CreateVersion7(), content.Exam.Id, content.OtherExamTopic.Id, "ByTopic", ids, []);

        var response = await client.PostAsJsonAsync(SyncUri, request, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "topic.not_found");
    }

    [Fact]
    public async Task SyncQuiz_QuestionOfAnotherExam_Returns422()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var foreign = content.ActiveQuestionsOf(content.OtherExamTopic).Single();
        var ids = content.ActiveQuestionsOf(content.Topic1).Select(q => q.Id).Append(foreign.Id).ToList();
        var request = NewRequest(Guid.CreateVersion7(), content.Exam.Id, null, "Random", ids, []);

        var response = await client.PostAsJsonAsync(SyncUri, request, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "quiz.invalid_questions");
    }

    [Fact]
    public async Task SyncQuiz_ByTopicWithQuestionOfAnotherTopicOrUnknown_Returns422()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var topic2Question = content.ActiveQuestionsOf(content.Topic2).First();
        var ofOtherTopic = NewRequest(Guid.CreateVersion7(), content.Exam.Id, content.Topic1.Id, "ByTopic", [topic2Question.Id], []);
        var unknown = NewRequest(Guid.CreateVersion7(), content.Exam.Id, content.Topic1.Id, "ByTopic", [Guid.NewGuid()], []);

        await (await client.PostAsJsonAsync(SyncUri, ofOtherTopic, Ct)).ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "quiz.invalid_questions");
        await (await client.PostAsJsonAsync(SyncUri, unknown, Ct)).ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "quiz.invalid_questions");
    }

    [Fact]
    public async Task SyncQuiz_QuestionDeactivatedAfterCaching_IsStillAccepted()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var inactive = content.InactiveQuestionOf(content.Topic1);
        var request = NewRequest(
            Guid.CreateVersion7(),
            content.Exam.Id,
            content.Topic1.Id,
            "ByTopic",
            [inactive.Id],
            [Correct(inactive, DateTimeOffset.UtcNow)]);

        var response = await client.PostAsJsonAsync(SyncUri, request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<SyncQuizResponse>(Ct)).ShouldNotBeNull().AcceptedCount.ShouldBe(1);
    }

    // ---- Answers ----

    [Fact]
    public async Task SyncQuiz_ForeignOptionOrQuestion_IsRejectedWithoutBlockingTheRest()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var questions = content.ActiveQuestionsOf(content.Topic1).ToList();
        var now = DateTimeOffset.UtcNow;
        var request = NewRequest(
            Guid.CreateVersion7(),
            content.Exam.Id,
            content.Topic1.Id,
            "ByTopic",
            [.. questions.Take(3).Select(q => q.Id)],
            [
                new SyncAnswerRequest(questions[0].Id, questions[1].Options[0].Id, now.AddSeconds(-10)),
                new SyncAnswerRequest(questions[3].Id, questions[3].CorrectOption.Id, now.AddSeconds(-9)),
                Correct(questions[1], now.AddSeconds(-8)),
            ]);

        var response = await client.PostAsJsonAsync(SyncUri, request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SyncQuizResponse>(Ct);
        body.ShouldNotBeNull();
        (body.AcceptedCount, body.DuplicateCount, body.RejectedCount).ShouldBe((1, 0, 2));
    }

    [Fact]
    public async Task SyncQuiz_FutureAndTooOldTimes_AreClampedToNowAndSevenDaysAgo()
    {
        var time = new FakeTimeProvider(Today);
        await using var app = WithTime(time);
        var content = await ContentSeeder.SeedAsync(app.Services);
        using var client = ApiFactory.CreateClientFor(app, NewUid());
        var questions = content.ActiveQuestionsOf(content.Topic1).ToList();
        var request = NewRequest(
            Guid.CreateVersion7(),
            content.Exam.Id,
            content.Topic1.Id,
            "ByTopic",
            [.. questions.Select(q => q.Id)],
            [Correct(questions[0], Today.AddHours(5)), Correct(questions[1], Today.AddDays(-10))],
            startedAt: Today.AddDays(-30));

        var response = await client.PostAsJsonAsync(SyncUri, request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await GetAnsweredAtAsync(app, request.Id, questions[0].Id)).ShouldBe(Today);
        (await GetAnsweredAtAsync(app, request.Id, questions[1].Id)).ShouldBe(Today.AddDays(-7));
        var quiz = await client.GetFromJsonAsync<QuizResponse>(QuizUri(request.Id), Ct);
        quiz.ShouldNotBeNull().StartedAt.ShouldBe(Today.AddDays(-7));
    }

    // ---- Progress ----

    [Fact]
    public async Task SyncQuiz_TenAnswersOfYesterday_MeetYesterdaysGoalWithStreakAndBonus()
    {
        var time = new FakeTimeProvider(Today);
        await using var app = WithTime(time);
        var content = await ContentSeeder.SeedAsync(app.Services, activeQuestionsInTopic1: 12);
        using var client = ApiFactory.CreateClientFor(app, NewUid());
        var questions = content.ActiveQuestionsOf(content.Topic1).ToList();
        var yesterday = Today.AddDays(-1);
        var request = NewRequest(
            Guid.CreateVersion7(),
            content.Exam.Id,
            content.Topic1.Id,
            "ByTopic",
            [.. questions.Select(q => q.Id)],
            [.. questions.Take(10).Select((q, i) => Correct(q, yesterday.AddSeconds(i)))],
            startedAt: yesterday.AddMinutes(-1));

        var response = await client.PostAsJsonAsync(SyncUri, request, Ct);

        var body = await response.Content.ReadFromJsonAsync<SyncQuizResponse>(Ct);
        body.ShouldNotBeNull();
        body.AcceptedCount.ShouldBe(10);
        body.XpEarned.ShouldBe((10 * 10) + 55);
        var streak = await client.GetFromJsonAsync<StreakResponse>(StreakUri, Ct);
        streak.ShouldNotBeNull();
        streak.CurrentStreak.ShouldBe(1);
        streak.AnsweredToday.ShouldBe(0);
        var day = streak.LastDays.Single(d => d.Date == new DateOnly(2026, 1, 14));
        (day.AnsweredCount, day.GoalMet).ShouldBe((10, true));
        (await client.GetFromJsonAsync<LevelResponse>(LevelUri, Ct)).ShouldNotBeNull().XpToday.ShouldBe(0);
    }

    [Fact]
    public async Task SyncQuiz_AnswersOfToday_CountForToday()
    {
        var time = new FakeTimeProvider(Today);
        await using var app = WithTime(time);
        var content = await ContentSeeder.SeedAsync(app.Services, activeQuestionsInTopic1: 12);
        using var client = ApiFactory.CreateClientFor(app, NewUid());
        var questions = content.ActiveQuestionsOf(content.Topic1).ToList();
        var request = NewRequest(
            Guid.CreateVersion7(),
            content.Exam.Id,
            content.Topic1.Id,
            "ByTopic",
            [.. questions.Select(q => q.Id)],
            [.. questions.Take(4).Select((q, i) => Correct(q, Today.AddMinutes(-10 + i)))]);

        var response = await client.PostAsJsonAsync(SyncUri, request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var streak = await client.GetFromJsonAsync<StreakResponse>(StreakUri, Ct);
        streak.ShouldNotBeNull().AnsweredToday.ShouldBe(4);
        (await client.GetFromJsonAsync<LevelResponse>(LevelUri, Ct)).ShouldNotBeNull().XpToday.ShouldBe(40);
    }

    [Fact]
    public async Task SyncQuiz_LateAnswersOfASettledDayWithLostStreak_AddXpWithoutChangingTheStreak()
    {
        var day10 = new DateTimeOffset(2026, 1, 10, 10, 0, 0, TimeSpan.Zero);
        var time = new FakeTimeProvider(day10);
        await using var app = WithTime(time);
        var content = await ContentSeeder.SeedAsync(app.Services, activeQuestionsInTopic1: 12);
        using var client = ApiFactory.CreateClientFor(app, NewUid());

        // Meet the goal on the 10th online, then lose the streak by not playing until the 15th.
        var quiz = await CreateOnlineQuizAsync(client, content.Exam.Id, content.Topic1.Id, 10);
        foreach (var item in quiz.Questions)
        {
            var q = content.Questions[item.QuestionId];
            var answered = await client.PostAsJsonAsync(AnswersUri(quiz.Id), new SubmitAnswerRequest(q.Id, q.CorrectOption.Id), Ct);
            answered.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        time.SetUtcNow(Today);
        (await client.GetFromJsonAsync<StreakResponse>(StreakUri, Ct)).ShouldNotBeNull().CurrentStreak.ShouldBe(0);
        var xpBefore = (await client.GetFromJsonAsync<LevelResponse>(LevelUri, Ct)).ShouldNotBeNull().TotalXp;

        // Late answers of the 9th: a day behind the settled streak.
        var questions = content.ActiveQuestionsOf(content.Topic2).Concat(content.ActiveQuestionsOf(content.Topic1)).Take(10).ToList();
        var late = new DateTimeOffset(2026, 1, 9, 10, 0, 0, TimeSpan.Zero);
        var request = NewRequest(
            Guid.CreateVersion7(),
            content.Exam.Id,
            null,
            "Random",
            [.. questions.Select(q => q.Id)],
            [.. questions.Select((q, i) => Correct(q, late.AddSeconds(i)))]);
        var response = await client.PostAsJsonAsync(SyncUri, request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SyncQuizResponse>(Ct);
        body.ShouldNotBeNull();
        body.AcceptedCount.ShouldBe(10);
        body.XpEarned.ShouldBe(100 + 55);
        (await client.GetFromJsonAsync<LevelResponse>(LevelUri, Ct)).ShouldNotBeNull().TotalXp.ShouldBe(xpBefore + body.XpEarned);
        (await client.GetFromJsonAsync<StreakResponse>(StreakUri, Ct)).ShouldNotBeNull().CurrentStreak.ShouldBe(0);
    }

    [Fact]
    public async Task SyncQuiz_WrongAnswers_CountAsMistakes()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var questions = content.ActiveQuestionsOf(content.Topic1).ToList();
        var now = DateTimeOffset.UtcNow;
        var request = NewRequest(
            Guid.CreateVersion7(),
            content.Exam.Id,
            content.Topic1.Id,
            "ByTopic",
            [.. questions.Select(q => q.Id)],
            [Wrong(questions[0], now.AddSeconds(-5)), Correct(questions[1], now.AddSeconds(-4))]);
        (await client.PostAsJsonAsync(SyncUri, request, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var mistakes = await client.GetFromJsonAsync<MistakeSummaryResponse>(
            new Uri($"/api/v1/exams/{content.Exam.Id}/mistakes", UriKind.Relative),
            Ct);

        mistakes.ShouldNotBeNull();
        mistakes.Total.ShouldBe(1);
        mistakes.Topics.Single().TopicId.ShouldBe(content.Topic1.Id);
    }

    // ---- Helpers ----

    private static SyncQuizRequest NewRequest(
        Guid id,
        Guid examId,
        Guid? topicId,
        string mode,
        IReadOnlyList<Guid> questionIds,
        IReadOnlyList<SyncAnswerRequest> answers,
        DateTimeOffset? startedAt = null) =>
        new(id, examId, topicId, mode, startedAt ?? DateTimeOffset.UtcNow.AddMinutes(-10), questionIds, answers);

    private static SyncAnswerRequest Correct(Question question, DateTimeOffset at) =>
        new(question.Id, question.CorrectOption.Id, at);

    private static SyncAnswerRequest Wrong(Question question, DateTimeOffset at) =>
        new(question.Id, question.Options.First(o => !o.IsCorrect).Id, at);

    private WebApplicationFactory<Program> WithTime(FakeTimeProvider time) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(time);
        }));

    private static async Task<QuizResponse> CreateOnlineQuizAsync(HttpClient client, Guid examId, Guid? topicId, int count)
    {
        var response = await client.PostAsJsonAsync(QuizzesUri, new CreateQuizRequest(examId, topicId, count), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var quiz = await response.Content.ReadFromJsonAsync<QuizResponse>(Ct);
        return quiz ?? throw new InvalidOperationException("The quiz response was empty.");
    }

    private static async Task<DateTimeOffset?> GetAnsweredAtAsync(WebApplicationFactory<Program> app, Guid quizId, Guid questionId)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpoStudyDbContext>();
        var attempt = await db.QuizAttempts.AsNoTracking().FirstAsync(q => q.Id == quizId, Ct);
        return attempt.Answers.Single(a => a.QuestionId == questionId).AnsweredAt;
    }
}