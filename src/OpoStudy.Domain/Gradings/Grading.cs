using OpoStudy.Domain.Common;
using OpoStudy.Domain.PracticalCases;

namespace OpoStudy.Domain.Gradings;

/// <summary>
/// The correction of a practical case submitted by a user. It is a row of the correction queue:
/// <see cref="GradingStatus.Pending"/> → <see cref="GradingStatus.Processing"/> → <see cref="GradingStatus.Completed"/> or <see cref="GradingStatus.Failed"/>.
/// </summary>
public sealed class Grading
{
    private const decimal ScoreStep = 0.5m;

    private readonly List<GradingAnswer> _answers = [];

    private Grading()
    {
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid PracticalCaseId { get; private set; }
    public GradingStatus Status { get; private set; }
    public DateTimeOffset SubmittedAt { get; private set; }

    /// <summary>Earliest time the worker may take it (moved forward by <see cref="Retry"/>).</summary>
    public DateTimeOffset NextAttemptAt { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public int Attempts { get; private set; }

    /// <summary>Copy of the maximum points of the case when it was submitted.</summary>
    public int MaxPoints { get; private set; }

    public decimal? Score { get; private set; }
    public string? Feedback { get; private set; }

    /// <summary>Stable code of the reason it failed (e.g. <c>grading.provider_unavailable</c>).</summary>
    public string? FailureCode { get; private set; }

    public string? Corrector { get; private set; }
    public int? InputTokens { get; private set; }
    public int? OutputTokens { get; private set; }

    /// <summary>The answers sorted by <see cref="GradingAnswer.Order"/>, whatever order the persistence loaded them in.</summary>
    public IReadOnlyList<GradingAnswer> Answers => [.. _answers.OrderBy(a => a.Order)];

    /// <summary>Creates a pending grading with one answer per part (in the order of the parts), copying the rubric.</summary>
    public static Grading Submit(Guid userId, PracticalCase practicalCase, IReadOnlyList<string> answers, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(practicalCase);
        ArgumentNullException.ThrowIfNull(answers);

        if (!practicalCase.IsActive)
        {
            throw new DomainRuleException("grading.case_inactive", "El supuesto práctico ya no está disponible.");
        }

        var parts = practicalCase.Parts;
        if (answers.Count != parts.Count)
        {
            throw new DomainRuleException(
                "grading.invalid_answer_count",
                $"Hay que enviar una respuesta por apartado ({parts.Count}).");
        }

        var texts = answers.Select(a => a?.Trim() ?? string.Empty).ToArray();
        if (texts.Any(t => t.Length > Limits.PartAnswer))
        {
            throw new DomainRuleException(
                "grading.invalid_answer",
                $"Cada respuesta puede tener como máximo {Limits.PartAnswer} caracteres.");
        }

        if (texts.All(t => t.Length == 0))
        {
            throw new DomainRuleException("grading.empty", "Responde al menos un apartado para pedir la corrección.");
        }

        var utcNow = now.ToUniversalTime();
        var grading = new Grading
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            PracticalCaseId = practicalCase.Id,
            Status = GradingStatus.Pending,
            SubmittedAt = utcNow,
            NextAttemptAt = utcNow,
            MaxPoints = practicalCase.MaxPoints,
        };
        for (var i = 0; i < parts.Count; i++)
        {
            var part = parts[i];
            grading._answers.Add(GradingAnswer.Create(
                grading.Id,
                i,
                texts[i],
                part.MaxPoints,
                part.Criteria.Select(c => new CriterionScore(c.Description, c.Points, null))));
        }

        return grading;
    }

    /// <summary>Claims it for a worker. Valid from <see cref="GradingStatus.Pending"/>, or from an expired <see cref="GradingStatus.Processing"/> (the queue query decides when it expired).</summary>
    public void Start(DateTimeOffset now)
    {
        RequireStatus(GradingStatus.Pending, GradingStatus.Processing);
        Status = GradingStatus.Processing;
        Attempts++;
        StartedAt = now.ToUniversalTime();
    }

    /// <summary>Validates <paramref name="outcome"/> and, if it fits the rubric, stores the scores. Nothing changes if it is invalid.</summary>
    public void Complete(GradingOutcome outcome, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        RequireStatus(GradingStatus.Processing);

        var (feedback, partFeedbacks, corrector) = ValidateOutcome(outcome);

        var ordered = Answers;
        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].Apply(outcome.Parts[i], partFeedbacks[i]);
        }

        Score = ordered.Sum(a => a.Score);
        Feedback = feedback;
        Corrector = corrector;
        InputTokens = outcome.InputTokens;
        OutputTokens = outcome.OutputTokens;
        FailureCode = null;
        Status = GradingStatus.Completed;
        CompletedAt = now.ToUniversalTime();
    }

    /// <summary>Puts it back in the queue to be tried again after <paramref name="delay"/>.</summary>
    public void Retry(DateTimeOffset now, TimeSpan delay)
    {
        RequireStatus(GradingStatus.Processing);
        Status = GradingStatus.Pending;
        NextAttemptAt = now.ToUniversalTime() + delay;
    }

    /// <summary>Gives up with a stable failure code.</summary>
    public void Fail(string code, DateTimeOffset now)
    {
        RequireStatus(GradingStatus.Processing);
        FailureCode = Guard.RequiredText(code, Limits.FailureCode, "grading.invalid_status");
        Status = GradingStatus.Failed;
        CompletedAt = now.ToUniversalTime();
    }

    private static DomainRuleException Invalid() =>
        new("grading.invalid_result", "El resultado de la corrección no es válido.");

    private static string? OptionalFeedback(string? feedback)
    {
        var text = feedback?.Trim();
        if (text is { Length: > Limits.GradingFeedback })
        {
            throw Invalid();
        }

        return string.IsNullOrEmpty(text) ? null : text;
    }

    private (string? Feedback, string?[] PartFeedbacks, string Corrector) ValidateOutcome(GradingOutcome outcome)
    {
        var ordered = Answers;
        if (outcome.Parts is null || outcome.Parts.Count != ordered.Count)
        {
            throw Invalid();
        }

        var partFeedbacks = new string?[ordered.Count];
        for (var i = 0; i < ordered.Count; i++)
        {
            var part = outcome.Parts[i];
            var criteria = ordered[i].Criteria;
            if (part?.Awarded is null || part.Awarded.Count != criteria.Count)
            {
                throw Invalid();
            }

            for (var c = 0; c < criteria.Count; c++)
            {
                var awarded = part.Awarded[c];
                if (awarded < 0 || awarded > criteria[c].Points || awarded % ScoreStep != 0)
                {
                    throw Invalid();
                }
            }

            partFeedbacks[i] = OptionalFeedback(part.Feedback);
        }

        if (outcome.InputTokens is < 0 || outcome.OutputTokens is < 0)
        {
            throw Invalid();
        }

        var corrector = outcome.Corrector?.Trim();
        if (string.IsNullOrEmpty(corrector) || corrector.Length > Limits.Corrector)
        {
            throw Invalid();
        }

        return (OptionalFeedback(outcome.Feedback), partFeedbacks, corrector);
    }

    private void RequireStatus(params GradingStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new DomainRuleException(
                "grading.invalid_status",
                $"La corrección no puede cambiar de estado desde {Status}.");
        }
    }
}