using System.Net;
using System.Net.Http.Json;

using OpoStudy.Contracts.PracticalCases;

using Shouldly;

namespace OpoStudy.Api.IntegrationTests;

public sealed class PracticalCasesEndpointsTests(ApiFactory factory)
{
    private static string NewUid() => $"uid-{Guid.NewGuid():N}";

    private static Uri ListUri(Guid examId) => new($"/api/v1/exams/{examId}/practical-cases", UriKind.Relative);

    private static Uri DetailUri(Guid id) => new($"/api/v1/practical-cases/{id}", UriKind.Relative);

    [Fact]
    public async Task ListPracticalCases_WithoutToken_Returns401()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(ListUri(Guid.NewGuid()), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPracticalCase_WithoutToken_Returns401()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(DetailUri(Guid.NewGuid()), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListPracticalCases_WithActiveExam_ReturnsActiveOnesOrderedByKey()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());

        var cases = await client.GetFromJsonAsync<List<PracticalCaseSummaryResponse>>(ListUri(content.Exam.Id), TestContext.Current.CancellationToken);

        cases.ShouldNotBeNull();
        cases.Select(c => c.Id).ShouldBe([content.PracticalCases.CaseA.Id, content.PracticalCases.CaseB.Id]);
        cases[0].ShouldBe(new PracticalCaseSummaryResponse(content.PracticalCases.CaseA.Id, "Case a", "Section a", 2, 10, 30));
        cases[1].ShouldBe(new PracticalCaseSummaryResponse(content.PracticalCases.CaseB.Id, "Case b", null, 3, 15, 30));
    }

    [Fact]
    public async Task ListPracticalCases_WithUnknownExam_Returns404()
    {
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.GetAsync(ListUri(Guid.NewGuid()), TestContext.Current.CancellationToken);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "exam.not_found");
    }

    [Fact]
    public async Task ListPracticalCases_WithInactiveExam_Returns404()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.GetAsync(ListUri(content.InactiveExam.Id), TestContext.Current.CancellationToken);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "exam.not_found");
    }

    [Fact]
    public async Task GetPracticalCase_WithSection_ReturnsIt()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());

        var practicalCase = await client.GetFromJsonAsync<PracticalCaseResponse>(
            DetailUri(content.PracticalCases.CaseA.Id),
            TestContext.Current.CancellationToken);

        practicalCase.ShouldNotBeNull();
        practicalCase.Section.ShouldBe("Section a");
    }

    [Fact]
    public async Task GetPracticalCase_WithActiveCase_ReturnsPartsInOrder()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());

        var practicalCase = await client.GetFromJsonAsync<PracticalCaseResponse>(
            DetailUri(content.PracticalCases.CaseB.Id),
            TestContext.Current.CancellationToken);

        practicalCase.ShouldNotBeNull();
        practicalCase.ExamId.ShouldBe(content.Exam.Id);
        practicalCase.Title.ShouldBe("Case b");
        practicalCase.Section.ShouldBeNull();
        practicalCase.Statement.ShouldBe("Statement of case b");
        practicalCase.EstimatedMinutes.ShouldBe(30);
        practicalCase.MaxPoints.ShouldBe(15);
        practicalCase.Parts.Select(p => p.Order).ShouldBe([0, 1, 2]);
        practicalCase.Parts.ShouldAllBe(p => p.MaxPoints == 5);
        practicalCase.Parts[1].Statement.ShouldBe("Part 1 of b");
    }

    [Fact]
    public async Task GetPracticalCase_RawJson_DoesNotExposeCriteriaNorModelAnswers()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.GetAsync(DetailUri(content.PracticalCases.CaseA.Id), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        json.ShouldNotContain("criteria", Case.Insensitive);
        json.ShouldNotContain("modelAnswer", Case.Insensitive);
        json.ShouldNotContain("SECRET");
    }

    [Fact]
    public async Task GetPracticalCase_WithUnknownId_Returns404()
    {
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.GetAsync(DetailUri(Guid.NewGuid()), TestContext.Current.CancellationToken);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "practical_case.not_found");
    }

    [Fact]
    public async Task GetPracticalCase_WithInactiveCase_Returns404()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.GetAsync(DetailUri(content.PracticalCases.InactiveCase.Id), TestContext.Current.CancellationToken);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "practical_case.not_found");
    }

    [Fact]
    public async Task GetPracticalCase_WithInactiveExam_Returns404()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.GetAsync(DetailUri(content.PracticalCases.CaseOfInactiveExam.Id), TestContext.Current.CancellationToken);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "practical_case.not_found");
    }
}