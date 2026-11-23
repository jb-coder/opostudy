namespace OpoStudy.Contracts.Offline;

/// <summary>An answer option for offline use. Unlike the quiz endpoints, it reveals whether it is the correct one.</summary>
public sealed record OfflineOptionResponse(Guid Id, int Order, string Text, bool IsCorrect);
