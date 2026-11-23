using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpoStudy.Domain.Common;
using OpoStudy.Domain.Gradings;
using OpoStudy.Domain.PracticalCases;
using OpoStudy.Infrastructure.Persistence;

namespace OpoStudy.Infrastructure.Gradings;

/// <summary>Stable codes stored in <see cref="Grading.FailureCode"/> when the queue gives up.</summary>
public static class GradingFailureCodes
{
    public const string InvalidResult = "grading.invalid_result";
    public const string ProviderUnavailable = "grading.provider_unavailable";
    public const string ProviderError = "grading.provider_error";
}

/// <summary>
/// Processes one element of the correction queue. It works in three steps so no transaction stays open while the corrector runs:
/// claim (short transaction with <c>FOR UPDATE SKIP LOCKED</c>), correct, and save the result.
/// Never log the answers or anything that identifies the user.
/// </summary>
public sealed partial class GradingProcessor(
    OpoStudyDbContext db,
    ILlmCorrector corrector,
    IOptions<GradingOptions> options,
    TimeProvider timeProvider,
    ILogger<GradingProcessor> logger)
{
    private readonly GradingOptions _options = options.Value;

    /// <summary>Takes the next due grading and processes it. Returns <c>false</c> when there was nothing to do.</summary>
    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        var grading = await ClaimAsync(cancellationToken);
        if (grading is null)
        {
            return false;
        }

        var startedAt = timeProvider.GetTimestamp();
        var request = await BuildRequestAsync(grading, cancellationToken);

        try
        {
            var outcome = await GradeWithTimeoutAsync(request, cancellationToken);
            Complete(grading, outcome);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutting down: leave it in Processing, its lease will expire and it will be claimed again.
            throw;
        }
        catch (Exception ex) when (ex is LlmTransientException or OperationCanceledException)
        {
            // OperationCanceledException without a shutdown is the timeout of the corrector.
            RetryOrFail(grading, ex);
        }
#pragma warning disable CA1031 // Any other corrector error is final: it must not stop the queue, and the grading records the failure.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogProviderError(logger, grading.Id, ex.GetType().Name);
            grading.Fail(GradingFailureCodes.ProviderError, timeProvider.GetUtcNow());
        }

        await db.SaveChangesAsync(cancellationToken);
        var durationMs = (long)timeProvider.GetElapsedTime(startedAt).TotalMilliseconds;
        LogProcessed(logger, grading.Id, grading.Status, grading.Attempts, durationMs);
        return true;
    }

    private async Task<Grading?> ClaimAsync(CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async token =>
        {
            db.ChangeTracker.Clear();
            var now = timeProvider.GetUtcNow();
            var leaseCutoff = now - _options.LeaseTimeout;

            await using var transaction = await db.Database.BeginTransactionAsync(token);
            var ids = await db.Database
                .SqlQuery<Guid>($"""
                    SELECT id AS "Value" FROM gradings
                    WHERE (status = 'Pending' AND next_attempt_at <= {now})
                       OR (status = 'Processing' AND started_at < {leaseCutoff})
                    ORDER BY submitted_at
                    LIMIT 1
                    FOR UPDATE SKIP LOCKED
                    """)
                .ToListAsync(token);
            if (ids.Count == 0)
            {
                return null;
            }

            var grading = await db.Gradings.SingleAsync(g => g.Id == ids[0], token);
            grading.Start(now);
            await db.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
            return grading;
        }, cancellationToken);
    }

    private async Task<GradingRequest> BuildRequestAsync(Grading grading, CancellationToken cancellationToken)
    {
        // The current statements and model answers; the rubric is the copy stored in the grading.
        // An inactive case is corrected anyway: the submission was already accepted.
        var practicalCase = await db.PracticalCases
            .AsNoTracking()
            .SingleAsync(c => c.Id == grading.PracticalCaseId, cancellationToken);
        var parts = practicalCase.Parts;
        var answers = grading.Answers;

        var partRequests = answers
            .Select(a => new GradingPartRequest(
                parts[a.Order].Statement,
                parts[a.Order].ModelAnswer,
                [.. a.Criteria.Select(c => new GradingCriterion(c.Description, c.Points))],
                a.Text))
            .ToList();
        return new GradingRequest(practicalCase.Statement, partRequests);
    }

    private async Task<GradingOutcome> GradeWithTimeoutAsync(GradingRequest request, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.CorrectorTimeout);
        return await corrector.GradeAsync(request, timeout.Token);
    }

    private void Complete(Grading grading, GradingOutcome outcome)
    {
        try
        {
            grading.Complete(outcome, timeProvider.GetUtcNow());
        }
        catch (DomainRuleException ex) when (ex.Code == GradingFailureCodes.InvalidResult)
        {
            grading.Fail(GradingFailureCodes.InvalidResult, timeProvider.GetUtcNow());
        }
    }

    private void RetryOrFail(Grading grading, Exception exception)
    {
        var now = timeProvider.GetUtcNow();
        if (grading.Attempts < _options.MaxAttempts)
        {
            LogTransientError(logger, grading.Id, exception.GetType().Name, grading.Attempts);
            grading.Retry(now, _options.RetryDelayAfter(grading.Attempts));
        }
        else
        {
            grading.Fail(GradingFailureCodes.ProviderUnavailable, now);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Grading {GradingId} left in status {Status} after {Attempts} attempt(s), {DurationMs} ms")]
    private static partial void LogProcessed(ILogger logger, Guid gradingId, GradingStatus status, int attempts, long durationMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Grading {GradingId}: transient corrector error ({ErrorType}) at attempt {Attempts}")]
    private static partial void LogTransientError(ILogger logger, Guid gradingId, string errorType, int attempts);

    [LoggerMessage(Level = LogLevel.Error, Message = "Grading {GradingId}: the corrector failed ({ErrorType})")]
    private static partial void LogProviderError(ILogger logger, Guid gradingId, string errorType);
}