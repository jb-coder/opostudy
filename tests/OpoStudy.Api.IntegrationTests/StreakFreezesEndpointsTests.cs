using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

using OpoStudy.Api.Errors;
using OpoStudy.Contracts.Progress;
using OpoStudy.Contracts.Quizzes;
using OpoStudy.Contracts.Users;
using OpoStudy.Domain.Users;
using OpoStudy.Infrastructure.Persistence;

using Shouldly;

namespace OpoStudy.Api.IntegrationTests;

public sealed class StreakFreezesEndpointsTests(ApiFactory factory)
{
    private static readonly Uri FreezesUri = new("/api/v1/me/streak-freezes", UriKind.Relative);
    private static readonly Uri StreakUri = new("/api/v1/me/streak", UriKind.Relative);
    private static readonly Uri LevelUri = new("/api/v1/me/level", UriKind.Relative);
    private static readonly Uri GoalUri = new("/api/v1/me/daily-goal", UriKind.Relative);
    private static readonly Uri MeUri = new("/api/v1/me", UriKind.Relative);
    private static readonly Uri QuizzesUri = new("/api/v1/quizzes", UriKind.Relative);

    private static readonly DateOnly Monday = new(2026, 1, 12);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string NewUid() => $"uid-{Guid.NewGuid():N}";

    private static FakeTimeProvider MondayClock() => new(new DateTimeOffset(2026, 1, 12, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task BuyStreakFreeze_WithoutToken_Returns401()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsync(FreezesUri, null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetStreak_NewUser_HasNoFreezesAndExposesRules()
    {
        using var client = factory.CreateClientFor(NewUid());

        var streak = await client.GetFromJsonAsync<StreakResponse>(StreakUri, Ct);
        var level = await client.GetFromJsonAsync<LevelResponse>(LevelUri, Ct);

        streak.ShouldNotBeNull();
        streak.StreakFreezes.ShouldBe(0);
        streak.MaxStreakFreezes.ShouldBe(2);
        streak.StreakFreezePrice.ShouldBe(200);
        streak.FreezesUsedNow.ShouldBe(0);
        level.ShouldNotBeNull();
        level.SpendableXp.ShouldBe(level.TotalXp);
    }

    [Fact]
    public async Task BuyStreakFreeze_EnoughXp_AddsFreezeAndSpendsXpWithoutChangingLevel()
    {
        using var client = factory.CreateClientFor(NewUid());
        var me = await GetMeAsync(client);
        await SeedAsync(me.Id, u => u.GainXp(450));
        var before = await client.GetFromJsonAsync<LevelResponse>(LevelUri, Ct);

        var response = await client.PostAsync(FreezesUri, null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var streak = await response.Content.ReadFromJsonAsync<StreakResponse>(Ct);
        streak.ShouldNotBeNull();
        streak.StreakFreezes.ShouldBe(1);
        var after = await client.GetFromJsonAsync<LevelResponse>(LevelUri, Ct);
        before.ShouldNotBeNull();
        after.ShouldNotBeNull();
        after.SpendableXp.ShouldBe(250);
        after.TotalXp.ShouldBe(450);
        after.Level.ShouldBe(before.Level);
    }

    [Fact]
    public async Task BuyStreakFreeze_ExactlyThePrice_Succeeds()
    {
        using var client = factory.CreateClientFor(NewUid());
        await SeedAsync((await GetMeAsync(client)).Id, u => u.GainXp(200));

        var response = await client.PostAsync(FreezesUri, null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetFromJsonAsync<LevelResponse>(LevelUri, Ct))!.SpendableXp.ShouldBe(0);
    }

    [Fact]
    public async Task BuyStreakFreeze_OneXpShort_Returns422AndChangesNothing()
    {
        using var client = factory.CreateClientFor(NewUid());
        await SeedAsync((await GetMeAsync(client)).Id, u => u.GainXp(199));

        var response = await client.PostAsync(FreezesUri, null, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "xp.insufficient");
        (await client.GetFromJsonAsync<LevelResponse>(LevelUri, Ct))!.SpendableXp.ShouldBe(199);
        (await client.GetFromJsonAsync<StreakResponse>(StreakUri, Ct))!.StreakFreezes.ShouldBe(0);
    }

    [Fact]
    public async Task BuyStreakFreeze_AtMaximumEvenWithXp_Returns422AndChangesNothing()
    {
        using var client = factory.CreateClientFor(NewUid());
        await SeedAsync((await GetMeAsync(client)).Id, u => SeedFreezes(u, 2, 1000));

        var response = await client.PostAsync(FreezesUri, null, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "streak.freezes_full");
        (await client.GetFromJsonAsync<LevelResponse>(LevelUri, Ct))!.SpendableXp.ShouldBe(1000);
        (await client.GetFromJsonAsync<StreakResponse>(StreakUri, Ct))!.StreakFreezes.ShouldBe(2);
    }

    [Fact]
    public async Task GetStreak_OneMissedDayWithFreeze_SpendsItOnceAndKeepsStreak()
    {
        var time = MondayClock();
        await using var app = WithTime(time);
        using var client = ApiFactory.CreateClientFor(app, NewUid());
        var me = await GetMeAsync(client);
        await SeedAsync(me.Id, u => SeedStreak(u, 5, 1));

        time.Advance(TimeSpan.FromDays(2));
        var first = await client.GetFromJsonAsync<StreakResponse>(StreakUri, Ct);
        var second = await client.GetFromJsonAsync<StreakResponse>(StreakUri, Ct);

        first.ShouldNotBeNull();
        first.CurrentStreak.ShouldBe(5);
        first.StreakFreezes.ShouldBe(0);
        first.FreezesUsedNow.ShouldBe(1);
        first.LastDays.Single(d => d.Date == Monday.AddDays(1)).Protected.ShouldBeTrue();
        first.LastDays.Single(d => d.Date == Monday).Protected.ShouldBeFalse();
        second.ShouldNotBeNull();
        second.CurrentStreak.ShouldBe(5);
        second.FreezesUsedNow.ShouldBe(0);
        second.StreakFreezes.ShouldBe(0);

        var activity = await FindActivityAsync(me.Id, Monday.AddDays(1));
        activity.ShouldNotBeNull();
        activity.FreezeUsedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task SubmitAnswer_AfterProtectedDay_ContinuesStreakAndGivesBonus()
    {
        var time = MondayClock();
        await using var app = WithTime(time);
        var content = await ContentSeeder.SeedAsync(app.Services, activeQuestionsInTopic1: 12);
        using var client = ApiFactory.CreateClientFor(app, NewUid());
        await SeedAsync((await GetMeAsync(client)).Id, u => SeedStreak(u, 5, 1));
        time.Advance(TimeSpan.FromDays(2));
        var quiz = await CreateQuizAsync(client, content.Exam.Id, 12);

        var answers = await AnswerManyAsync(client, quiz, 0, 10);

        answers[^1].DailyProgress.GoalMetNow.ShouldBeTrue();
        answers[^1].DailyProgress.CurrentStreak.ShouldBe(6);
        answers[^1].Xp.Bonus.ShouldBe(80);
        var streak = await client.GetFromJsonAsync<StreakResponse>(StreakUri, Ct);
        streak.ShouldNotBeNull();
        streak.CurrentStreak.ShouldBe(6);
        streak.LongestStreak.ShouldBe(6);
        streak.StreakFreezes.ShouldBe(0);
    }

    [Fact]
    public async Task GetStreak_TwoMissedDaysWithTwoFreezes_SpendsBoth()
    {
        var time = MondayClock();
        await using var app = WithTime(time);
        using var client = ApiFactory.CreateClientFor(app, NewUid());
        await SeedAsync((await GetMeAsync(client)).Id, u => SeedStreak(u, 5, 2));

        time.Advance(TimeSpan.FromDays(3));
        var streak = await client.GetFromJsonAsync<StreakResponse>(StreakUri, Ct);

        streak.ShouldNotBeNull();
        streak.CurrentStreak.ShouldBe(5);
        streak.StreakFreezes.ShouldBe(0);
        streak.FreezesUsedNow.ShouldBe(2);
        streak.LastDays.Count(d => d.Protected).ShouldBe(2);
    }

    [Fact]
    public async Task GetStreak_TwoMissedDaysWithOneFreeze_LosesStreakAndKeepsFreeze()
    {
        var time = MondayClock();
        await using var app = WithTime(time);
        using var client = ApiFactory.CreateClientFor(app, NewUid());
        var me = await GetMeAsync(client);
        await SeedAsync(me.Id, u => SeedStreak(u, 5, 1));

        time.Advance(TimeSpan.FromDays(3));
        var streak = await client.GetFromJsonAsync<StreakResponse>(StreakUri, Ct);

        streak.ShouldNotBeNull();
        streak.CurrentStreak.ShouldBe(0);
        streak.StreakFreezes.ShouldBe(1);
        streak.FreezesUsedNow.ShouldBe(0);
        streak.LongestStreak.ShouldBe(5);
        streak.LastDays.ShouldAllBe(d => !d.Protected);
        (await FindActivityAsync(me.Id, Monday.AddDays(1))).ShouldBeNull();
    }

    [Fact]
    public async Task GetStreak_PartialDayMissed_IsProtectedAndKeepsAnsweredCount()
    {
        var time = MondayClock();
        await using var app = WithTime(time);
        var content = await ContentSeeder.SeedAsync(app.Services, activeQuestionsInTopic1: 12);
        using var client = ApiFactory.CreateClientFor(app, NewUid());
        await SeedAsync((await GetMeAsync(client)).Id, u => SeedStreak(u, 5, 1));
        time.Advance(TimeSpan.FromDays(1));
        var quiz = await CreateQuizAsync(client, content.Exam.Id, 12);
        await AnswerManyAsync(client, quiz, 0, 3);

        time.Advance(TimeSpan.FromDays(1));
        var streak = await client.GetFromJsonAsync<StreakResponse>(StreakUri, Ct);

        streak.ShouldNotBeNull();
        streak.CurrentStreak.ShouldBe(5);
        streak.FreezesUsedNow.ShouldBe(1);
        var tuesday = streak.LastDays.Single(d => d.Date == Monday.AddDays(1));
        tuesday.Protected.ShouldBeTrue();
        tuesday.AnsweredCount.ShouldBe(3);
        tuesday.GoalMet.ShouldBeFalse();
    }

    [Fact]
    public async Task BuyStreakFreeze_StreakAlreadyLost_DoesNotRecoverIt()
    {
        var time = MondayClock();
        await using var app = WithTime(time);
        using var client = ApiFactory.CreateClientFor(app, NewUid());
        await SeedAsync((await GetMeAsync(client)).Id, u =>
        {
            SeedStreak(u, 5, 0);
            u.GainXp(1000);
        });

        time.Advance(TimeSpan.FromDays(3));
        var response = await client.PostAsync(FreezesUri, null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var bought = await response.Content.ReadFromJsonAsync<StreakResponse>(Ct);
        bought.ShouldNotBeNull();
        bought.CurrentStreak.ShouldBe(0);
        bought.StreakFreezes.ShouldBe(1);
        bought.FreezesUsedNow.ShouldBe(0);
        (await client.GetFromJsonAsync<StreakResponse>(StreakUri, Ct))!.CurrentStreak.ShouldBe(0);
    }

    [Fact]
    public async Task GetStreak_NoStreakAndDaysPass_DoesNotSpendFreezes()
    {
        var time = MondayClock();
        await using var app = WithTime(time);
        using var client = ApiFactory.CreateClientFor(app, NewUid());
        await SeedAsync((await GetMeAsync(client)).Id, u => SeedFreezes(u, 1, 0));

        time.Advance(TimeSpan.FromDays(6));
        var streak = await client.GetFromJsonAsync<StreakResponse>(StreakUri, Ct);

        streak.ShouldNotBeNull();
        streak.StreakFreezes.ShouldBe(1);
        streak.FreezesUsedNow.ShouldBe(0);
        streak.CurrentStreak.ShouldBe(0);
    }

    [Fact]
    public async Task SubmitAnswer_AfterMissedDay_SpendsFreezeOnFirstAnswer()
    {
        var time = MondayClock();
        await using var app = WithTime(time);
        var content = await ContentSeeder.SeedAsync(app.Services, activeQuestionsInTopic1: 12);
        using var client = ApiFactory.CreateClientFor(app, NewUid());
        var me = await GetMeAsync(client);
        await SeedAsync(me.Id, u => SeedStreak(u, 5, 1));
        time.Advance(TimeSpan.FromDays(2));
        var quiz = await CreateQuizAsync(client, content.Exam.Id, 12);

        await AnswerManyAsync(client, quiz, 0, 1);

        (await FindActivityAsync(me.Id, Monday.AddDays(1)))!.IsProtected.ShouldBeTrue();
        var streak = await client.GetFromJsonAsync<StreakResponse>(StreakUri, Ct);
        streak.ShouldNotBeNull();
        streak.StreakFreezes.ShouldBe(0);
        streak.CurrentStreak.ShouldBe(5);
        streak.FreezesUsedNow.ShouldBe(0);
    }

    [Fact]
    public async Task UpdateDailyGoal_AfterMissedDay_SpendsFreezeAndReportsIt()
    {
        var time = MondayClock();
        await using var app = WithTime(time);
        using var client = ApiFactory.CreateClientFor(app, NewUid());
        await SeedAsync((await GetMeAsync(client)).Id, u => SeedStreak(u, 5, 1));
        time.Advance(TimeSpan.FromDays(2));

        var response = await client.PutAsJsonAsync(GoalUri, new UpdateDailyGoalRequest(20), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var streak = await response.Content.ReadFromJsonAsync<StreakResponse>(Ct);
        streak.ShouldNotBeNull();
        streak.FreezesUsedNow.ShouldBe(1);
        streak.StreakFreezes.ShouldBe(0);
        streak.CurrentStreak.ShouldBe(5);
        streak.LastDays.Single(d => d.Date == Monday.AddDays(1)).Protected.ShouldBeTrue();
    }

    [Fact]
    public async Task BuyStreakFreeze_OtherUsersFreezes_AreNotVisibleNorAffected()
    {
        using var alice = factory.CreateClientFor(NewUid());
        using var bob = factory.CreateClientFor(NewUid());
        await SeedAsync((await GetMeAsync(alice)).Id, u => u.GainXp(400));
        (await alice.PostAsync(FreezesUri, null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var streak = await bob.GetFromJsonAsync<StreakResponse>(StreakUri, Ct);

        streak.ShouldNotBeNull();
        streak.StreakFreezes.ShouldBe(0);
        await (await bob.PostAsync(FreezesUri, null, Ct)).ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "xp.insufficient");
    }

    [Fact]
    public async Task SaveChanges_TwoConcurrentPurchases_SecondOneGetsConflict()
    {
        using var client = factory.CreateClientFor(NewUid());
        var me = await GetMeAsync(client);
        await SeedAsync(me.Id, u => u.GainXp(1000));
        using var firstScope = factory.Services.CreateScope();
        using var secondScope = factory.Services.CreateScope();
        var firstDb = firstScope.ServiceProvider.GetRequiredService<OpoStudyDbContext>();
        var secondDb = secondScope.ServiceProvider.GetRequiredService<OpoStudyDbContext>();
        var first = await firstDb.Users.FirstAsync(u => u.Id == me.Id, Ct);
        var second = await secondDb.Users.FirstAsync(u => u.Id == me.Id, Ct);
        first.BuyStreakFreeze();
        second.BuyStreakFreeze();
        await firstDb.SaveChangesAsync(Ct);

        var exception = await Should.ThrowAsync<DbUpdateConcurrencyException>(() => secondDb.SaveChangesAsync(Ct));
        var (handled, status) = await HandleAsync(exception);

        handled.ShouldBeTrue();
        status.ShouldBe(StatusCodes.Status409Conflict);
        (await client.GetFromJsonAsync<StreakResponse>(StreakUri, Ct))!.StreakFreezes.ShouldBe(1);
    }

    // ---- Helpers ----

    private static void SeedStreak(User user, int length, int freezes)
    {
        for (var i = length - 1; i >= 0; i--)
        {
            user.Streak.RegisterGoalMet(Monday.AddDays(-i));
        }

        SeedFreezes(user, freezes, 0);
    }

    /// <summary>Buys <paramref name="freezes"/> freezes and leaves <paramref name="extraXp"/> spendable on top.</summary>
    private static void SeedFreezes(User user, int freezes, int extraXp)
    {
        var xp = (freezes * XpRules.StreakFreezePrice) + extraXp;
        if (xp > 0)
        {
            user.GainXp(xp);
        }

        for (var i = 0; i < freezes; i++)
        {
            user.BuyStreakFreeze();
        }
    }

    private async Task SeedAsync(Guid userId, Action<User> change)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpoStudyDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == userId, Ct);
        change(user);
        await db.SaveChangesAsync(Ct);
    }

    private async Task<DailyActivity?> FindActivityAsync(Guid userId, DateOnly date)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpoStudyDbContext>();
        return await db.DailyActivities.AsNoTracking().FirstOrDefaultAsync(a => a.UserId == userId && a.Date == date, Ct);
    }

    private async Task<(bool Handled, int Status)> HandleAsync(Exception exception)
    {
        using var scope = factory.Services.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Response.Body = new MemoryStream();
        var handled = await new DomainRuleExceptionHandler().TryHandleAsync(context, exception, Ct);
        return (handled, context.Response.StatusCode);
    }

    private static async Task<UserProfileResponse> GetMeAsync(HttpClient client)
    {
        var me = await client.GetFromJsonAsync<UserProfileResponse>(MeUri, Ct);
        return me ?? throw new InvalidOperationException("The profile response was empty.");
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
            results.Add(await response.Content.ReadFromJsonAsync<AnswerFeedbackResponse>(Ct) ?? throw new InvalidOperationException("Empty answer."));
        }

        results.Count.ShouldBe(count);
        return results;
    }
}