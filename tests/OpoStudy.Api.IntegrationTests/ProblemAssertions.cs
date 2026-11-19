using System.Net;
using System.Net.Http.Json;

using Shouldly;

namespace OpoStudy.Api.IntegrationTests;

/// <summary>The parts of a ProblemDetails the tests check; <c>Code</c> is the stable extension.</summary>
internal sealed record ProblemBody(string? Title, int? Status, string? Code);

internal static class ProblemAssertions
{
    /// <summary>Checks a ProblemDetails response: status, content type, <c>code</c>, and that the title does not leak the code.</summary>
    public static async Task ShouldBeProblemAsync(this HttpResponseMessage response, HttpStatusCode status, string code)
    {
        ArgumentNullException.ThrowIfNull(response);

        response.StatusCode.ShouldBe(status);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemBody>(TestContext.Current.CancellationToken);
        problem.ShouldNotBeNull();
        problem.Status.ShouldBe((int)status);
        problem.Code.ShouldBe(code);
        problem.Title.ShouldNotBeNullOrWhiteSpace();
        problem.Title.ShouldNotContain(code);
        problem.Title.ShouldNotContain("quiz.");
    }
}