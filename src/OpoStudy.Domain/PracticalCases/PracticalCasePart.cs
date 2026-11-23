using OpoStudy.Domain.Common;

namespace OpoStudy.Domain.PracticalCases;

/// <summary>A part of a <see cref="PracticalCase"/> with its rubric and model answer. Only created and modified through the case.</summary>
public sealed class PracticalCasePart
{
    private readonly List<GradingCriterion> _criteria = [];

    private PracticalCasePart()
    {
        Statement = ModelAnswer = string.Empty;
    }

    public Guid Id { get; private set; }
    public Guid PracticalCaseId { get; private set; }
    public int Order { get; private set; }
    public string Statement { get; private set; }
    public string ModelAnswer { get; private set; }

    /// <summary>Sum of the criteria points. Stored in a column so it can be summed in SQL.</summary>
    public int MaxPoints { get; private set; }

    /// <summary>The criteria in the order of the content.</summary>
    public IReadOnlyList<GradingCriterion> Criteria => _criteria;

    internal static PracticalCasePart Create(Guid practicalCaseId, int order, PracticalCasePartData data)
    {
        var validated = Validate(data);
        var part = new PracticalCasePart
        {
            Id = Guid.CreateVersion7(),
            PracticalCaseId = practicalCaseId,
            Order = order,
        };
        part.Apply(validated);
        return part;
    }

    /// <summary>Validates the data of a part without building it.</summary>
    internal static ValidatedPart Validate(PracticalCasePartData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(data.Criteria);

        var statement = Guard.RequiredText(data.Statement, Limits.Statement, "practical_case.invalid_part_statement");
        var modelAnswer = Guard.RequiredText(data.ModelAnswer, Limits.ModelAnswer, "practical_case.invalid_model_answer");
        if (data.Criteria.Count is < Limits.MinCriteriaPerPart or > Limits.MaxCriteriaPerPart)
        {
            throw new DomainRuleException(
                "practical_case.invalid_criterion_count",
                $"Un apartado debe tener entre {Limits.MinCriteriaPerPart} y {Limits.MaxCriteriaPerPart} criterios.");
        }

        var criteria = new List<GradingCriterion>(data.Criteria.Count);
        foreach (var criterion in data.Criteria)
        {
            var description = Guard.RequiredText(criterion.Description, Limits.CriterionDescription, "practical_case.invalid_criterion_description");
            if (criterion.Points is < Limits.MinCriterionPoints or > Limits.MaxCriterionPoints)
            {
                throw new DomainRuleException(
                    "practical_case.invalid_criterion_points",
                    $"Los puntos de un criterio deben estar entre {Limits.MinCriterionPoints} y {Limits.MaxCriterionPoints}.");
            }

            criteria.Add(new GradingCriterion(description, criterion.Points));
        }

        return new ValidatedPart(statement, modelAnswer, criteria);
    }

    /// <summary>Applies already validated data (see <see cref="Validate"/>); it cannot fail.</summary>
    internal void Apply(ValidatedPart validated)
    {
        Statement = validated.Statement;
        ModelAnswer = validated.ModelAnswer;
        _criteria.Clear();
        _criteria.AddRange(validated.Criteria);
        MaxPoints = _criteria.Sum(c => c.Points);
    }

    internal sealed record ValidatedPart(string Statement, string ModelAnswer, IReadOnlyList<GradingCriterion> Criteria);
}