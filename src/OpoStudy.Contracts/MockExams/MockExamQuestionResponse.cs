using OpoStudy.Contracts.Quizzes;

namespace OpoStudy.Contracts.MockExams;

/// <summary>A question of a mock exam. It never reveals the correct option.</summary>
public sealed record MockExamQuestionResponse(Guid QuestionId, int Order, string Statement, IReadOnlyList<QuizOptionResponse> Options);
