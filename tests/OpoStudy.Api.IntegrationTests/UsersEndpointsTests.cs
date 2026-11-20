using System.Net;
using System.Net.Http.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using OpoStudy.Contracts.Users;
using OpoStudy.Domain.Exams;
using OpoStudy.Domain.Questions;
using OpoStudy.Domain.Quizzes;
using OpoStudy.Infrastructure.Persistence;

using Shouldly;

namespace OpoStudy.Api.IntegrationTests;

public sealed class UsersEndpointsTests(ApiFactory factory)
{
    private static readonly Uri MeUri = new("/api/v1/me", UriKind.Relative);

    private static string NewUid() => $"uid-{Guid.NewGuid():N}";

    [Fact]
    public async Task GetMe_WithoutToken_ReturnsProblemDetails401()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(MeUri, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task GetMe_CalledTwice_CreatesUserOnceAndReturnsSameId()
    {
        using var client = factory.CreateClientFor(NewUid());

        var first = await client.GetFromJsonAsync<UserProfileResponse>(MeUri, TestContext.Current.CancellationToken);
        var second = await client.GetFromJsonAsync<UserProfileResponse>(MeUri, TestContext.Current.CancellationToken);

        first.ShouldNotBeNull();
        second.ShouldNotBeNull();
        second.Id.ShouldBe(first.Id);
        first.ActiveExamId.ShouldBeNull();
    }

    [Fact]
    public async Task GetMe_ConcurrentRequestsForNewUid_CreateSingleUser()
    {
        var uid = NewUid();
        using var client = factory.CreateClientFor(uid);

        var responses = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => client.GetAsync(MeUri, TestContext.Current.CancellationToken)));

        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.OK);
        var ids = new HashSet<Guid>();
        foreach (var response in responses)
        {
            var profile = await response.Content.ReadFromJsonAsync<UserProfileResponse>(TestContext.Current.CancellationToken);
            ids.Add(profile!.Id);
        }

        ids.Count.ShouldBe(1);
        (await CountUsersAsync(uid)).ShouldBe(1);
    }

    [Fact]
    public async Task GetMe_Response_DoesNotExposeFirebaseUid()
    {
        var uid = NewUid();
        using var client = factory.CreateClientFor(uid);

        var json = await client.GetStringAsync(MeUri, TestContext.Current.CancellationToken);

        json.ShouldNotContain(uid);
        json.ToLowerInvariant().ShouldNotContain("firebase");
    }

    [Fact]
    public async Task DeleteMe_WithData_DeletesUserAndQuizAttempts()
    {
        var uid = NewUid();
        using var client = factory.CreateClientFor(uid);
        var profile = await client.GetFromJsonAsync<UserProfileResponse>(MeUri, TestContext.Current.CancellationToken);
        await SeedQuizAttemptAsync(profile!.Id);

        var response = await client.DeleteAsync(MeUri, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await CountUsersAsync(uid)).ShouldBe(0);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpoStudyDbContext>();
        (await db.QuizAttempts.CountAsync(a => a.UserId == profile.Id, TestContext.Current.CancellationToken)).ShouldBe(0);
    }

    [Fact]
    public async Task DeleteMe_WithoutExistingUser_ReturnsNoContent()
    {
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.DeleteAsync(MeUri, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteMe_WithoutToken_Returns401()
    {
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync(MeUri, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task<int> CountUsersAsync(string uid)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpoStudyDbContext>();
        return await db.Users.CountAsync(u => u.FirebaseUid == uid, TestContext.Current.CancellationToken);
    }

    private async Task SeedQuizAttemptAsync(Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpoStudyDbContext>();

        var slug = $"seed-{Guid.NewGuid():N}";
        var exam = Exam.Create(slug, "Seed exam", "Seed organization", "C1");
        var topic = Topic.Create(exam.Id, "seed-topic", 1, "Seed topic", null);
        var question = Question.Create(
            topic.Id,
            $"{slug}/0001",
            "Statement?",
            "Explanation",
            "CE art. 1",
            [new AnswerOptionData("A", true), new AnswerOptionData("B", false), new AnswerOptionData("C", false)]);
        var attempt = QuizAttempt.Create(userId, exam.Id, topic.Id, QuizMode.ByTopic, [question.Id], DateTimeOffset.UtcNow);

        db.AddRange(exam, topic, question, attempt);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}