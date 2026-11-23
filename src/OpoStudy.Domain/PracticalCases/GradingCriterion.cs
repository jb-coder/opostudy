namespace OpoStudy.Domain.PracticalCases;

/// <summary>A rubric line of a <see cref="PracticalCasePart"/>: what the answer must contain and how many points it is worth.</summary>
public sealed record GradingCriterion(string Description, int Points);

/// <summary>Input data of a <see cref="GradingCriterion"/>.</summary>
public sealed record GradingCriterionData(string Description, int Points);

/// <summary>Input data of a <see cref="PracticalCasePart"/>. Its order is its position in the list.</summary>
public sealed record PracticalCasePartData(string Statement, string ModelAnswer, IReadOnlyList<GradingCriterionData> Criteria);