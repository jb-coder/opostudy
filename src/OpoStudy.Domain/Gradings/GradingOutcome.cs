namespace OpoStudy.Domain.Gradings;

/// <summary>Points awarded to each criterion of one part (in the order of the criteria) and the feedback of the part.</summary>
public sealed record PartOutcome(IReadOnlyList<decimal> Awarded, string Feedback);

/// <summary>The result of correcting a case, as produced by a corrector and validated by <see cref="Grading.Complete"/>.</summary>
public sealed record GradingOutcome(
    IReadOnlyList<PartOutcome> Parts,
    string Feedback,
    string Corrector,
    int? InputTokens,
    int? OutputTokens);
