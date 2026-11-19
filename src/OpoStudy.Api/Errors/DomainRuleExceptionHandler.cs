using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

using OpoStudy.Domain.Common;

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

        var code = exception switch
        {
            DomainRuleException rule => rule.Code,
            DbUpdateConcurrencyException => DomainErrors.QuizConcurrentUpdate,
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