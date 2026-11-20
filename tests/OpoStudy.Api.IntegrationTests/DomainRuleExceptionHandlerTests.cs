using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

using OpoStudy.Api.Errors;
using OpoStudy.Domain.Common;
using OpoStudy.Domain.Quizzes;
using OpoStudy.Domain.Users;
using OpoStudy.Infrastructure.Persistence;

using Shouldly;

namespace OpoStudy.Api.IntegrationTests;

public sealed class DomainRuleExceptionHandlerTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TryHandleAsync_ConcurrencyConflictOnQuizAttempt_Returns409QuizConcurrentUpdate()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpoStudyDbContext>();
        var attempt = QuizAttempt.Create(Guid.NewGuid(), Guid.NewGuid(), null, QuizMode.Random, [Guid.NewGuid()], DateTimeOffset.UtcNow);
        var exception = ConflictOn(db, attempt);

        var (status, problem) = await HandleAsync(exception);

        status.ShouldBe(HttpStatusCode.Conflict);
        problem.GetProperty("code").GetString().ShouldBe("quiz.concurrent_update");
    }

    [Fact]
    public async Task TryHandleAsync_ConcurrencyConflictOnOtherEntity_Returns409ConcurrencyConflict()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpoStudyDbContext>();
        var user = User.Create($"uid-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        var exception = ConflictOn(db, user);

        var (status, problem) = await HandleAsync(exception);

        status.ShouldBe(HttpStatusCode.Conflict);
        problem.GetProperty("code").GetString().ShouldBe("concurrency.conflict");
        problem.GetProperty("title").GetString().ShouldBe("Los datos se han modificado desde otra petición. Vuelve a intentarlo.");
    }

    [Fact]
    public async Task TryHandleAsync_UnknownDomainRuleCode_Returns422WithFallbackMessage()
    {
        var exception = new DomainRuleException("some.unknown_code", "internal detail");

        var (status, problem) = await HandleAsync(exception);

        status.ShouldBe((HttpStatusCode)422);
        problem.GetProperty("code").GetString().ShouldBe("some.unknown_code");
        var title = problem.GetProperty("title").GetString();
        title.ShouldBe("No se ha podido completar la operación.");
        title.ShouldNotBeNull();
        title.ShouldNotContain("some.unknown_code");
        title.ShouldNotContain("internal detail");
    }

    [Fact]
    public async Task TryHandleAsync_UnrelatedException_ReturnsFalse()
    {
        var context = NewContext();

        var handled = await new DomainRuleExceptionHandler().TryHandleAsync(context, new InvalidOperationException(), Ct);

        handled.ShouldBeFalse();
    }

    // The only public way to build a DbUpdateConcurrencyException with entries takes IUpdateEntry, which EF exposes
    // only through the internal-API GetInfrastructure(); there is no DB round-trip alternative for a deterministic test.
    private static DbUpdateConcurrencyException ConflictOn(OpoStudyDbContext db, object entity)
    {
#pragma warning disable EF1001 // Internal EF Core API, justified above.
        return new DbUpdateConcurrencyException("conflict", [db.Entry(entity).GetInfrastructure()]);
#pragma warning restore EF1001
    }

    private static DefaultHttpContext NewContext()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails();
        return new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
    }

    private static async Task<(HttpStatusCode Status, JsonElement Problem)> HandleAsync(Exception exception)
    {
        var context = NewContext();
        using var body = new MemoryStream();
        context.Response.Body = body;

        var handled = await new DomainRuleExceptionHandler().TryHandleAsync(context, exception, Ct);

        handled.ShouldBeTrue();
        body.Position = 0;
        var problem = await JsonSerializer.DeserializeAsync<JsonElement>(body, cancellationToken: Ct);
        return ((HttpStatusCode)context.Response.StatusCode, problem);
    }
}