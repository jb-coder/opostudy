using OpoStudy.Contracts.Exams;
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

    [Post("/api/v1/quizzes")]
    Task<QuizResponse> CreateQuizAsync([Body] CreateQuizRequest request, CancellationToken cancellationToken = default);

    [Get("/api/v1/quizzes/{quizId}")]
    Task<QuizResponse> GetQuizAsync(Guid quizId, CancellationToken cancellationToken = default);

    [Post("/api/v1/quizzes/{quizId}/answers")]
    Task<AnswerFeedbackResponse> SubmitAnswerAsync(Guid quizId, [Body] SubmitAnswerRequest request, CancellationToken cancellationToken = default);
}
