namespace OpoStudy.Domain.Gradings;

/// <summary>
/// A copy of a rubric criterion inside a <see cref="GradingAnswer"/>, with the points awarded once corrected.
/// The copy keeps the grading readable even if the content is edited later.
/// </summary>
/// <param name="Description">What the answer must contain.</param>
/// <param name="Points">Maximum points of the criterion.</param>
/// <param name="Awarded">Points awarded (a multiple of 0.5); <c>null</c> until the grading is completed.</param>
public sealed record CriterionScore(string Description, int Points, decimal? Awarded);
