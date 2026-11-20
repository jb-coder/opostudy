using System.Net;
using System.Net.Http.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using OpoStudy.Contracts.Offline;
using OpoStudy.Domain.Questions;
using OpoStudy.Infrastructure.Persistence;

using Shouldly;

namespace OpoStudy.Api.IntegrationTests;

public sealed class OfflineContentEndpointsTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string NewUid() => $"uid-{Guid.NewGuid():N}";

    private static Uri ContentUri(Guid examId) => new($"/api/v1/exams/{examId}/offline-content", UriKind.Relative);

    private static HttpRequestMessage ConditionalRequest(Guid examId, string ifNoneMatch)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, ContentUri(examId));
        request.Headers.TryAddWithoutValidation("If-None-Match", ifNoneMatch);
        return request;
    }

    [Fact]
    public async Task GetOfflineContent_WithoutToken_Returns401()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(ContentUri(Guid.NewGuid()), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetOfflineContent_UnknownExam_Returns404()
    {
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.GetAsync(ContentUri(Guid.NewGuid()), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "exam.not_found");
    }

    [Fact]
    public async Task GetOfflineContent_InactiveExam_Returns404()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.GetAsync(ContentUri(content.InactiveExam.Id), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "exam.not_found");
    }

    [Fact]
    public async Task GetOfflineContent_ActiveExam_ReturnsActiveQuestionsWithCorrectOptionAndETag()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.GetAsync(ContentUri(content.Exam.Id), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<OfflineContentResponse>(Ct);
        body.ShouldNotBeNull();
        body.ExamId.ShouldBe(content.Exam.Id);
        body.Exam.Id.ShouldBe(content.Exam.Id);
        response.Headers.ETag?.Tag.ShouldBe($"\"{body.Version}\"");
        response.Headers.CacheControl?.NoCache.ShouldBeTrue();
        body.Version.ShouldMatch("^[0-9a-f]{64}$");

        body.Topics.Select(t => t.Number).ShouldBe([1, 2, 3]);
        body.Topics.Select(t => t.Id).ShouldBe([content.Topic1.Id, content.Topic2.Id, content.EmptyTopic.Id]);
        body.Topics[0].Section.ShouldBe("Block I");
        body.Topics[2].Questions.ShouldBeEmpty();

        var returned = body.Topics.SelectMany(t => t.Questions).ToList();
        returned.Count.ShouldBe(7);
        returned.Select(q => q.Id).ShouldNotContain(content.InactiveQuestionOf(content.Topic1).Id);
        foreach (var question in returned)
        {
            var seeded = content.Questions[question.Id];
            question.Statement.ShouldBe(seeded.Statement);
            question.Explanation.ShouldBe(seeded.Explanation);
            question.LegalReference.ShouldBe(seeded.LegalReference);
            question.Options.Select(o => o.Order).ShouldBe([0, 1, 2, 3]);
            question.Options.Count(o => o.IsCorrect).ShouldBe(1);
            question.Options.Single(o => o.IsCorrect).Id.ShouldBe(seeded.CorrectOption.Id);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetOfflineContent_WithMatchingIfNoneMatch_Returns304WithoutBody(bool quoted)
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var first = await client.GetFromJsonAsync<OfflineContentResponse>(ContentUri(content.Exam.Id), Ct);
        first.ShouldNotBeNull();

        using var request = ConditionalRequest(content.Exam.Id, quoted ? $"\"{first.Version}\"" : first.Version);
        var response = await client.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotModified);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldBeEmpty();
        response.Headers.ETag?.Tag.ShouldBe($"\"{first.Version}\"");
    }

    [Fact]
    public async Task GetOfflineContent_WithStaleIfNoneMatch_Returns200()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());

        using var request = ConditionalRequest(content.Exam.Id, "\"stale\"");
        var response = await client.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetOfflineContent_AfterChangingAQuestionText_ChangesTheETag()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var before = await client.GetFromJsonAsync<OfflineContentResponse>(ContentUri(content.Exam.Id), Ct);
        before.ShouldNotBeNull();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OpoStudyDbContext>();
            var id = content.ActiveQuestionsOf(content.Topic2).First().Id;
            var question = await db.Questions.FirstAsync(q => q.Id == id, Ct);
            question.Update(
                question.TopicId,
                "Changed statement?",
                question.Explanation,
                question.LegalReference,
                [.. question.Options.Select(o => new AnswerOptionData(o.Text, o.IsCorrect))]);
            await db.SaveChangesAsync(Ct);
        }

        using var request = ConditionalRequest(content.Exam.Id, $"\"{before.Version}\"");
        var response = await client.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var after = await response.Content.ReadFromJsonAsync<OfflineContentResponse>(Ct);
        after.ShouldNotBeNull();
        after.Version.ShouldNotBe(before.Version);
        response.Headers.ETag?.Tag.ShouldBe($"\"{after.Version}\"");
    }
}