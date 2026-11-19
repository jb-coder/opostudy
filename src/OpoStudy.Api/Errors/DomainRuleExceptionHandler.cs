using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

using OpoStudy.Domain.Common;
using OpoStudy.Domain.Quizzes;

namespace OpoStudy.Api.Errors;

/// <summary>
/// Turns domain rule violations and optimistic concurrency conflicts into ProblemDetails. It never uses
/// the exception message: the text comes from <see cref="DomainErrors"/>.
/// </summary>
internal sealed class DomainRuleExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        // Malformed or missing request body: Minimal APIs throw this and the exception handler middleware would turn it into a 500.
        if (exception is BadHttpRequestException badRequest)
        {
            var status = badRequest.StatusCode is >= 400 and < 500 ? badRequest.StatusCode : StatusCodes.Status400BadRequest;
            await TypedResults.Problem(
                title: "La petición no es válida.",
                statusCode: status,
                extensions: new Dictionary<string, object?> { ["code"] = DomainErrors.InvalidRequest }).ExecuteAsync(httpContext);
            return true;
        }

        var code = exception switch
        {
            DomainRuleException rule => rule.Code,
            DbUpdateConcurrencyException concurrency => concurrency.Entries.Any(e => e.Entity is QuizAttempt)
                ? DomainErrors.QuizConcurrentUpdate
                : DomainErrors.ConcurrencyConflict,
            _ => null,
        };
        if (code is null)
        {
            return false;
        }

        await DomainErrors.Problem(code).ExecuteAsync(httpContext);
        return true;
    }
}