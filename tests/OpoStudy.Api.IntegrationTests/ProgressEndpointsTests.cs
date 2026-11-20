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
using OpoStudy.Contracts.Users;
using OpoStudy.Infrastructure.Persistence;

using Shouldly;

namespace OpoStudy.Api.IntegrationTests;

public sealed class ProgressEndpointsTests(ApiFactory factory)
{
    private static readonly Uri StreakUri = new("/api/v1/me/streak", UriKind.Relative);
    private static readonly Uri GoalUri = new("/api/v1/me/daily-goal", UriKind.Relative);
    private static readonly Uri ZoneUri = new("/api/v1/me/time-zone", UriKind.Relative);
    private static readonly Uri MeUri = new("/api/v1/me", UriKind.Relative);
    private static readonly Uri QuizzesUri = new("/api/v1/quizzes", UriKind.Relative);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string NewUid() => $"uid-{Guid.NewGuid():N}";

    [Fact]
    public async Task GetStreak_WithoutToken_Returns401()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(StreakUri, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMe_NewUser_HasDefaultGoalAndTimeZone()
    {
        using var client = factory.CreateClientFor(NewUid());

        var me = await client.GetFromJsonAsync<UserProfileResponse>(MeUri, Ct);

        me.ShouldNotBeNull();
        me.DailyGoal.ShouldBe(10);
        me.TimeZone.ShouldBe("Europe/Madrid");
    }

    [Fact]
    public async Task GetStreak_NewUser_ReturnsSevenEmptyDaysEndingToday()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero));
        await using var app = WithTime(time);
        using var client = ApiFactory.CreateClientFor(app, NewUid());

        var streak = await client.GetFromJsonAsync<StreakResponse>(StreakUri, Ct);

        streak.ShouldNotBeNull();
        streak.DailyGoal.ShouldBe(10);
        streak.Today.ShouldBe(new DateOnly(2026, 1, 15));
        streak.AnsweredToday.ShouldBe(0);
        streak.GoalMetToday.ShouldBeFalse();
        streak.CurrentStreak.ShouldBe(0);
        streak.LongestStreak.ShouldBe(0);
        streak.LastDays.Count.ShouldBe(7);
        streak.LastDays.Select(d => d.Date).ShouldBe(Enumerable.Range(0, 7).Select(i => new DateOnly(2026, 1, 9).AddDays(i)));
        streak.LastDays.ShouldAllBe(d => d.AnsweredCount == 0 && !d.GoalMet && d.Goal == 10);
    }

    [Fact]
    public async Task SubmitAnswer_FullFlowOverSeveralDays_TracksGoalAndStreak()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero));
        await using var app = WithTime(time);
        var content = await ContentSeeder.SeedAsync(app.Services, activeQuestionsInTopic1: 22);
        using var client = ApiFactory.CreateClientFor(app, NewUid());
        (await client.PutAsJsonAsync(GoalUri, new UpdateDailyGoalRequest(5), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var quiz = await CreateRandomQuizAsync(client, content.Exam.Id, 25);
        var next = 0;

        // Day 1: the 5th answer meets the goal, the 6th does not meet it again.
        var day1 = await AnswerManyAsync(client, quiz, next, 6);
        next += 6;
        day1.Select(p => p.GoalMetNow).ShouldBe([false, false, false, false, true, false]);
        day1[4].CurrentStreak.ShouldBe(1);
        day1[5].CurrentStreak.ShouldBe(1);
        day1[5].AnsweredToday.ShouldBe(6);

        // Day 2: consecutive.
        time.Advance(TimeSpan.FromDays(1));
        (await AnswerManyAsync(client, quiz, next, 5))[4].CurrentStreak.ShouldBe(2);
        next += 5;
        var streak = await client.GetFromJsonAsync<StreakResponse>(StreakUri, Ct);
        streak.ShouldNotBeNull();
        streak.CurrentStreak.ShouldBe(2);
        streak.LongestStreak.ShouldBe(2);
        streak.GoalMetToday.ShouldBeTrue();
        streak.AnsweredToday.ShouldBe(5);
        streak.LastDays[^1].GoalMet.ShouldBeTrue();
        streak.LastDays[^2].AnsweredCount.ShouldBe(6);
        streak.LastDays[^2].GoalMet.ShouldBeTrue();

        // Skip a day: broken on read, restarts at 1 and keeps the longest.
        time.Advance(TimeSpan.FromDays(2));
        streak = await client.GetFromJsonAsync<StreakResponse>(StreakUri, Ct);
        streak.ShouldNotBeNull();
        streak.CurrentStreak.ShouldBe(0);
        streak.LongestStreak.ShouldBe(2);
        (await AnswerManyAsync(client, quiz, next, 5))[4].CurrentStreak.ShouldBe(1);
    }

    [Fact]
    public async Task GetStreak_GoalMetYesterdayNotToday_KeepsStreakButNotMetToday()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero));
        await using var app = WithTime(time);
        var content = await ContentSeeder.SeedAsync(app.Services, activeQuestionsInTopic1: 12);
        using var client = ApiFactory.CreateClientFor(app, NewUid());
        await client.PutAsJsonAsync(GoalUri, new UpdateDailyGoalRequest(5), Ct);
        var quiz = await CreateRandomQuizAsync(client, content.Exam.Id, 15);
        await AnswerManyAsync(client, quiz, 0, 5);

        time.Advance(TimeSpan.FromDays(1));
        var streak = await client.GetFromJsonAsync<StreakResponse>(StreakUri, Ct);

        streak.ShouldNotBeNull();
        streak.CurrentStreak.ShouldBe(1);
        streak.GoalMetToday.ShouldBeFalse();
    }

    [Fact]
    public async Task SubmitAnswer_NearMidnightInUserZone_CountsForTheLocalDay()
    {
        // 22:30 UTC in winter is 23:30 in Madrid; 23:30 UTC is already the next day there.
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 1, 15, 22, 30, 0, TimeSpan.Zero));
        await using var app = WithTime(time);
        var content = await ContentSeeder.SeedAsync(app.Services, activeQuestionsInTopic1: 12);
        using var client = ApiFactory.CreateClientFor(app, NewUid());
        var quiz = await CreateRandomQuizAsync(client, content.Exam.Id, 15);

        await AnswerManyAsync(client, quiz, 0, 1);
        time.Advance(TimeSpan.FromHours(1));
        await AnswerManyAsync(client, quiz, 1, 1);

        var streak = await client.GetFromJsonAsync<StreakResponse>(StreakUri, Ct);
        streak.ShouldNotBeNull();
        streak.Today.ShouldBe(new DateOnly(2026, 1, 16));
        streak.AnsweredToday.ShouldBe(1);
        streak.LastDays[^2].Date.ShouldBe(new DateOnly(2026, 1, 15));
        streak.LastDays[^2].AnsweredCount.ShouldBe(1);
    }

    [Fact]
    public async Task UpdateDailyGoal_LoweringToAlreadyReachedCount_MeetsTodayAndAddsStreak()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero));
        await using var app = WithTime(time);
        var content = await ContentSeeder.SeedAsync(app.Services, activeQuestionsInTopic1: 12);
        using var client = ApiFactory.CreateClientFor(app, NewUid());
        var quiz = await CreateRandomQuizAsync(client, content.Exam.Id, 15);
        await AnswerManyAsync(client, quiz, 0, 6);

        var response = await client.PutAsJsonAsync(GoalUri, new UpdateDailyGoalRequest(5), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var streak = await response.Content.ReadFromJsonAsync<StreakResponse>(Ct);
        streak.ShouldNotBeNull();
        streak.DailyGoal.ShouldBe(5);
        streak.GoalMetToday.ShouldBeTrue();
        streak.CurrentStreak.ShouldBe(1);

        // A met day stays met when the goal goes up, and the goal of that day is untouched.
        var raised = await (await client.PutAsJsonAsync(GoalUri, new UpdateDailyGoalRequest(50), Ct))
            .Content.ReadFromJsonAsync<StreakResponse>(Ct);
        raised.ShouldNotBeNull();
        raised.GoalMetToday.ShouldBeTrue();
        raised.LastDays[^1].Goal.ShouldBe(5);
        raised.DailyGoal.ShouldBe(50);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(101)]
    public async Task UpdateDailyGoal_OutOfRange_Returns400(int goal)
    {
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.PutAsJsonAsync(GoalUri, new UpdateDailyGoalRequest(goal), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task UpdateTimeZone_Valid_Returns204AndPersists()
    {
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.PutAsJsonAsync(ZoneUri, new UpdateTimeZoneRequest("Atlantic/Canary"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var me = await client.GetFromJsonAsync<UserProfileResponse>(MeUri, Ct);
        me.ShouldNotBeNull();
        me.TimeZone.ShouldBe("Atlantic/Canary");
    }

    [Theory]
    [InlineData("Mars/Olympus")]
    [InlineData("")]
    public async Task UpdateTimeZone_Invalid_Returns400(string zone)
    {
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.PutAsJsonAsync(ZoneUri, new UpdateTimeZoneRequest(zone), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetStreak_OtherUsersActivity_IsNotVisible()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services, activeQuestionsInTopic1: 12);
        using var alice = factory.CreateClientFor(NewUid());
        using var bob = factory.CreateClientFor(NewUid());
        var quiz = await CreateRandomQuizAsync(alice, content.Exam.Id, 15);
        await AnswerManyAsync(alice, quiz, 0, 3);

        var streak = await bob.GetFromJsonAsync<StreakResponse>(StreakUri, Ct);

        streak.ShouldNotBeNull();
        streak.AnsweredToday.ShouldBe(0);
    }

    [Fact]
    public async Task DeleteMe_RemovesDailyActivities()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services, activeQuestionsInTopic1: 12);
        using var client = factory.CreateClientFor(NewUid());
        var quiz = await CreateRandomQuizAsync(client, content.Exam.Id, 15);
        await AnswerManyAsync(client, quiz, 0, 2);
        var me = await client.GetFromJsonAsync<UserProfileResponse>(MeUri, Ct);
        me.ShouldNotBeNull();

        (await client.DeleteAsync(MeUri, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpoStudyDbContext>();
        (await db.DailyActivities.CountAsync(a => a.UserId == me.Id, Ct)).ShouldBe(0);
    }

    // ---- Helpers ----

    private WebApplicationFactory<Program> WithTime(FakeTimeProvider time) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(time);
        }));

    private static async Task<QuizResponse> CreateRandomQuizAsync(HttpClient client, Guid examId, int count)
    {
        var response = await client.PostAsJsonAsync(QuizzesUri, new CreateQuizRequest(examId, null, count), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var quiz = await response.Content.ReadFromJsonAsync<QuizResponse>(Ct);
        return quiz ?? throw new InvalidOperationException("The quiz response was empty.");
    }

    private static async Task<List<DailyProgressResponse>> AnswerManyAsync(HttpClient client, QuizResponse quiz, int start, int count)
    {
        var results = new List<DailyProgressResponse>();
        foreach (var question in quiz.Questions.Skip(start).Take(count))
        {
            var response = await client.PostAsJsonAsync(
                new Uri($"/api/v1/quizzes/{quiz.Id}/answers", UriKind.Relative),
                new SubmitAnswerRequest(question.QuestionId, question.Options[0].Id),
                Ct);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var body = await response.Content.ReadFromJsonAsync<AnswerFeedbackResponse>(Ct);
            results.Add(body?.DailyProgress ?? throw new InvalidOperationException("No daily progress in the response."));
        }

        results.Count.ShouldBe(count);
        return results;
    }
}