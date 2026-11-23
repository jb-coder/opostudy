namespace OpoStudy.Contracts.Quizzes;

/// <summary>Questions the user currently has wrong in an exam: the total and the count per topic (only topics with mistakes, by topic number).</summary>
public sealed record MistakeSummaryResponse(int Total, IReadOnlyList<TopicMistakeCountResponse> Topics);
