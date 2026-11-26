namespace OpoStudy.Contracts.Questions;

/// <summary>
/// Reports a question the user thinks is wrong. <paramref name="Reason"/> is one of <c>"WrongAnswer"</c>,
/// <c>"OutdatedLegislation"</c>, <c>"UnclearStatement"</c>, <c>"Typo"</c> or <c>"Other"</c>.
/// <paramref name="Comment"/> is optional (up to 500 characters) except with <c>"Other"</c>.
/// </summary>
public sealed record CreateQuestionReportRequest(string Reason, string? Comment);