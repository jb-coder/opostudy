namespace OpoStudy.Contracts.Quizzes;

/// <summary>An answer option. It never says whether it is the correct one.</summary>
public sealed record QuizOptionResponse(Guid Id, int Order, string Text);