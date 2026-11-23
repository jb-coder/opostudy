namespace OpoStudy.Mobile.Core.PracticalCases;

/// <summary>The unsent answers of a practical case, one per part in the order of the parts, stored on the device.</summary>
/// <param name="Answers">Answers by part (blank ones as empty strings).</param>
/// <param name="SavedAt">When the draft was saved.</param>
public sealed record PracticalCaseDraft(IReadOnlyList<string> Answers, DateTimeOffset SavedAt);