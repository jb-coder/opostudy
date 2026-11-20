namespace OpoStudy.Contracts.Quizzes;

/// <summary>Number of questions of a topic the user currently has wrong.</summary>
public sealed record TopicMistakeCountResponse(Guid TopicId, int Count);
