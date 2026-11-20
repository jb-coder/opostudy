namespace OpoStudy.Contracts.Offline;

/// <summary>A question with its options (correct one included), explanation and legal reference, for offline use.</summary>
public sealed record OfflineQuestionResponse(
    Guid Id,
    string Statement,
    string Explanation,
    string LegalReference,
    IReadOnlyList<OfflineOptionResponse> Options);
