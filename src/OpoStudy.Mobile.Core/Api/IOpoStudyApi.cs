using OpoStudy.Contracts.Exams;
using OpoStudy.Contracts.Gradings;
using OpoStudy.Contracts.MockExams;
using OpoStudy.Contracts.Offline;
using OpoStudy.Contracts.PracticalCases;
using OpoStudy.Contracts.Progress;
using OpoStudy.Contracts.Quizzes;
using OpoStudy.Contracts.Reviews;
using OpoStudy.Contracts.Stats;
using OpoStudy.Contracts.SystemInfo;
using OpoStudy.Contracts.Users;

using Refit;

namespace OpoStudy.Mobile.Core.Api;

/// <summary>Typed client for the OpoStudy API.</summary>
public interface IOpoStudyApi
{
    [Get("/api/v1/version")]
    Task<VersionResponse> GetVersionAsync(CancellationToken cancellationToken = default);

    [Get("/api/v1/me")]
    Task<UserProfileResponse> GetMeAsync(CancellationToken cancellationToken = default);

    [Delete("/api/v1/me")]
    Task DeleteMeAsync(CancellationToken cancellationToken = default);

    [Get("/api/v1/exams")]
    Task<IReadOnlyList<ExamResponse>> GetExamsAsync(CancellationToken cancellationToken = default);

    [Get("/api/v1/exams/{examId}/topics")]
    Task<IReadOnlyList<TopicResponse>> GetTopicsAsync(Guid examId, CancellationToken cancellationToken = default);

    [Get("/api/v1/exams/{examId}/offline-content")]
    Task<IApiResponse<OfflineContentResponse>> GetOfflineContentAsync(
        Guid examId,
        [Header("If-None-Match")] string? ifNoneMatch,
        CancellationToken cancellationToken = default);

    [Get("/api/v1/exams/{examId}/mistakes")]
    Task<MistakeSummaryResponse> GetMistakesAsync(Guid examId, CancellationToken cancellationToken = default);

    [Get("/api/v1/reviews/due")]
    Task<DueReviewSummaryResponse> GetDueReviewsAsync(CancellationToken cancellationToken = default);

    [Get("/api/v1/quizzes")]
    Task<IReadOnlyList<QuizSummaryResponse>> GetQuizzesAsync(int skip, int take, CancellationToken cancellationToken = default);

    [Post("/api/v1/quizzes")]
    Task<QuizResponse> CreateQuizAsync([Body] CreateQuizRequest request, CancellationToken cancellationToken = default);

    [Get("/api/v1/quizzes/{quizId}")]
    Task<QuizResponse> GetQuizAsync(Guid quizId, CancellationToken cancellationToken = default);

    [Post("/api/v1/quizzes/{quizId}/answers")]
    Task<AnswerFeedbackResponse> SubmitAnswerAsync(Guid quizId, [Body] SubmitAnswerRequest request, CancellationToken cancellationToken = default);

    [Post("/api/v1/quizzes/sync")]
    Task<SyncQuizResponse> SyncQuizAsync([Body] SyncQuizRequest request, CancellationToken cancellationToken = default);

    [Get("/api/v1/exams/{examId}/exercises")]
    Task<IReadOnlyList<ExamExerciseResponse>> GetExercisesAsync(Guid examId, CancellationToken cancellationToken = default);

    [Post("/api/v1/mock-exams")]
    Task<MockExamResponse> CreateMockExamAsync([Body] CreateMockExamRequest request, CancellationToken cancellationToken = default);

    [Post("/api/v1/mock-exams/{id}/submission")]
    Task<SubmitMockExamResponse> SubmitMockExamAsync(Guid id, [Body] SubmitMockExamRequest request, CancellationToken cancellationToken = default);

    [Get("/api/v1/mock-exams/{id}/result")]
    Task<MockExamResultResponse> GetMockExamResultAsync(Guid id, CancellationToken cancellationToken = default);

    [Get("/api/v1/me/streak")]
    Task<StreakResponse> GetStreakAsync(CancellationToken cancellationToken = default);

    [Get("/api/v1/me/level")]
    Task<LevelResponse> GetLevelAsync(CancellationToken cancellationToken = default);

    [Post("/api/v1/me/streak-freezes")]
    Task<StreakResponse> BuyStreakFreezeAsync(CancellationToken cancellationToken = default);

    [Put("/api/v1/me/daily-goal")]
    Task<StreakResponse> UpdateDailyGoalAsync([Body] UpdateDailyGoalRequest request, CancellationToken cancellationToken = default);

    [Put("/api/v1/me/time-zone")]
    Task UpdateTimeZoneAsync([Body] UpdateTimeZoneRequest request, CancellationToken cancellationToken = default);

    [Get("/api/v1/exams/{examId}/practical-cases")]
    Task<IReadOnlyList<PracticalCaseSummaryResponse>> GetPracticalCasesAsync(Guid examId, CancellationToken cancellationToken = default);

    [Get("/api/v1/practical-cases/{id}")]
    Task<PracticalCaseResponse> GetPracticalCaseAsync(Guid id, CancellationToken cancellationToken = default);

    [Post("/api/v1/practical-cases/{id}/gradings")]
    Task<GradingResponse> SubmitGradingAsync(Guid id, [Body] SubmitGradingRequest request, CancellationToken cancellationToken = default);

    [Get("/api/v1/gradings/{id}")]
    Task<GradingResponse> GetGradingAsync(Guid id, CancellationToken cancellationToken = default);

    [Get("/api/v1/gradings")]
    Task<IReadOnlyList<GradingSummaryResponse>> GetGradingsAsync(int skip, int take, CancellationToken cancellationToken = default);

    [Get("/api/v1/me/grading-quota")]
    Task<GradingQuotaResponse> GetGradingQuotaAsync(CancellationToken cancellationToken = default);

    [Get("/api/v1/me/stats")]
    Task<StatsResponse> GetStatsAsync(Guid examId, CancellationToken cancellationToken = default);

    /// <summary>Days with activity from <paramref name="startDate"/> to <paramref name="endDate"/>, both inclusive. Dates go as ISO (<c>yyyy-MM-dd</c>): the Refit default is not ISO.</summary>
    [Get("/api/v1/me/activity")]
    Task<ActivityResponse> GetActivityAsync(
        [Query(Format = "yyyy-MM-dd"), AliasAs("from")] DateOnly startDate,
        [Query(Format = "yyyy-MM-dd"), AliasAs("to")] DateOnly endDate,
        CancellationToken cancellationToken = default);
}
