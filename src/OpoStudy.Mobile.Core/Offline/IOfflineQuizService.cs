using OpoStudy.Contracts.Quizzes;

namespace OpoStudy.Mobile.Core.Offline;

/// <summary>
/// Quizzes taken without the server: they are corrected with the cached content and the answers wait in a queue.
/// Every quiz belongs to the signed-in user (<c>IAuthService.UserId</c>); without a session nothing is read.
/// </summary>
public interface IOfflineQuizService
{
    /// <summary>
    /// Creates a quiz with random cached questions of the topic (or of the whole exam when <paramref name="topicId"/> is null),
    /// never more than the available ones. Throws <see cref="OfflineQuizException"/> when there are none.
    /// </summary>
    Task<QuizResponse> CreateAsync(Guid examId, Guid? topicId, int questionCount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores an online quiz that lost its connection, with the answers already given. Those answers are already on the
    /// server, so they are never queued. Adopting a quiz twice does nothing.
    /// </summary>
    Task AdoptAsync(QuizResponse quiz, string title, CancellationToken cancellationToken = default);

    /// <summary>The local quiz with the feedback of the answered questions, or <c>null</c> when it is unknown or cannot be built from the cache.</summary>
    Task<QuizResponse?> GetAsync(Guid quizId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Corrects the answer with the cache and queues it. Throws <see cref="InvalidOperationException"/> when the quiz or
    /// the question is unknown, the question is not in the cache or it was already answered.
    /// </summary>
    Task<OfflineAnswerResult> AnswerAsync(Guid quizId, Guid questionId, Guid optionId, CancellationToken cancellationToken = default);

    /// <summary>The most recent unfinished local quiz of the current user, or <c>null</c>.</summary>
    Task<LocalQuiz?> GetLatestUnfinishedAsync(CancellationToken cancellationToken = default);

    /// <summary>Answers of the current user waiting to be sent.</summary>
    Task<int> PendingAnswerCountAsync(CancellationToken cancellationToken = default);

    /// <summary>Deletes the local quizzes and answers of a user (when the account is deleted).</summary>
    Task DeleteUserDataAsync(string userId, CancellationToken cancellationToken = default);
}