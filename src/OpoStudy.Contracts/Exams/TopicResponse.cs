namespace OpoStudy.Contracts.Exams;

/// <summary>A topic of an exam with the number of active questions available.</summary>
public sealed record TopicResponse(Guid Id, int Number, string Title, string? Section, int QuestionCount);