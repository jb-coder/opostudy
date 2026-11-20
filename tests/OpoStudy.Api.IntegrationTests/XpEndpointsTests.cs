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

public sealed class XpEndpointsTests(ApiFactory factory)
{
    private static readonly Uri LevelUri = new("/api/v1/me/level", UriKind.Relative);
    private static readonly Uri GoalUri = new("/api/v1/me/daily-goal", UriKind.Relative);
    private static readonly Uri MeUri = new("/api/v1/me", UriKind.Relative);
    private static readonly Uri QuizzesUri = new("/api/v1/quizzes", UriKind.Relative);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string NewUid() => $"uid-{Guid.NewGuid():N}";

    [Fact]
    public async Task GetLevel_WithoutToken_Returns401()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(LevelUri, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetLevel_NewUser_StartsAtLevelOneWithoutXp()
    {
        using var client = factory.CreateClientFor(NewUid());

        var level = await client.GetFromJsonAsync<LevelResponse>(LevelUri, Ct);

        level.ShouldBe(new LevelResponse(1, 0, 0, 100, 0));
    }

    [Fact]
    public async Task SubmitAnswer_TestOfTwelve_GrantsXpPerAnswerAndGoalBonusOnTheTenth()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero));
        await using var app = WithTime(time);
        var content = await ContentSeeder.SeedAsync(app.Services, activeQuestionsInTopic1: 12);
        using var client = ApiFactory.CreateClientFor(app, NewUid());
        var quiz = await CreateQuizAsync(client, content.Exam.Id, 12);

        var answers = await AnswerManyAsync(client, quiz, 0, 12);

        for (var i = 0; i < 12; i++)
        {
            var isTenth = i == 9;
            var expectedBase = answers[i].Feedback.IsCorrect ? 10 : 2;
            answers[i].Xp.Bonus.ShouldBe(isTenth ? 55 : 0);
            answers[i].Xp.Gained.ShouldBe(expectedBase + (isTenth ? 55 : 0));
        }

        var expectedTotal = answers.Sum(a => a.Xp.Gained);
        answers[^1].Xp.TotalXp.ShouldBe(expectedTotal);
        var level = await client.GetFromJsonAsync<LevelResponse>(LevelUri, Ct);
        level.ShouldNotBeNull();
        level.TotalXp.ShouldBe(expectedTotal);
        level.XpToday.ShouldBe(expectedTotal);
        level.Level.ShouldBe(answers[^1].Xp.Level);
        level.XpIntoLevel.ShouldBe(expectedTotal - (50 * level.Level * (level.Level - 1)));
        level.XpForNextLevel.ShouldBe(100 * level.Level);
    }

    [Fact]
    public async Task SubmitAnswer_GoalMetOnSecondConsecutiveDay_BonusGrowsWithStreak()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero));
        await using var app = WithTime(time);
        var content = await ContentSeeder.SeedAsync(app.Services, activeQuestionsInTopic1: 12);
        using var client = ApiFactory.CreateClientFor(app, NewUid());
        (await client.PutAsJsonAsync(GoalUri, new UpdateDailyGoalRequest(5), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var quiz = await CreateQuizAsync(client, content.Exam.Id, 12);

        var day1 = await AnswerManyAsync(client, quiz, 0, 5);
        time.Advance(TimeSpan.FromDays(1));
        var day2 = await AnswerManyAsync(client, quiz, 5, 5);

        day1[4].Xp.Bonus.ShouldBe(55);
        day2[4].Xp.Bonus.ShouldBe(60);
        day2.Take(4).ShouldAllBe(a => a.Xp.Bonus == 0);
    }

    [Fact]
    public async Task UpdateDailyGoal_LoweringToMeetTheDay_GrantsBonusOnlyOnce()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero));
        await using var app = WithTime(time);
        var content = await ContentSeeder.SeedAsync(app.Services, activeQuestionsInTopic1: 12);
        using var client = ApiFactory.CreateClientFor(app, NewUid());
        var quiz = await CreateQuizAsync(client, content.Exam.Id, 12);
        var answers = await AnswerManyAsync(client, quiz, 0, 6);
        var answeredXp = answers.Sum(a => a.Xp.Gained);

        (await client.PutAsJsonAsync(GoalUri, new UpdateDailyGoalRequest(5), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var afterLowering = await client.GetFromJsonAsync<LevelResponse>(LevelUri, Ct);
        (await client.PutAsJsonAsync(GoalUri, new UpdateDailyGoalRequest(20), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.PutAsJsonAsync(GoalUri, new UpdateDailyGoalRequest(5), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var afterRepeating = await client.GetFromJsonAsync<LevelResponse>(LevelUri, Ct);

        afterLowering.ShouldNotBeNull();
        afterLowering.TotalXp.ShouldBe(answeredXp + 55);
        afterLowering.XpToday.ShouldBe(answeredXp + 55);
        afterRepeating.ShouldBe(afterLowering);
    }

    [Fact]
    public async Task SubmitAnswer_CrossingALevelThreshold_FlagsLeveledUpOnlyOnce()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services, activeQuestionsInTopic1: 12);
        using var client = factory.CreateClientFor(NewUid());
        var me = await client.GetFromJsonAsync<UserProfileResponse>(MeUri, Ct);
        me.ShouldNotBeNull();
        await SeedXpAsync(me.Id, 99);
        var quiz = await CreateQuizAsync(client, content.Exam.Id, 12);

        var answers = await AnswerManyAsync(client, quiz, 0, 2);

        answers[0].Xp.LeveledUp.ShouldBeTrue();
        answers[0].Xp.Level.ShouldBe(2);
        answers[1].Xp.LeveledUp.ShouldBeFalse();
        answers[1].Xp.Level.ShouldBe(2);
    }

    [Fact]
    public async Task GetLevel_AfterMidnight_ResetsXpTodayButKeepsTotal()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero));
        await using var app = WithTime(time);
        var content = await ContentSeeder.SeedAsync(app.Services, activeQuestionsInTopic1: 12);
        using var client = ApiFactory.CreateClientFor(app, NewUid());
        var quiz = await CreateQuizAsync(client, content.Exam.Id, 12);
        var answers = await AnswerManyAsync(client, quiz, 0, 3);
        var total = answers.Sum(a => a.Xp.Gained);

        time.Advance(TimeSpan.FromDays(1));
        var level = await client.GetFromJsonAsync<LevelResponse>(LevelUri, Ct);

        level.ShouldNotBeNull();
        level.XpToday.ShouldBe(0);
        level.TotalXp.ShouldBe(total);
    }

    [Fact]
    public async Task GetLevel_OtherUsersXp_IsNotVisible()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services, activeQuestionsInTopic1: 12);
        using var alice = factory.CreateClientFor(NewUid());
        using var bob = factory.CreateClientFor(NewUid());
        var quiz = await CreateQuizAsync(alice, content.Exam.Id, 12);
        await AnswerManyAsync(alice, quiz, 0, 3);

        var level = await bob.GetFromJsonAsync<LevelResponse>(LevelUri, Ct);

        level.ShouldBe(new LevelResponse(1, 0, 0, 100, 0));
    }

    // ---- Helpers ----

    private async Task SeedXpAsync(Guid userId, int xp)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpoStudyDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == userId, Ct);
        user.GainXp(xp);
        await db.SaveChangesAsync(Ct);
    }

    private WebApplicationFactory<Program> WithTime(FakeTimeProvider time) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(time);
        }));

    private static async Task<QuizResponse> CreateQuizAsync(HttpClient client, Guid examId, int count)
    {
        var response = await client.PostAsJsonAsync(QuizzesUri, new CreateQuizRequest(examId, null, count), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var quiz = await response.Content.ReadFromJsonAsync<QuizResponse>(Ct);
        return quiz ?? throw new InvalidOperationException("The quiz response was empty.");
    }

    private static async Task<List<AnswerFeedbackResponse>> AnswerManyAsync(HttpClient client, QuizResponse quiz, int start, int count)
    {
        var results = new List<AnswerFeedbackResponse>();
        foreach (var question in quiz.Questions.Skip(start).Take(count))
        {
            var response = await client.PostAsJsonAsync(
                new Uri($"/api/v1/quizzes/{quiz.Id}/answers", UriKind.Relative),
                new SubmitAnswerRequest(question.QuestionId, question.Options[0].Id),
                Ct);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var body = await response.Content.ReadFromJsonAsync<AnswerFeedbackResponse>(Ct);
            results.Add(body ?? throw new InvalidOperationException("The answer response was empty."));
        }

        results.Count.ShouldBe(count);
        return results;
    }
}