namespace OpoStudy.Contracts.Quizzes;

/// <summary>Result of an answered question: only sent once the question has been answered.</summary>
public sealed record AnswerFeedback(
    Guid SelectedOptionId,
    bool IsCorrect,
    Guid CorrectOptionId,
    string Explanation,
    string LegalReference);