using System.Net;

using Microsoft.Extensions.Logging;

using OpoStudy.Contracts.Quizzes;
using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.Auth;

using Refit;

namespace OpoStudy.Mobile.Core.Offline;

/// <summary>Flushes the answer queue. Never logs user or quiz ids.</summary>
public sealed partial class QuizSyncService(
    IOpoStudyApi api,
    LocalQuizStore store,
    IAuthService auth,
    TimeProvider time,
    ILogger<QuizSyncService> logger) : IQuizSyncService, IDisposable
{
    private static readonly TimeSpan MaxLocalAge = TimeSpan.FromDays(30);

    private readonly SemaphoreSlim _flushLock = new(1, 1);

    public void Dispose() => _flushLock.Dispose();

    public async Task<SyncResult> FlushAsync(Guid? excludeQuizId = null, CancellationToken cancellationToken = default)
    {
        await _flushLock.WaitAsync(cancellationToken);
        try
        {
            if (auth.UserId is not { } userId)
            {
                return new SyncResult(0, 0);
            }

            var synced = 0;
            var discarded = 0;
            foreach (var quiz in await store.ListWithPendingAnswersAsync(userId, cancellationToken))
            {
                if (quiz.Id == excludeQuizId)
                {
                    continue;
                }

                var outcome = await SyncQuizAsync(quiz, cancellationToken);
                synced += outcome.Sent;
                if (outcome.Discarded)
                {
                    discarded++;
                }

                if (outcome.StopLoop)
                {
                    break;
                }
            }

            await store.DeleteFinishedOrOlderThanAsync(time.GetUtcNow() - MaxLocalAge, cancellationToken);
            return new SyncResult(synced, discarded);
        }
        finally
        {
            _flushLock.Release();
        }
    }

    private async Task<(int Sent, bool StopLoop, bool Discarded)> SyncQuizAsync(LocalQuiz quiz, CancellationToken cancellationToken)
    {
        var pending = await store.GetPendingAnswersAsync(quiz.Id, cancellationToken);
        if (pending.Count == 0)
        {
            return (0, false, false);
        }

        var request = new SyncQuizRequest(
            quiz.Id,
            quiz.ExamId,
            quiz.TopicId,
            quiz.Mode,
            quiz.StartedAt,
            quiz.QuestionIds,
            [.. pending.Select(a => new SyncAnswerRequest(a.QuestionId, a.OptionId, a.AnsweredAt))]);
        try
        {
            await api.SyncQuizAsync(request, cancellationToken);
        }
        catch (ApiException ex)
        {
            var (stop, discarded) = await HandleApiErrorAsync(ex, quiz, cancellationToken);
            return (0, stop, discarded);
        }
        catch (Exception ex) when (ConnectivityErrors.IsConnectivityError(ex, cancellationToken))
        {
            LogOffline();
            return (0, true, false);
        }

        // Finished quizzes are gone once sent; an unfinished one keeps its answers (marked as sent) so it can still be resumed.
        if (quiz.Finished)
        {
            await store.DeleteAsync(quiz.Id, cancellationToken);
        }
        else
        {
            await store.MarkSyncedAsync(quiz.Id, [.. pending.Select(a => a.QuestionId)], cancellationToken);
        }

        return (pending.Count, false, false);
    }

    /// <summary>Returns whether the loop must stop and whether the quiz was discarded. Errors that retrying cannot fix discard the local quiz.</summary>
    private async Task<(bool Stop, bool Discarded)> HandleApiErrorAsync(ApiException exception, LocalQuiz quiz, CancellationToken cancellationToken)
    {
        var status = exception.StatusCode;
        var code = ApiErrorMessages.ProblemCode(exception);
        var retryLater = status is HttpStatusCode.Unauthorized or HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
            || (int)status >= 500
            || (status == HttpStatusCode.Conflict && code is "quiz.concurrent_update" or "concurrency.conflict");
        if (retryLater)
        {
            LogRetryLater((int)status);
            return (true, false);
        }

        LogDiscarded((int)status, code);
        await store.DeleteAsync(quiz.Id, cancellationToken);
        return (false, true);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Offline sync postponed: no connection")]
    private partial void LogOffline();

    [LoggerMessage(Level = LogLevel.Information, Message = "Offline sync postponed: status {StatusCode}")]
    private partial void LogRetryLater(int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Local quiz discarded: the server rejected it (status {StatusCode}, code {Code})")]
    private partial void LogDiscarded(int statusCode, string? code);
}