namespace OpoStudy.Contracts.Stats;

/// <summary>Statistics of the user for one exam.</summary>
public sealed record StatsResponse(Guid ExamId, QuizStatsResponse Quizzes, PracticalCaseStatsResponse PracticalCases);

/// <summary>Quiz statistics. <paramref name="Topics"/> only has the topics with some answer, weakest first.</summary>
/// <param name="AnsweredCount">Answered questions, repeated ones and reviews included.</param>
/// <param name="CorrectCount">Correct answers among <paramref name="AnsweredCount"/>.</param>
/// <param name="FinishedQuizCount">Quizzes of the exam that have a finish time, of any mode.</param>
/// <param name="AvailableTopicCount">Topics of the exam with at least one active question.</param>
/// <param name="Topics">Per-topic counts; their sum equals the totals.</param>
public sealed record QuizStatsResponse(
    int AnsweredCount,
    int CorrectCount,
    int FinishedQuizCount,
    int AvailableTopicCount,
    IReadOnlyList<TopicStatsResponse> Topics);

/// <summary>Answers of the user on the questions of one topic.</summary>
/// <param name="AvailableQuestionCount">Active questions of the topic (0 = it cannot be practiced).</param>
public sealed record TopicStatsResponse(
    Guid TopicId,
    int Number,
    string Title,
    string? Section,
    int AnsweredCount,
    int CorrectCount,
    DateTimeOffset LastAnsweredAt,
    int AvailableQuestionCount);

/// <summary>Statistics of the completed gradings of the exam's practical cases.</summary>
/// <param name="AveragePercent">Average of the percentages of the gradings, or <c>null</c> if there are none.</param>
/// <param name="BestScores">One per practical case, ordered by title.</param>
/// <param name="RecentScores">The last gradings, oldest first.</param>
public sealed record PracticalCaseStatsResponse(
    int CompletedCount,
    decimal? AveragePercent,
    IReadOnlyList<CaseBestScoreResponse> BestScores,
    IReadOnlyList<CaseScoreResponse> RecentScores);

/// <summary>Best grading of a practical case: the greatest percentage; on a tie, the most recent.</summary>
public sealed record CaseBestScoreResponse(
    Guid PracticalCaseId,
    string CaseTitle,
    string? Section,
    int GradingCount,
    Guid BestGradingId,
    decimal Score,
    int MaxPoints,
    decimal Percent);

/// <summary>Score of one completed grading.</summary>
public sealed record CaseScoreResponse(
    Guid GradingId,
    string CaseTitle,
    DateTimeOffset SubmittedAt,
    decimal Score,
    int MaxPoints,
    decimal Percent);