namespace OpoStudy.Contracts.Exams;

/// <summary>An active exam (oposición) the user can prepare.</summary>
public sealed record ExamResponse(Guid Id, string Slug, string Name, string Organization, string Group);