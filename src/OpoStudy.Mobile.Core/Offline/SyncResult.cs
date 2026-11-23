namespace OpoStudy.Mobile.Core.Offline;

/// <summary>Outcome of flushing the answer queue.</summary>
/// <param name="SyncedAnswers">Answers handed to the server and removed from the queue.</param>
/// <param name="DiscardedQuizzes">Local quizzes dropped because the server rejected them for good (non-retryable 4xx).</param>
public sealed record SyncResult(int SyncedAnswers, int DiscardedQuizzes);
