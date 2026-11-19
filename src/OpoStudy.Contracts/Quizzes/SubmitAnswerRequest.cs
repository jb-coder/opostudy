namespace OpoStudy.Contracts.Quizzes;

/// <summary>Answers a question of a quiz with one of its options.</summary>
public sealed record SubmitAnswerRequest(Guid QuestionId, Guid OptionId);