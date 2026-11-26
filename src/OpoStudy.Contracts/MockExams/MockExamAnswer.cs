namespace OpoStudy.Contracts.MockExams;

/// <summary>The option chosen for a question. Questions left blank are not sent.</summary>
public sealed record MockExamAnswer(Guid QuestionId, Guid OptionId);
