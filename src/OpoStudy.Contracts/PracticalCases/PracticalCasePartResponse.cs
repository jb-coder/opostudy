namespace OpoStudy.Contracts.PracticalCases;

/// <summary>A part of a practical case; <paramref name="Order"/> is its position (0-based).</summary>
public sealed record PracticalCasePartResponse(int Order, string Statement, int MaxPoints);
