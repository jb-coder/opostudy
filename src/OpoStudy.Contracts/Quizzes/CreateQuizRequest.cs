namespace OpoStudy.Contracts.Quizzes;

/// <summary>
/// Creates a quiz. With <paramref name="TopicId"/> it is a by-topic quiz; without it, a random one over the whole exam.
/// With <paramref name="MistakesOnly"/> only the questions the user currently has wrong are used (a review).
/// </summary>
public sealed record CreateQuizRequest(Guid ExamId, Guid? TopicId, int QuestionCount, bool MistakesOnly = false);
