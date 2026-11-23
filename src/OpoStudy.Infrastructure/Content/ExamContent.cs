using OpoStudy.Domain.PracticalCases;
using OpoStudy.Domain.Questions;

namespace OpoStudy.Infrastructure.Content;

/// <summary>An exam with its topics and questions, as read from <c>content/</c>.</summary>
public sealed record ExamContent(
    string Slug,
    string Name,
    string Organization,
    string Group,
    IReadOnlyList<TopicContent> Topics,
    IReadOnlyList<PracticalCaseContent> PracticalCases);

public sealed record TopicContent(
    string Slug,
    int Number,
    string Title,
    string? Section,
    IReadOnlyList<QuestionContent> Questions);

public sealed record QuestionContent(
    string Key,
    string Statement,
    string Explanation,
    string LegalReference,
    IReadOnlyList<AnswerOptionData> Options);

/// <summary>A problem found in a content file. <see cref="Message"/> is meant for people, hence in Spanish.</summary>
/// <param name="File">Path of the file relative to the content root, with forward slashes.</param>
/// <param name="Path">JSON path of the offending element (e.g. <c>$.questions[3].options</c>).</param>
/// <param name="Message">What is wrong.</param>
public sealed record ContentError(string File, string Path, string Message)
{
    public override string ToString() => $"{File}: {Path}: {Message}";
}

/// <summary>Result of reading the content. <see cref="Exams"/> is empty when there are errors.</summary>
public sealed record ContentReadResult(IReadOnlyList<ExamContent> Exams, IReadOnlyList<ContentError> Errors);

public sealed record PracticalCaseContent(
    string Key,
    string Title,
    string Statement,
    int EstimatedMinutes,
    IReadOnlyList<PracticalCasePartData> Parts);
