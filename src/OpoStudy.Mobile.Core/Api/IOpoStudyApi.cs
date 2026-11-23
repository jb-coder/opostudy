using OpoStudy.Contracts.Exams;
using OpoStudy.Contracts.Offline;
using OpoStudy.Contracts.Progress;
using OpoStudy.Contracts.Quizzes;
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
}
