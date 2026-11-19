using System.Net;
using System.Net.Http.Json;

using OpoStudy.Contracts.Exams;

using Shouldly;

namespace OpoStudy.Api.IntegrationTests;

public sealed class ExamsEndpointsTests(ApiFactory factory)
{
    private static readonly Uri ExamsUri = new("/api/v1/exams", UriKind.Relative);

    private static string NewUid() => $"uid-{Guid.NewGuid():N}";

    private static Uri TopicsUri(Guid examId) => new($"/api/v1/exams/{examId}/topics", UriKind.Relative);

    [Fact]
    public async Task ListExams_WithoutToken_Returns401()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(ExamsUri, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListTopics_WithoutToken_Returns401()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(TopicsUri(Guid.NewGuid()), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListExams_WithInactiveExam_ReturnsOnlyActiveOnes()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());

        var exams = await client.GetFromJsonAsync<List<ExamResponse>>(ExamsUri, TestContext.Current.CancellationToken);

        exams.ShouldNotBeNull();
        exams.ShouldContain(e => e.Id == content.Exam.Id);
        exams.ShouldNotContain(e => e.Id == content.InactiveExam.Id);
        var seeded = exams.Single(e => e.Id == content.Exam.Id);
        seeded.Slug.ShouldBe(content.Exam.Slug);
        seeded.Group.ShouldBe("C1");
    }

    [Fact]
    public async Task ListTopics_WithActiveExam_ReturnsTopicsOrderedWithActiveQuestionCount()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());

        var topics = await client.GetFromJsonAsync<List<TopicResponse>>(TopicsUri(content.Exam.Id), TestContext.Current.CancellationToken);

        topics.ShouldNotBeNull();
        topics.Select(t => t.Number).ShouldBe([1, 2, 3]);
        topics[0].QuestionCount.ShouldBe(4); // The inactive question is not counted.
        topics[0].Section.ShouldBe("Block I");
        topics[1].QuestionCount.ShouldBe(3);
        topics[2].QuestionCount.ShouldBe(0);
    }

    [Fact]
    public async Task ListTopics_WithUnknownExam_ReturnsProblemDetails404()
    {
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.GetAsync(TopicsUri(Guid.NewGuid()), TestContext.Current.CancellationToken);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "exam.not_found");
    }

    [Fact]
    public async Task ListTopics_WithInactiveExam_ReturnsProblemDetails404()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.GetAsync(TopicsUri(content.InactiveExam.Id), TestContext.Current.CancellationToken);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "exam.not_found");
    }
}
