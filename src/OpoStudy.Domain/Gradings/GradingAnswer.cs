using OpoStudy.Domain.Common;

namespace OpoStudy.Domain.Gradings;

/// <summary>The answer of the user to one part of the case, with a copy of the rubric. Only created and modified through the <see cref="Grading"/>.</summary>
public sealed class GradingAnswer
{
    private readonly List<CriterionScore> _criteria = [];

    private GradingAnswer()
    {
        Text = string.Empty;
    }

    public Guid Id { get; private set; }
    public Guid GradingId { get; private set; }
    public int Order { get; private set; }

    /// <summary>The answer; empty when the part was left blank.</summary>
    public string Text { get; private set; }

    public int MaxPoints { get; private set; }

    /// <summary>The criteria copied from the part when the grading was submitted.</summary>
    public IReadOnlyList<CriterionScore> Criteria => _criteria;

    public decimal? Score { get; private set; }
    public string? Feedback { get; private set; }

    internal static GradingAnswer Create(Guid gradingId, int order, string text, int maxPoints, IEnumerable<CriterionScore> criteria)
    {
        var answer = new GradingAnswer
        {
            Id = Guid.CreateVersion7(),
            GradingId = gradingId,
            Order = order,
            Text = text,
            MaxPoints = maxPoints,
        };
        answer._criteria.AddRange(criteria);
        return answer;
    }

    /// <summary>Applies an already validated outcome (see <see cref="Grading.Complete"/>); it cannot fail.</summary>
    internal void Apply(PartOutcome outcome, string? feedback)
    {
        var scored = _criteria.Select((c, i) => c with { Awarded = outcome.Awarded[i] }).ToList();
        _criteria.Clear();
        _criteria.AddRange(scored);
        Score = scored.Sum(c => c.Awarded);
        Feedback = feedback;
    }
}
