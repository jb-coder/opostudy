using System.Net;
using System.Text;

using OpoStudy.Mobile.Core.Api;

using Refit;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests;

public sealed class OpoStudyApiTests
{
    [Fact]
    public async Task GetVersionAsync_ValidResponse_CallsVersionRouteAndDeserializes()
    {
        using var handler = new StubHandler("""{"version":"0.1.0"}""");
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5054") };
        var api = RestService.ForGenerated<IOpoStudyApi>(client, ApiJsonContext.Default);

        var response = await api.GetVersionAsync(TestContext.Current.CancellationToken);

        response.Version.ShouldBe("0.1.0");
        handler.LastRequestUri.ShouldBe(new Uri("http://localhost:5054/api/v1/version"));
    }

    [Fact]
    public async Task GetActivityAsync_Dates_AreSentAsIsoWithFromAndToNames()
    {
        using var handler = new StubHandler("""{"today":"2026-11-24","dailyGoal":10,"days":[{"date":"2026-11-03","answeredCount":12,"goal":10,"goalMet":true,"protected":false,"xpEarned":160}]}""");
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5054") };
        var api = RestService.ForGenerated<IOpoStudyApi>(client, ApiJsonContext.Default);

        var response = await api.GetActivityAsync(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), TestContext.Current.CancellationToken);

        handler.LastRequestUri.ShouldBe(new Uri("http://localhost:5054/api/v1/me/activity?from=2026-11-01&to=2026-11-30"));
        response.Today.ShouldBe(new DateOnly(2026, 11, 24));
        response.Days.Single().XpEarned.ShouldBe(160);
    }

    [Fact]
    public async Task GetStatsAsync_ExamId_IsSentAsQueryAndDeserializes()
    {
        var examId = Guid.NewGuid();
        using var handler = new StubHandler(
            $$$"""{"examId":"{{{examId}}}","quizzes":{"answeredCount":4,"correctCount":3,"finishedQuizCount":1,"availableTopicCount":2,"topics":[]},"practicalCases":{"completedCount":0,"averagePercent":null,"bestScores":[],"recentScores":[]}}""");
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5054") };
        var api = RestService.ForGenerated<IOpoStudyApi>(client, ApiJsonContext.Default);

        var response = await api.GetStatsAsync(examId, TestContext.Current.CancellationToken);

        handler.LastRequestUri.ShouldBe(new Uri($"http://localhost:5054/api/v1/me/stats?examId={examId}"));
        response.Quizzes.AnsweredCount.ShouldBe(4);
        response.PracticalCases.AveragePercent.ShouldBeNull();
    }

    private sealed class StubHandler(string json) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }
}