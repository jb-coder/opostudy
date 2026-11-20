namespace OpoStudy.Mobile.Core.Offline;

/// <summary>Sends the answers given offline to the server.</summary>
public interface IQuizSyncService
{
    /// <summary>
    /// Sends the pending answers of the current user, quiz by quiz (oldest first). Only one flush runs at a time:
    /// a second call waits for the first and then finds nothing left to send.
    /// </summary>
    /// <param name="excludeQuizId">
    /// Quiz that is being played right now: it is skipped so its answers are not sent in the middle of the quiz.
    /// Its answers go out on the next flush, when the user is no longer playing it.
    /// </param>
    /// <param name="cancellationToken">Cancels the flush; it never counts as a connectivity error.</param>
    /// <returns>How many answers reached the server and how many local quizzes were discarded because the server rejected them.</returns>
    Task<SyncResult> FlushAsync(Guid? excludeQuizId = null, CancellationToken cancellationToken = default);
}