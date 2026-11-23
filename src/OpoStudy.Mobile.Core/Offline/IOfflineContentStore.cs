using OpoStudy.Contracts.Exams;
using OpoStudy.Contracts.Offline;

namespace OpoStudy.Mobile.Core.Offline;

/// <summary>Local copy of the exams' content (topics, questions, correct answers and explanations).</summary>
public interface IOfflineContentStore
{
    /// <summary>
    /// Downloads the content of the exam when it changed (<c>If-None-Match</c> with the stored version).
    /// Connectivity errors and unexpected statuses are ignored: the stored copy, if any, stays.
    /// </summary>
    Task RefreshAsync(Guid examId, CancellationToken cancellationToken = default);

    /// <summary>The stored content of the exam, or <c>null</c> when it was never downloaded.</summary>
    Task<OfflineContentResponse?> GetAsync(Guid examId, CancellationToken cancellationToken = default);

    /// <summary>The exams whose content is stored, ordered by name.</summary>
    Task<IReadOnlyList<ExamResponse>> ListExamsAsync(CancellationToken cancellationToken = default);
}