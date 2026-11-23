namespace OpoStudy.Contracts.Quizzes;

/// <summary>Result of syncing a quiz.</summary>
/// <param name="QuizId">Id of the quiz.</param>
/// <param name="AcceptedCount">Answers applied now.</param>
/// <param name="DuplicateCount">Answers the server already had for those questions; ignored.</param>
/// <param name="RejectedCount">Answers for a question that is not in the quiz or an option that is not the question's; ignored.</param>
/// <param name="XpEarned">XP granted now, daily-goal bonuses included.</param>
/// <param name="QuizFinished">Whether every question of the quiz is answered.</param>
public sealed record SyncQuizResponse(Guid QuizId, int AcceptedCount, int DuplicateCount, int RejectedCount, int XpEarned, bool QuizFinished);
