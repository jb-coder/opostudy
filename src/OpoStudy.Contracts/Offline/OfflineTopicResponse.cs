namespace OpoStudy.Contracts.Offline;

/// <summary>A topic with its active questions, for offline use.</summary>
public sealed record OfflineTopicResponse(Guid Id, int Number, string Title, string? Section, IReadOnlyList<OfflineQuestionResponse> Questions);
