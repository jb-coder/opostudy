namespace OpoStudy.Contracts.Gradings;

/// <summary>The answers of the user to a practical case: one per part, in the order of the parts. A part may be left blank.</summary>
public sealed record SubmitGradingRequest(IReadOnlyList<string> Answers);
