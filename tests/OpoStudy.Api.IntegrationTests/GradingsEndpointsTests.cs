using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

using OpoStudy.Contracts.Gradings;
using OpoStudy.Contracts.Progress;
using OpoStudy.Contracts.Users;
using OpoStudy.Domain.Gradings;
using OpoStudy.Infrastructure.Persistence;

using Shouldly;

namespace OpoStudy.Api.IntegrationTests;

/// <summary>The quota is configured to 2 in <see cref="ApiFactory"/>. Each test uses its own user and content.</summary>
public sealed class GradingsEndpointsTests(ApiFactory factory)
{
    private static readonly Uri GradingsUri = new("/api/v1/gradings", UriKind.Relative);
    private static readonly Uri QuotaUri = new("/api/v1/me/grading-quota", UriKind.Relative);
    private static readonly Uri MeUri = new("/api/v1/me", UriKind.Relative);
    private static readonly Uri ZoneUri = new("/api/v1/me/time-zone", UriKind.Relative);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string NewUid() => $"uid-{Guid.NewGuid():N}";

    private static Uri SubmitUri(Guid caseId) => new($"/api/v1/practical-cases/{caseId}/gradings", UriKind.Relative);

    private static Uri DetailUri(Guid id) => new($"/api/v1/gradings/{id}", UriKind.Relative);

    // Case A of the seeder has two parts.
    private static SubmitGradingRequest TwoAnswers(string first = "First answer", string second = "Second answer") => new([first, second]);

    private static async Task<GradingResponse> SubmitOkAsync(HttpClient client, Guid caseId, SubmitGradingRequest? request = null)
    {
        var response = await client.PostAsJsonAsync(SubmitUri(caseId), request ?? TwoAnswers(), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        return (await response.Content.ReadFromJsonAsync<GradingResponse>(Ct))!;
    }

    private static async Task MutateAsync(IServiceProvider services, Guid gradingId, Action<Grading> change)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpoStudyDbContext>();
        var grading = await db.Gradings.SingleAsync(g => g.Id == gradingId, Ct);
        change(grading);
        await db.SaveChangesAsync(Ct);
    }

    private static GradingOutcome FullMarks(Grading grading) =>
        new(
            [.. grading.Answers.Select(a => new PartOutcome([.. a.Criteria.Select(c => (decimal)c.Points)], "Part feedback"))],
            "General feedback",
            "test",
            null,
            null);

    private static Task CompleteAsync(IServiceProvider services, Guid gradingId) =>
        MutateAsync(services, gradingId, g =>
        {
            g.Start(DateTimeOffset.UtcNow);
            g.Complete(FullMarks(g), DateTimeOffset.UtcNow);
        });

    private static Task FailAsync(IServiceProvider services, Guid gradingId) =>
        MutateAsync(services, gradingId, g =>
        {
            g.Start(DateTimeOffset.UtcNow);
            g.Fail("grading.provider_error", DateTimeOffset.UtcNow);
        });

    private WebApplicationFactory<Program> WithTime(FakeTimeProvider time) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(time);
        }));

    [Fact]
    public async Task SubmitGrading_WithoutToken_Returns401()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(SubmitUri(Guid.NewGuid()), TwoAnswers(), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ReadEndpoints_WithoutToken_Return401()
    {
        using var client = factory.CreateClient();

        (await client.GetAsync(DetailUri(Guid.NewGuid()), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.GetAsync(GradingsUri, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.GetAsync(QuotaUri, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SubmitGrading_WithValidAnswers_Returns202WithLocationAndPendingGradingWithoutRubric()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.PostAsJsonAsync(SubmitUri(content.PracticalCases.CaseA.Id), TwoAnswers("  one  ", ""), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var grading = (await response.Content.ReadFromJsonAsync<GradingResponse>(Ct))!;
        response.Headers.Location.ShouldBe(new Uri($"/api/v1/gradings/{grading.Id}", UriKind.Relative));
        grading.Status.ShouldBe("Pending");
        grading.PracticalCaseId.ShouldBe(content.PracticalCases.CaseA.Id);
        grading.CaseTitle.ShouldBe("Case a");
        grading.MaxPoints.ShouldBe(10);
        grading.Attempts.ShouldBe(0);
        grading.Score.ShouldBeNull();
        grading.Answers.Select(a => a.Text).ShouldBe(["one", string.Empty]);
        grading.Answers.Select(a => a.Statement).ShouldBe(["Part 0 of a", "Part 1 of a"]);
        grading.Answers.ShouldAllBe(a => a.Criteria == null && a.ModelAnswer == null && a.Score == null);
        var json = await response.Content.ReadAsStringAsync(Ct);
        json.ShouldNotContain("SECRET");
    }

    [Fact]
    public async Task SubmitGrading_StoresACopyOfTheRubric()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());

        var grading = await SubmitOkAsync(client, content.PracticalCases.CaseA.Id);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpoStudyDbContext>();
        var stored = await db.Gradings.AsNoTracking().SingleAsync(g => g.Id == grading.Id, Ct);
        stored.Answers[0].Criteria.ShouldBe([new CriterionScore("SECRET-CRITERION 0", 2, null), new CriterionScore("Another criterion", 3, null)]);
        stored.Answers.Select(a => a.MaxPoints).ShouldBe([5, 5]);
    }

    [Fact]
    public async Task SubmitGrading_WithUnknownCase_Returns404()
    {
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.PostAsJsonAsync(SubmitUri(Guid.NewGuid()), TwoAnswers(), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "practical_case.not_found");
    }

    [Fact]
    public async Task SubmitGrading_WithInactiveCaseOrInactiveExam_Returns404()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());

        var inactiveCase = await client.PostAsJsonAsync(SubmitUri(content.PracticalCases.InactiveCase.Id), new SubmitGradingRequest(["a"]), Ct);
        var inactiveExam = await client.PostAsJsonAsync(SubmitUri(content.PracticalCases.CaseOfInactiveExam.Id), new SubmitGradingRequest(["a"]), Ct);

        await inactiveCase.ShouldBeProblemAsync(HttpStatusCode.NotFound, "practical_case.not_found");
        await inactiveExam.ShouldBeProblemAsync(HttpStatusCode.NotFound, "practical_case.not_found");
    }

    [Fact]
    public async Task SubmitGrading_WithWrongNumberOfAnswers_Returns422()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.PostAsJsonAsync(SubmitUri(content.PracticalCases.CaseA.Id), new SubmitGradingRequest(["only one"]), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "grading.invalid_answer_count");
    }

    [Fact]
    public async Task SubmitGrading_WithMoreAnswersThanTheMaximumOfParts_Returns422()
    {
        using var client = factory.CreateClientFor(NewUid());
        var answers = Enumerable.Repeat("answer", 11).ToArray();

        // Rejected before any lookup, so the case does not even need to exist.
        var response = await client.PostAsJsonAsync(SubmitUri(Guid.NewGuid()), new SubmitGradingRequest(answers), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "grading.invalid_answer_count");
    }

    [Fact]
    public async Task GetGrading_WhenTheCaseLostAPart_ReturnsEmptyStatementInsteadOfFailing()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var submitted = await SubmitOkAsync(client, content.PracticalCases.CaseA.Id);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OpoStudyDbContext>();
            var caseId = content.PracticalCases.CaseA.Id;
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""DELETE FROM practical_case_parts WHERE practical_case_id = {caseId} AND "order" = 1""", Ct);
        }

        var response = await client.GetAsync(DetailUri(submitted.Id), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var grading = await response.Content.ReadFromJsonAsync<GradingResponse>(Ct);
        grading!.Answers[1].Statement.ShouldBe(string.Empty);
        grading.Answers[0].Statement.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task SubmitGrading_WithTooLongAnswer_Returns422()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.PostAsJsonAsync(SubmitUri(content.PracticalCases.CaseA.Id), TwoAnswers(new string('x', 8001)), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "grading.invalid_answer");
    }

    [Fact]
    public async Task SubmitGrading_WithAllAnswersBlank_Returns422()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.PostAsJsonAsync(SubmitUri(content.PracticalCases.CaseA.Id), TwoAnswers("", "   "), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "grading.empty");
    }

    [Fact]
    public async Task SubmitGrading_WithoutAnswers_Returns422()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.PostAsJsonAsync(SubmitUri(content.PracticalCases.CaseA.Id), new { answers = (string[]?)null }, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "grading.invalid_answer_count");
    }

    [Fact]
    public async Task SubmitGrading_WhileAnotherIsInProgress_Returns409()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        await SubmitOkAsync(client, content.PracticalCases.CaseA.Id);

        var response = await client.PostAsJsonAsync(SubmitUri(content.PracticalCases.CaseB.Id), new SubmitGradingRequest(["a", "b", "c"]), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "grading.already_pending");
    }

    [Fact]
    public async Task SubmitGrading_ConcurrentSubmissionsOfTheSameUser_OnlyOneIsAccepted()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        _ = await client.GetAsync(MeUri, Ct); // Create the user first so both requests race on the gradings, not on the user.

        var responses = await Task.WhenAll(Enumerable.Range(0, 4)
            .Select(_ => client.PostAsJsonAsync(SubmitUri(content.PracticalCases.CaseA.Id), TwoAnswers(), Ct)));

        responses.Count(r => r.StatusCode == HttpStatusCode.Accepted).ShouldBe(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(3);
        foreach (var conflict in responses.Where(r => r.StatusCode == HttpStatusCode.Conflict))
        {
            await conflict.ShouldBeProblemAsync(HttpStatusCode.Conflict, "grading.already_pending");
        }
    }

    [Fact]
    public async Task SubmitGrading_WithQuotaSpent_Returns422()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        for (var i = 0; i < 2; i++)
        {
            var grading = await SubmitOkAsync(client, content.PracticalCases.CaseA.Id);
            await CompleteAsync(factory.Services, grading.Id);
        }

        var response = await client.PostAsJsonAsync(SubmitUri(content.PracticalCases.CaseA.Id), TwoAnswers(), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var problem = await response.Content.ReadFromJsonAsync<ProblemBody>(Ct);
        problem!.Code.ShouldBe("grading.quota_exceeded");
        problem.Title.ShouldBe("Has agotado tus 2 correcciones de este mes.");
    }

    [Fact]
    public async Task SubmitGrading_AfterFailedGradings_TheyDoNotCountAgainstTheQuota()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        for (var i = 0; i < 3; i++)
        {
            var grading = await SubmitOkAsync(client, content.PracticalCases.CaseA.Id);
            await FailAsync(factory.Services, grading.Id);
        }

        var quota = await client.GetFromJsonAsync<GradingQuotaResponse>(QuotaUri, Ct);

        quota!.Used.ShouldBe(0);
        quota.Remaining.ShouldBe(2);
    }

    [Fact]
    public async Task GetGrading_WhilePending_HidesTheRubricAndTheModelAnswer()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var submitted = await SubmitOkAsync(client, content.PracticalCases.CaseA.Id);

        var response = await client.GetAsync(DetailUri(submitted.Id), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync(Ct);
        json.ShouldNotContain("SECRET");
        json.ShouldNotContain("Another criterion");
        var grading = await response.Content.ReadFromJsonAsync<GradingResponse>(Ct);
        grading!.Status.ShouldBe("Pending");
        grading.Answers.ShouldAllBe(a => a.Criteria == null && a.ModelAnswer == null);
    }

    [Fact]
    public async Task GetGrading_WhenCompletedWithProgress_ReturnsXpAndGoal()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var submitted = await SubmitOkAsync(client, content.PracticalCases.CaseA.Id);
        await MutateAsync(factory.Services, submitted.Id, g =>
        {
            g.Start(DateTimeOffset.UtcNow);
            g.Complete(FullMarks(g), DateTimeOffset.UtcNow);
            g.RecordProgress(155, true);
        });

        var grading = await client.GetFromJsonAsync<GradingResponse>(DetailUri(submitted.Id), Ct);

        grading.ShouldNotBeNull();
        grading.XpEarned.ShouldBe(155);
        grading.MetDailyGoal.ShouldBeTrue();
    }

    [Fact]
    public async Task GetGrading_WhenCompletedWithoutRecordedProgress_ReturnsNullXp()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var submitted = await SubmitOkAsync(client, content.PracticalCases.CaseA.Id);
        await CompleteAsync(factory.Services, submitted.Id);

        var grading = await client.GetFromJsonAsync<GradingResponse>(DetailUri(submitted.Id), Ct);

        grading.ShouldNotBeNull();
        grading.XpEarned.ShouldBeNull();
        grading.MetDailyGoal.ShouldBeFalse();
    }

    [Fact]
    public async Task GetGrading_WhenCompleted_ReturnsScoresCriteriaAndModelAnswer()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var submitted = await SubmitOkAsync(client, content.PracticalCases.CaseA.Id);
        await MutateAsync(factory.Services, submitted.Id, g =>
        {
            g.Start(DateTimeOffset.UtcNow);
            g.Complete(
                new GradingOutcome(
                    [new PartOutcome([1.5m, 3m], "Feedback 0"), new PartOutcome([2m, 0m], "Feedback 1")],
                    "General feedback",
                    "test",
                    null,
                    null),
                DateTimeOffset.UtcNow);
        });

        var grading = await client.GetFromJsonAsync<GradingResponse>(DetailUri(submitted.Id), Ct);

        grading.ShouldNotBeNull();
        grading.Status.ShouldBe("Completed");
        grading.Attempts.ShouldBe(1);
        grading.CompletedAt.ShouldNotBeNull();
        grading.Score.ShouldBe(6.5m);
        grading.MaxPoints.ShouldBe(10);
        grading.Feedback.ShouldBe("General feedback");
        grading.FailureCode.ShouldBeNull();
        grading.Answers.Select(a => a.Score).ShouldBe([4.5m, 2m]);
        grading.Answers[0].Feedback.ShouldBe("Feedback 0");
        grading.Answers[0].ModelAnswer.ShouldBe("SECRET-MODEL-ANSWER 0");
        grading.Answers[0].Statement.ShouldBe("Part 0 of a");
        grading.Answers[0].Criteria.ShouldBe(
        [
            new CriterionScoreResponse("SECRET-CRITERION 0", 2, 1.5m),
            new CriterionScoreResponse("Another criterion", 3, 3m),
        ]);
    }

    [Fact]
    public async Task GetGrading_WhenFailed_ReturnsTheFailureCodeWithoutRubric()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var submitted = await SubmitOkAsync(client, content.PracticalCases.CaseA.Id);
        await FailAsync(factory.Services, submitted.Id);

        var grading = await client.GetFromJsonAsync<GradingResponse>(DetailUri(submitted.Id), Ct);

        grading!.Status.ShouldBe("Failed");
        grading.FailureCode.ShouldBe("grading.provider_error");
        grading.Score.ShouldBeNull();
        grading.Answers.ShouldAllBe(a => a.Criteria == null && a.ModelAnswer == null);
    }

    [Fact]
    public async Task GetGrading_OfAnotherUserOrUnknown_Returns404()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var owner = factory.CreateClientFor(NewUid());
        using var other = factory.CreateClientFor(NewUid());
        var submitted = await SubmitOkAsync(owner, content.PracticalCases.CaseA.Id);

        var foreign = await other.GetAsync(DetailUri(submitted.Id), Ct);
        var unknown = await other.GetAsync(DetailUri(Guid.NewGuid()), Ct);

        await foreign.ShouldBeProblemAsync(HttpStatusCode.NotFound, "grading.not_found");
        await unknown.ShouldBeProblemAsync(HttpStatusCode.NotFound, "grading.not_found");
    }

    [Fact]
    public async Task ListGradings_ReturnsOwnGradingsNewestFirstAndPaginates()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        using var other = factory.CreateClientFor(NewUid());
        var first = await SubmitOkAsync(client, content.PracticalCases.CaseA.Id);
        await FailAsync(factory.Services, first.Id);
        var second = await SubmitOkAsync(client, content.PracticalCases.CaseB.Id, new SubmitGradingRequest(["a", "b", "c"]));
        await FailAsync(factory.Services, second.Id);
        var third = await SubmitOkAsync(client, content.PracticalCases.CaseA.Id);
        await SubmitOkAsync(other, content.PracticalCases.CaseA.Id);

        var all = await client.GetFromJsonAsync<List<GradingSummaryResponse>>(GradingsUri, Ct);
        var page = await client.GetFromJsonAsync<List<GradingSummaryResponse>>(new Uri("/api/v1/gradings?skip=1&take=1", UriKind.Relative), Ct);

        all.ShouldNotBeNull();
        all.Select(g => g.Id).ShouldBe([third.Id, second.Id, first.Id]);
        // The database keeps microseconds, so the submission time is compared with a tolerance.
        all[0].ShouldBe(new GradingSummaryResponse(third.Id, content.PracticalCases.CaseA.Id, "Case a", "Section a", "Pending", all[0].SubmittedAt, 10, null));
        all[0].SubmittedAt.ShouldBe(third.SubmittedAt, TimeSpan.FromMilliseconds(1));
        all[1].Section.ShouldBeNull();
        all[1].Status.ShouldBe("Failed");
        all[1].MaxPoints.ShouldBe(15);
        page!.Select(g => g.Id).ShouldBe([second.Id]);
    }

    [Theory]
    [InlineData("skip=-1")]
    [InlineData("take=0")]
    [InlineData("take=51")]
    public async Task ListGradings_WithInvalidPaging_Returns400(string query)
    {
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.GetAsync(new Uri($"/api/v1/gradings?{query}", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetGradingQuota_ForNewUser_ReturnsFullQuotaAndNextMonthReset()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 5, 20, 10, 0, 0, TimeSpan.Zero));
        await using var app = WithTime(time);
        using var client = ApiFactory.CreateClientFor(app, NewUid());

        var quota = await client.GetFromJsonAsync<GradingQuotaResponse>(QuotaUri, Ct);

        // Default zone Europe/Madrid (UTC+2 in summer): June 1st 00:00 local.
        quota.ShouldBe(new GradingQuotaResponse(2, 0, 2, new DateTimeOffset(2026, 5, 31, 22, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public async Task GetGradingQuota_CloseToMonthChangeInTheUserTimeZone_UsesTheLocalMonth()
    {
        // 2026-03-31 10:00 UTC is 23:00 in Auckland (UTC+13): still March there.
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 3, 31, 10, 0, 0, TimeSpan.Zero));
        await using var app = WithTime(time);
        var content = await ContentSeeder.SeedAsync(app.Services);
        using var client = ApiFactory.CreateClientFor(app, NewUid());
        (await client.PutAsJsonAsync(ZoneUri, new UpdateTimeZoneRequest("Pacific/Auckland"), Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var grading = await SubmitOkAsync(client, content.PracticalCases.CaseA.Id);
        await CompleteAsync(app.Services, grading.Id);

        var march = await client.GetFromJsonAsync<GradingQuotaResponse>(QuotaUri, Ct);

        march.ShouldBe(new GradingQuotaResponse(2, 1, 1, new DateTimeOffset(2026, 3, 31, 11, 0, 0, TimeSpan.Zero)));

        // 90 minutes later it is April 1st in Auckland: the grading belongs to the previous month.
        time.Advance(TimeSpan.FromMinutes(90));
        var april = await client.GetFromJsonAsync<GradingQuotaResponse>(QuotaUri, Ct);

        april.ShouldBe(new GradingQuotaResponse(2, 0, 2, new DateTimeOffset(2026, 4, 30, 12, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public async Task DeleteMe_WithGradings_DeletesThem()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var grading = await SubmitOkAsync(client, content.PracticalCases.CaseA.Id);

        var response = await client.DeleteAsync(MeUri, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpoStudyDbContext>();
        (await db.Gradings.CountAsync(g => g.Id == grading.Id, Ct)).ShouldBe(0);
        (await db.Set<GradingAnswer>().CountAsync(a => a.GradingId == grading.Id, Ct)).ShouldBe(0);
    }
}