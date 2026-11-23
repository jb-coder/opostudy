using System.Text.Json.Serialization;

namespace OpoStudy.Infrastructure.Content;

// Shapes of the JSON files. Deserialized with required members and unmapped members disallowed.

internal sealed class ExamFile
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; init; }

    public required string Slug { get; init; }
    public required string Name { get; init; }
    public required string Organization { get; init; }
    public required string Group { get; init; }
    public required IReadOnlyList<ExamFileTopic> Topics { get; init; }
}

internal sealed class ExamFileTopic
{
    public required string Slug { get; init; }
    public required int Number { get; init; }
    public required string Title { get; init; }
    public string? Section { get; init; }
}

internal sealed class TopicFile
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; init; }

    public required string Topic { get; init; }
    public required IReadOnlyList<TopicFileQuestion> Questions { get; init; }
}

internal sealed class TopicFileQuestion
{
    public required string Key { get; init; }
    public required string Statement { get; init; }
    public required IReadOnlyList<TopicFileOption> Options { get; init; }
    public required string Explanation { get; init; }
    public required string LegalReference { get; init; }
}

internal sealed class TopicFileOption
{
    public required string Text { get; init; }
    public required bool IsCorrect { get; init; }
}

internal sealed class PracticalCaseFile
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; init; }

    public required string Key { get; init; }
    public required string Title { get; init; }
    public string? Section { get; init; }
    public required string Statement { get; init; }
    public required int EstimatedMinutes { get; init; }
    public required IReadOnlyList<PracticalCaseFilePart> Parts { get; init; }
}

internal sealed class PracticalCaseFilePart
{
    public required string Statement { get; init; }
    public required string ModelAnswer { get; init; }
    public required IReadOnlyList<PracticalCaseFileCriterion> Criteria { get; init; }
}

internal sealed class PracticalCaseFileCriterion
{
    public required string Description { get; init; }
    public required int Points { get; init; }
}
