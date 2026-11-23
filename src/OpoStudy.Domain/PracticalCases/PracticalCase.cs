using OpoStudy.Domain.Common;

namespace OpoStudy.Domain.PracticalCases;

/// <summary>
/// A practical case (supuesto práctico) with parts, each with a rubric and a model answer.
/// It is never deleted, only deactivated, to keep the history of gradings.
/// </summary>
public sealed class PracticalCase
{
    private readonly List<PracticalCasePart> _parts = [];

    private PracticalCase()
    {
        Key = Title = Statement = string.Empty;
    }

    public Guid Id { get; private set; }
    public Guid ExamId { get; private set; }

    /// <summary>Stable identifier coming from the versioned content (e.g. <c>malaga-programador-c1/practical-cases/0001</c>).</summary>
    public string Key { get; private set; }

    public string Title { get; private set; }
    public string Statement { get; private set; }
    public int EstimatedMinutes { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>The parts sorted by <see cref="PracticalCasePart.Order"/>, whatever order the persistence loaded them in.</summary>
    public IReadOnlyList<PracticalCasePart> Parts => [.. _parts.OrderBy(p => p.Order)];

    /// <summary>Sum of the points of every part.</summary>
    public int MaxPoints => _parts.Sum(p => p.MaxPoints);

    public static PracticalCase Create(
        Guid examId,
        string key,
        string title,
        string statement,
        int estimatedMinutes,
        IReadOnlyList<PracticalCasePartData> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        ValidatePartCount(parts.Count);

        var practicalCase = new PracticalCase
        {
            Id = Guid.CreateVersion7(),
            ExamId = examId,
            Key = Guard.RequiredText(key, Limits.Key, "practical_case.invalid_key"),
            IsActive = true,
        };
        (practicalCase.Title, practicalCase.Statement, practicalCase.EstimatedMinutes) =
            ValidateTexts(title, statement, estimatedMinutes);
        for (var order = 0; order < parts.Count; order++)
        {
            practicalCase._parts.Add(PracticalCasePart.Create(practicalCase.Id, order, parts[order]));
        }

        return practicalCase;
    }

    /// <summary>
    /// Updates the texts and the parts (matched by order). The number of parts cannot change because
    /// the answers point to them: in that case create a new case with another key and deactivate this one.
    /// </summary>
    public void Update(
        Guid examId,
        string title,
        string statement,
        int estimatedMinutes,
        IReadOnlyList<PracticalCasePartData> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        ValidatePartCount(parts.Count);
        if (parts.Count != _parts.Count)
        {
            throw new DomainRuleException(
                "practical_case.part_count_changed",
                "No se puede cambiar el número de apartados de un supuesto existente; crea otro con una key nueva y desactiva este.");
        }

        // Validate everything first so a failure leaves the case untouched.
        var texts = ValidateTexts(title, statement, estimatedMinutes);
        var validatedParts = parts.Select(PracticalCasePart.Validate).ToArray();

        ExamId = examId;
        (Title, Statement, EstimatedMinutes) = texts;
        foreach (var part in _parts)
        {
            part.Apply(validatedParts[part.Order]);
        }
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    private static (string Title, string Statement, int EstimatedMinutes) ValidateTexts(string title, string statement, int estimatedMinutes)
    {
        var validTitle = Guard.RequiredText(title, Limits.Name, "practical_case.invalid_title");
        var validStatement = Guard.RequiredText(statement, Limits.CaseStatement, "practical_case.invalid_statement");
        if (estimatedMinutes is < Limits.MinEstimatedMinutes or > Limits.MaxEstimatedMinutes)
        {
            throw new DomainRuleException(
                "practical_case.invalid_estimated_minutes",
                $"La duración estimada debe estar entre {Limits.MinEstimatedMinutes} y {Limits.MaxEstimatedMinutes} minutos.");
        }

        return (validTitle, validStatement, estimatedMinutes);
    }

    private static void ValidatePartCount(int count)
    {
        if (count is < Limits.MinCaseParts or > Limits.MaxCaseParts)
        {
            throw new DomainRuleException(
                "practical_case.invalid_part_count",
                $"Un supuesto debe tener entre {Limits.MinCaseParts} y {Limits.MaxCaseParts} apartados.");
        }
    }
}