using System.Net;
using System.Text;

using OpoStudy.Contracts.Gradings;
using OpoStudy.Contracts.PracticalCases;
using OpoStudy.Mobile.Core.PracticalCases;

using Refit;

namespace OpoStudy.Mobile.Core.Tests.PracticalCases;

/// <summary>In-memory <see cref="IPracticalCaseDraftStore"/>.</summary>
internal sealed class FakeDraftStore : IPracticalCaseDraftStore
{
    public Dictionary<Guid, PracticalCaseDraft> Drafts { get; } = [];

    public int Saves { get; private set; }

    public PracticalCaseDraft? Find(Guid practicalCaseId) => Drafts.GetValueOrDefault(practicalCaseId);

    public void Save(Guid practicalCaseId, PracticalCaseDraft draft)
    {
        Saves++;
        Drafts[practicalCaseId] = draft;
    }

    public void Delete(Guid practicalCaseId) => Drafts.Remove(practicalCaseId);
}

internal static class PracticalCaseTestData
{
    public static readonly Guid CaseId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    public static readonly Guid GradingId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");

    public static PracticalCaseResponse Case(int parts = 2) => new(
        CaseId,
        Guid.NewGuid(),
        "Supuesto 1",
        "Bloque A",
        "Enunciado",
        45,
        25,
        [.. Enumerable.Range(0, parts).Select(i => new PracticalCasePartResponse(i, $"Apartado {i}", 10))]);

    public static PracticalCaseSummaryResponse Summary(string title, string? section) => new(Guid.NewGuid(), title, section, 3, 25, 45);

    public static GradingQuotaResponse Quota(int remaining = 3, int limit = 5) =>
        new(limit, limit - remaining, remaining, new DateTimeOffset(2026, 12, 1, 12, 0, 0, TimeSpan.Zero));

    public static GradingSummaryResponse SummaryGrading(string status) =>
        new(GradingId, CaseId, "Supuesto 1", "Bloque A", status, DateTimeOffset.UtcNow, 25, null);

    public static GradingResponse Grading(string status, int attempts = 0, decimal? score = null, string? failureCode = null, int? xpEarned = null, bool metDailyGoal = false) => new(
        GradingId,
        CaseId,
        "Supuesto 1",
        status,
        attempts,
        DateTimeOffset.UtcNow,
        null,
        25,
        score,
        null,
        failureCode,
        [new GradingAnswerResponse(1, "B", "segunda", 10, null, null, null, null), new GradingAnswerResponse(0, "A", "primera", 15, null, null, null, null)],
        xpEarned,
        metDailyGoal);

    public static async Task<ApiException> ApiExceptionAsync(HttpStatusCode status, string? code = null, string? detail = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/api/v1/x");
        using var response = new HttpResponseMessage(status);
        if (code is not null)
        {
            response.Content = new StringContent($$"""{"code":"{{code}}","title":"x","detail":"{{detail ?? "detalle"}}"}""", Encoding.UTF8, "application/problem+json");
        }

        return await ApiException.Create(request, HttpMethod.Post, response, new RefitSettings());
    }

    /// <summary>Waits (in real time, briefly) for work that continues on the thread pool after a fake clock advance.</summary>
    public static async Task EventuallyAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The condition was not met in time.");
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }
}