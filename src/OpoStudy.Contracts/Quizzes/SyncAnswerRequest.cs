namespace OpoStudy.Contracts.Quizzes;

/// <summary>An answer given offline, with the time the user gave it according to the device.</summary>
public sealed record SyncAnswerRequest(Guid QuestionId, Guid OptionId, DateTimeOffset AnsweredAt);
