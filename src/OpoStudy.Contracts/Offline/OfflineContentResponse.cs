using OpoStudy.Contracts.Exams;

namespace OpoStudy.Contracts.Offline;

/// <summary>Everything the app needs to study an exam without a connection.</summary>
/// <param name="ExamId">Exam the content belongs to.</param>
/// <param name="Version">SHA-256 (lowercase hex) of the serialized topics; it is also the ETag of the response.</param>
/// <param name="Exam">The exam.</param>
/// <param name="Topics">Topics ordered by number, with their active questions.</param>
public sealed record OfflineContentResponse(Guid ExamId, string Version, ExamResponse Exam, IReadOnlyList<OfflineTopicResponse> Topics);
