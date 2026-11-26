using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

using OpoStudy.Contracts.MockExams;
using OpoStudy.Contracts.Progress;
using OpoStudy.Contracts.Quizzes;
using OpoStudy.Contracts.Reviews;
using OpoStudy.Contracts.Users;
using OpoStudy.Domain.Questions;
using OpoStudy.Domain.Quizzes;
using OpoStudy.Infrastructure.Persistence;

using Shouldly;

namespace OpoStudy.Api.IntegrationTests;

public sealed class ReviewsEndpointsTests(ApiFactory factory)
{
    private static readonly Uri DueUri = new("/api/v1/reviews/due", UriKind.Relative);
    private static readonly Uri QuizzesUri = new("/api/v1/quizzes", UriKind.Relative);
    private static readonly Uri SyncUri = new("/api/v1/quizzes/sync", UriKind.Relative);
    private static readonly Uri MeUri = new("/api/v1/me", UriKind.Relative);
    private static readonly Uri ZoneUri = new("/api/v1/me/time-zone", UriKind.Relative);
    private static readonly Uri MockExamsUri = new("/api/v1/mock-exams", UriKind.Relative);

    // 11:00 in Madrid (UTC+1 in January).
    private static readonly DateTimeOffset Start = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Day = new(2026, 1, 15);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string NewUid() => $"uid-{Guid.NewGuid():N}";

    // ---- Schedule recording ----

    [Fact]
    public async Task SubmitAnswer_CorrectFirstTime_SchedulesBox1InTwoDays()
    {
        var (app, time, content, client, userId) = await StartAsync();
        await using var host = app;
        var question = content.ActiveQuestionsOf(content.Topic2).First();

        await AnswerAsync(client, await CreateTopicQuizAsync(client, content), content, question.Id, correct: true);

        var schedule = await FindAsync(app, userId, question.Id);
        schedule.ShouldNotBeNull();
        schedule.Box.ShouldBe(1);
        schedule.DueOn.ShouldBe(Day.AddDays(2));
        schedule.LastAnsweredAt.ShouldBe(time.GetUtcNow());
    }

    [Fact]
    public async Task SubmitAnswer_WrongFirstTime_SchedulesBox0ForTomorrow()
    {
        var (app, _, content, client, userId) = await StartAsync();
        await using var host = app;
        var question = content.ActiveQuestionsOf(content.Topic2).First();

        await AnswerAsync(client, await CreateTopicQuizAsync(client, content), content, question.Id, correct: false);

        var schedule = await FindAsync(app, userId, question.Id);
        schedule.ShouldNotBeNull();
        schedule.Box.ShouldBe(0);
        schedule.DueOn.ShouldBe(Day.AddDays(1));
    }

    [Fact]
    public async Task SubmitAnswer_CorrectWhenDueRepeatedly_ClimbsBoxesAndCapsAtSix()
    {
        var (app, time, content, client, userId) = await StartAsync();
        await using var host = app;
        var question = content.ActiveQuestionsOf(content.Topic2).First();
        var expectedIntervals = new[] { 2, 4, 8, 16, 32, 64, 64 };

        var today = Day;
        for (var i = 0; i < expectedIntervals.Length; i++)
        {
            await AnswerAsync(client, await CreateTopicQuizAsync(client, content), content, question.Id, correct: true);

            var schedule = await FindAsync(app, userId, question.Id);
            schedule.ShouldNotBeNull();
            schedule.Box.ShouldBe(Math.Min(i + 1, 6));
            schedule.DueOn.ShouldBe(today.AddDays(expectedIntervals[i]));

            time.Advance(TimeSpan.FromDays(expectedIntervals[i]));
            today = today.AddDays(expectedIntervals[i]);
        }
    }

    [Fact]
    public async Task SubmitAnswer_WrongAfterClimbing_ResetsToBox0()
    {
        var (app, time, content, client, userId) = await StartAsync();
        await using var host = app;
        var question = content.ActiveQuestionsOf(content.Topic2).First();
        await AnswerAsync(client, await CreateTopicQuizAsync(client, content), content, question.Id, correct: true);
        time.Advance(TimeSpan.FromDays(2));
        await AnswerAsync(client, await CreateTopicQuizAsync(client, content), content, question.Id, correct: true);
        (await FindAsync(app, userId, question.Id))!.Box.ShouldBe(2);

        await AnswerAsync(client, await CreateTopicQuizAsync(client, content), content, question.Id, correct: false);

        var schedule = await FindAsync(app, userId, question.Id);
        schedule.ShouldNotBeNull();
        schedule.Box.ShouldBe(0);
        schedule.DueOn.ShouldBe(Day.AddDays(3));
    }

    [Fact]
    public async Task SubmitAnswer_CorrectBeforeDueDate_ChangesNothing()
    {
        var (app, time, content, client, userId) = await StartAsync();
        await using var host = app;
        var question = content.ActiveQuestionsOf(content.Topic2).First();
        await AnswerAsync(client, await CreateTopicQuizAsync(client, content), content, question.Id, correct: true);
        var before = await FindAsync(app, userId, question.Id);
        time.Advance(TimeSpan.FromHours(3));

        await AnswerAsync(client, await CreateTopicQuizAsync(client, content), content, question.Id, correct: true);

        var after = await FindAsync(app, userId, question.Id);
        after.ShouldNotBeNull();
        after.Box.ShouldBe(1);
        after.DueOn.ShouldBe(before!.DueOn);
        after.LastAnsweredAt.ShouldBe(before.LastAnsweredAt);
    }

    [Fact]
    public async Task GetDueReviews_AfterAdvancingToDueDate_CountsTheQuestion()
    {
        var (app, time, content, client, _) = await StartAsync();
        await using var host = app;
        var question = content.ActiveQuestionsOf(content.Topic2).First();
        await AnswerAsync(client, await CreateTopicQuizAsync(client, content), content, question.Id, correct: false);
        (await GetDueAsync(client)).Total.ShouldBe(0);

        time.Advance(TimeSpan.FromDays(1));
        var due = await GetDueAsync(client);

        due.Total.ShouldBe(1);
        due.Exams.ShouldBe([new ExamDueReviewsResponse(content.Exam.Id, content.Exam.Name, 1)]);
    }

    // ---- Sync ----

    [Fact]
    public async Task SyncQuiz_Answers_ScheduleEachQuestion()
    {
        var (app, time, content, client, userId) = await StartAsync();
        await using var host = app;
        var questions = content.ActiveQuestionsOf(content.Topic2).ToList();
        var request = NewSync(content, content.Topic2.Id, questions, [Correct(questions[0], time.GetUtcNow().AddMinutes(-5)), Wrong(questions[1], time.GetUtcNow().AddMinutes(-4))]);

        (await client.PostAsJsonAsync(SyncUri, request, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await FindAsync(app, userId, questions[0].Id))!.Box.ShouldBe(1);
        (await FindAsync(app, userId, questions[1].Id))!.Box.ShouldBe(0);
        (await FindAsync(app, userId, questions[2].Id)).ShouldBeNull();
    }

    [Fact]
    public async Task SyncQuiz_AnswerOlderThanLastAnswer_DoesNotChangeTheSchedule()
    {
        var (app, time, content, client, userId) = await StartAsync();
        await using var host = app;
        var questions = content.ActiveQuestionsOf(content.Topic2).ToList();
        await AnswerAsync(client, await CreateTopicQuizAsync(client, content), content, questions[0].Id, correct: false);
        var before = await FindAsync(app, userId, questions[0].Id);

        // A late sync with an answer given two hours before the online one.
        var request = NewSync(content, content.Topic2.Id, questions, [Correct(questions[0], time.GetUtcNow().AddHours(-2))]);
        (await client.PostAsJsonAsync(SyncUri, request, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var after = await FindAsync(app, userId, questions[0].Id);
        after.ShouldNotBeNull();
        after.Box.ShouldBe(0);
        after.DueOn.ShouldBe(before!.DueOn);
        after.LastAnsweredAt.ShouldBe(before.LastAnsweredAt);
    }

    // ---- Mock exam ----

    [Fact]
    public async Task SubmitMockExam_SchedulesAnsweredQuestionsButNotBlankOnes()
    {
        var (app, _, content, client, userId) = await StartAsync();
        await using var host = app;
        var created = await client.PostAsJsonAsync(MockExamsUri, new CreateMockExamRequest(content.Exam.Id, 1, null), Ct);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var mockExam = (await created.Content.ReadFromJsonAsync<MockExamResponse>(Ct))!;
        var q = mockExam.Questions;
        var request = new SubmitMockExamRequest(
        [
            new MockExamAnswer(q[0].QuestionId, content.Questions[q[0].QuestionId].CorrectOption.Id),
            new MockExamAnswer(q[1].QuestionId, content.Questions[q[1].QuestionId].Options.First(o => !o.IsCorrect).Id),
        ]);

        var response = await client.PostAsJsonAsync(new Uri($"/api/v1/mock-exams/{mockExam.Id}/submission", UriKind.Relative), request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await FindAsync(app, userId, q[0].QuestionId))!.Box.ShouldBe(1);
        (await FindAsync(app, userId, q[1].QuestionId))!.Box.ShouldBe(0);
        (await FindAsync(app, userId, q[2].QuestionId)).ShouldBeNull();
    }

    // ---- GET /reviews/due ----

    [Fact]
    public async Task GetDueReviews_WithoutToken_Returns401()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(DueUri, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetDueReviews_NewUser_ReturnsZero()
    {
        using var client = factory.CreateClientFor(NewUid());

        var due = await GetDueAsync(client);

        due.Total.ShouldBe(0);
        due.Exams.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetDueReviews_DeactivatedQuestion_IsNotCounted()
    {
        var (app, time, content, client, _) = await StartAsync();
        await using var host = app;
        var questions = content.ActiveQuestionsOf(content.Topic2).ToList();
        var quiz = await CreateTopicQuizAsync(client, content);
        await AnswerAsync(client, quiz, content, questions[0].Id, correct: false);
        await AnswerAsync(client, quiz, content, questions[1].Id, correct: false);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OpoStudyDbContext>();
            var entity = await db.Questions.FirstAsync(x => x.Id == questions[0].Id, Ct);
            entity.Deactivate();
            await db.SaveChangesAsync(Ct);
        }

        time.Advance(TimeSpan.FromDays(1));
        var due = await GetDueAsync(client);

        due.Total.ShouldBe(1);
    }

    [Fact]
    public async Task GetDueReviews_TwoExams_GroupsAndSortsByExamName()
    {
        var (app, time, content, client, _) = await StartAsync();
        await using var host = app;
        var main = content.ActiveQuestionsOf(content.Topic2).ToList();
        var quiz = await CreateTopicQuizAsync(client, content);
        await AnswerAsync(client, quiz, content, main[0].Id, correct: false);
        await AnswerAsync(client, quiz, content, main[1].Id, correct: false);
        var otherQuestion = content.ActiveQuestionsOf(content.OtherExamTopic).Single();
        var otherQuizResponse = await client.PostAsJsonAsync(QuizzesUri, new CreateQuizRequest(content.OtherExam.Id, null, 1), Ct);
        var otherQuiz = (await otherQuizResponse.Content.ReadFromJsonAsync<QuizResponse>(Ct))!;
        await AnswerAsync(client, otherQuiz, content, otherQuestion.Id, correct: true);

        time.Advance(TimeSpan.FromDays(2));
        var due = await GetDueAsync(client);

        due.Total.ShouldBe(3);
        due.Exams.Select(e => e.ExamName).ShouldBe([content.Exam.Name, content.OtherExam.Name], ignoreOrder: false);
        due.Exams.Single(e => e.ExamId == content.Exam.Id).Count.ShouldBe(2);
        due.Exams.Single(e => e.ExamId == content.OtherExam.Id).Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetDueReviews_OtherUsersAnswers_AreNotCounted()
    {
        var (app, time, content, client, _) = await StartAsync();
        await using var host = app;
        var question = content.ActiveQuestionsOf(content.Topic2).First();
        await AnswerAsync(client, await CreateTopicQuizAsync(client, content), content, question.Id, correct: false);
        using var other = ApiFactory.CreateClientFor(app, NewUid());

        time.Advance(TimeSpan.FromDays(1));

        (await GetDueAsync(client)).Total.ShouldBe(1);
        (await GetDueAsync(other)).Total.ShouldBe(0);
    }

    [Fact]
    public async Task GetDueReviews_UserTimeZoneAhead_IsDueOnTheLocalDay()
    {
        var (app, time, content, client, _) = await StartAsync();
        await using var host = app;
        (await client.PutAsJsonAsync(ZoneUri, new UpdateTimeZoneRequest("Pacific/Auckland"), Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var question = content.ActiveQuestionsOf(content.Topic2).First();

        // 10:00 UTC is 23:00 on 15 January in Auckland (UTC+13): the answer is due on the 16th, local.
        await AnswerAsync(client, await CreateTopicQuizAsync(client, content), content, question.Id, correct: false);
        (await GetDueAsync(client)).Total.ShouldBe(0);

        // 12:00 UTC is already the 16th in Auckland, but still the 15th in UTC.
        time.Advance(TimeSpan.FromHours(2));
        (await GetDueAsync(client)).Total.ShouldBe(1);
    }

    // ---- POST /quizzes with DueOnly ----

    [Fact]
    public async Task CreateQuiz_DueOnly_CreatesSpacedReviewWithOnlyDueQuestions()
    {
        var (app, time, content, client, _) = await StartAsync();
        await using var host = app;
        var questions = content.ActiveQuestionsOf(content.Topic2).ToList();
        var quiz = await CreateTopicQuizAsync(client, content);
        await AnswerAsync(client, quiz, content, questions[0].Id, correct: false);
        await AnswerAsync(client, quiz, content, questions[1].Id, correct: true);
        time.Advance(TimeSpan.FromDays(1));

        var response = await client.PostAsJsonAsync(QuizzesUri, new CreateQuizRequest(content.Exam.Id, null, 20, DueOnly: true), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = (await response.Content.ReadFromJsonAsync<QuizResponse>(Ct))!;
        created.Mode.ShouldBe("SpacedReview");
        created.TopicId.ShouldBeNull();
        created.Questions.Select(x => x.QuestionId).ShouldBe([questions[0].Id]);
        created.Questions[0].Feedback.ShouldBeNull();
        var list = (await client.GetFromJsonAsync<List<QuizSummaryResponse>>(new Uri("/api/v1/quizzes?skip=0&take=1", UriKind.Relative), Ct))!;
        list[0].Mode.ShouldBe("SpacedReview");
    }

    [Fact]
    public async Task CreateQuiz_DueOnlyWithTopic_UsesOnlyThatTopic()
    {
        var (app, time, content, client, _) = await StartAsync();
        await using var host = app;
        var topic1 = content.ActiveQuestionsOf(content.Topic1).First();
        var topic2 = content.ActiveQuestionsOf(content.Topic2).First();
        await AnswerAsync(client, await CreateTopicQuizAsync(client, content), content, topic2.Id, correct: false);
        var quiz1 = (await (await client.PostAsJsonAsync(QuizzesUri, new CreateQuizRequest(content.Exam.Id, content.Topic1.Id, 4), Ct)).Content.ReadFromJsonAsync<QuizResponse>(Ct))!;
        await AnswerAsync(client, quiz1, content, topic1.Id, correct: false);
        time.Advance(TimeSpan.FromDays(1));

        var response = await client.PostAsJsonAsync(QuizzesUri, new CreateQuizRequest(content.Exam.Id, content.Topic1.Id, 20, DueOnly: true), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = (await response.Content.ReadFromJsonAsync<QuizResponse>(Ct))!;
        created.TopicId.ShouldBe(content.Topic1.Id);
        created.Questions.Select(x => x.QuestionId).ShouldBe([topic1.Id]);
    }

    [Fact]
    public async Task CreateQuiz_DueOnly_OrdersByDueDateAndRespectsQuestionCount()
    {
        var (app, time, content, client, _) = await StartAsync();
        await using var host = app;
        var questions = content.ActiveQuestionsOf(content.Topic1).ToList();
        var quiz = (await (await client.PostAsJsonAsync(QuizzesUri, new CreateQuizRequest(content.Exam.Id, content.Topic1.Id, 4), Ct)).Content.ReadFromJsonAsync<QuizResponse>(Ct))!;

        // Answered on consecutive days, so the first one is the most overdue.
        await AnswerAsync(client, quiz, content, questions[2].Id, correct: false);
        time.Advance(TimeSpan.FromDays(1));
        await AnswerAsync(client, quiz, content, questions[0].Id, correct: false);
        time.Advance(TimeSpan.FromDays(1));
        await AnswerAsync(client, quiz, content, questions[3].Id, correct: false);
        time.Advance(TimeSpan.FromDays(5));

        var response = await client.PostAsJsonAsync(QuizzesUri, new CreateQuizRequest(content.Exam.Id, null, 2, DueOnly: true), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = (await response.Content.ReadFromJsonAsync<QuizResponse>(Ct))!;
        created.Questions.Select(x => x.QuestionId).ShouldBe([questions[2].Id, questions[0].Id]);
    }

    [Fact]
    public async Task CreateQuiz_DueOnlyWithNothingDue_Returns422()
    {
        var (app, _, content, client, _) = await StartAsync();
        await using var host = app;

        var response = await client.PostAsJsonAsync(QuizzesUri, new CreateQuizRequest(content.Exam.Id, null, 10, DueOnly: true), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "quiz.no_due_reviews");
    }

    [Fact]
    public async Task CreateQuiz_DueOnlyAndMistakesOnly_Returns400()
    {
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.PostAsJsonAsync(QuizzesUri, new CreateQuizRequest(Guid.NewGuid(), null, 10, MistakesOnly: true, DueOnly: true), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("dueOnly");
    }

    // ---- Account deletion ----

    [Fact]
    public async Task DeleteMe_WithSchedules_DeletesThem()
    {
        var (app, _, content, client, userId) = await StartAsync();
        await using var host = app;
        var question = content.ActiveQuestionsOf(content.Topic2).First();
        await AnswerAsync(client, await CreateTopicQuizAsync(client, content), content, question.Id, correct: true);
        (await FindAsync(app, userId, question.Id)).ShouldNotBeNull();

        var response = await client.DeleteAsync(MeUri, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await FindAsync(app, userId, question.Id)).ShouldBeNull();
    }

    // ---- Helpers ----

    private async Task<(WebApplicationFactory<Program> App, FakeTimeProvider Time, SeededContent Content, HttpClient Client, Guid UserId)> StartAsync()
    {
        var time = new FakeTimeProvider(Start);
        var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(time);
        }));
        var content = await ContentSeeder.SeedAsync(app.Services);
        var client = ApiFactory.CreateClientFor(app, NewUid());
        var me = await client.GetFromJsonAsync<UserProfileResponse>(MeUri, Ct);
        return (app, time, content, client, me!.Id);
    }

    private static async Task<QuizResponse> CreateTopicQuizAsync(HttpClient client, SeededContent content)
    {
        var response = await client.PostAsJsonAsync(QuizzesUri, new CreateQuizRequest(content.Exam.Id, content.Topic2.Id, 3), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<QuizResponse>(Ct))!;
    }

    private static async Task AnswerAsync(HttpClient client, QuizResponse quiz, SeededContent content, Guid questionId, bool correct)
    {
        var question = content.Questions[questionId];
        var optionId = correct ? question.CorrectOption.Id : question.Options.First(o => !o.IsCorrect).Id;
        var response = await client.PostAsJsonAsync(
            new Uri($"/api/v1/quizzes/{quiz.Id}/answers", UriKind.Relative),
            new SubmitAnswerRequest(questionId, optionId),
            Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<DueReviewSummaryResponse> GetDueAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<DueReviewSummaryResponse>(DueUri, Ct))!;

    private static async Task<ReviewSchedule?> FindAsync(WebApplicationFactory<Program> app, Guid userId, Guid questionId)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpoStudyDbContext>();
        return await db.ReviewSchedules.AsNoTracking().FirstOrDefaultAsync(r => r.UserId == userId && r.QuestionId == questionId, Ct);
    }

    private static SyncQuizRequest NewSync(SeededContent content, Guid topicId, IReadOnlyList<Question> questions, IReadOnlyList<SyncAnswerRequest> answers) =>
        new(Guid.CreateVersion7(), content.Exam.Id, topicId, "ByTopic", Start.AddHours(-1), [.. questions.Select(q => q.Id)], answers);

    private static SyncAnswerRequest Correct(Question question, DateTimeOffset at) =>
        new(question.Id, question.CorrectOption.Id, at);

    private static SyncAnswerRequest Wrong(Question question, DateTimeOffset at) =>
        new(question.Id, question.Options.First(o => !o.IsCorrect).Id, at);
}