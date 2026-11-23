using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using OpoStudy.Domain.PracticalCases;
using OpoStudy.Infrastructure.Gradings;
using OpoStudy.Infrastructure.Gradings.Gemini;

using Shouldly;

namespace OpoStudy.Infrastructure.Tests.Gradings.Gemini;

/// <summary>Calls the real Gemini API. Skipped unless the environment variable <c>OPOSTUDY_GEMINI_API_KEY</c> exists, so it never runs in CI.</summary>
public sealed class GeminiLiveTests
{
    private static readonly GradingCriterion[] Criteria =
    [
        new("Uses a JOIN between the customers and orders tables", 2),
        new("Groups by customer and counts the orders", 2),
        new("Orders the result by the number of orders, descending", 1),
    ];

    private static GradingRequest Request(string answer) =>
        new(
            "A database has the tables customers(id, name) and orders(id, customer_id, total).",
            [new GradingPartRequest(
                "Write a SQL query that returns each customer name with the number of orders, from the most to the fewest.",
                "SELECT c.name, COUNT(o.id) AS orders FROM customers c JOIN orders o ON o.customer_id = c.id GROUP BY c.name ORDER BY orders DESC;",
                Criteria,
                answer)]);

    [Fact]
    public async Task GradeAsync_WithGoodAndBlankAnswers_ScoresTheGoodOneHigher()
    {
        var key = Environment.GetEnvironmentVariable("OPOSTUDY_GEMINI_API_KEY");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(key), "OPOSTUDY_GEMINI_API_KEY is not set.");

        var options = new GeminiOptions { ApiKey = key };
        using var http = new HttpClient { BaseAddress = new Uri(options.BaseUrl), Timeout = TimeSpan.FromMinutes(2) };
        var corrector = new GeminiLlmCorrector(http, Options.Create(options), NullLogger<GeminiLlmCorrector>.Instance);
        var ct = TestContext.Current.CancellationToken;

        var good = await corrector.GradeAsync(
            Request("SELECT c.name, COUNT(o.id) AS orders FROM customers c JOIN orders o ON o.customer_id = c.id GROUP BY c.name ORDER BY orders DESC;"),
            ct);
        var blank = await corrector.GradeAsync(Request(string.Empty), ct);

        good.Parts.Single().Awarded.Sum().ShouldBeGreaterThan(blank.Parts.Single().Awarded.Sum());
        good.Corrector.ShouldStartWith("gemini:");
    }
}