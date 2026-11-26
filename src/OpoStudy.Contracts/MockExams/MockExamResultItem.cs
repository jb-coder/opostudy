using OpoStudy.Contracts.Quizzes;

namespace OpoStudy.Contracts.MockExams;

/// <summary>Correction of one question of a submitted mock exam. <paramref name="Outcome"/> is "Correct", "Incorrect" or "Blank".</summary>
public sealed record MockExamResultItem(
    Guid QuestionId,
    int Order,
    string Statement,
    IReadOnlyList<QuizOptionResponse> Options,
    Guid? SelectedOptionId,
    Guid CorrectOptionId,
    string Outcome,
    string Explanation,
    string LegalReference);
