namespace OpoStudy.Contracts.Quizzes;

/// <summary>
/// Creates a quiz. With <paramref name="TopicId"/> it is a by-topic quiz; without it, a random one over the whole exam.
/// With <paramref name="MistakesOnly"/> only the questions the user currently has wrong are used (a review).
/// With <paramref name="DueOnly"/> only the questions due for spaced review today are used, the most overdue first;
/// it cannot be combined with <paramref name="MistakesOnly"/>.
/// With <paramref name="BookmarkedOnly"/> only the questions the user bookmarked are used; it cannot be combined
/// with <paramref name="MistakesOnly"/> or <paramref name="DueOnly"/>.
/// </summary>
public sealed record CreateQuizRequest(Guid ExamId, Guid? TopicId, int QuestionCount, bool MistakesOnly = false, bool DueOnly = false, bool BookmarkedOnly = false);
