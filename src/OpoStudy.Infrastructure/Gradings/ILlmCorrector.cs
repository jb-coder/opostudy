using OpoStudy.Domain.Gradings;
using OpoStudy.Domain.PracticalCases;

namespace OpoStudy.Infrastructure.Gradings;

/// <summary>One part of the case to correct. Nothing personal: no user id, uid or email ever reaches a corrector.</summary>
public sealed record GradingPartRequest(string Statement, string ModelAnswer, IReadOnlyList<GradingCriterion> Criteria, string Answer);

/// <summary>What a corrector receives: the case and the answers of the user, one per part in order.</summary>
public sealed record GradingRequest(string CaseStatement, IReadOnlyList<GradingPartRequest> Parts);

/// <summary>Corrects the answers to a practical case against its rubric.</summary>
public interface ILlmCorrector
{
    /// <summary>
    /// Grades the request. Throw <see cref="LlmTransientException"/> for errors worth retrying (timeout, 429, 5xx);
    /// any other exception is final. The outcome is validated against the rubric by <c>Grading.Complete</c>.
    /// </summary>
    Task<GradingOutcome> GradeAsync(GradingRequest request, CancellationToken cancellationToken);
}

/// <summary>A temporary failure of the corrector (timeout, rate limit, server error): the grading is retried.</summary>
public sealed class LlmTransientException : Exception
{
    public LlmTransientException()
    {
    }

    public LlmTransientException(string message)
        : base(message)
    {
    }

    public LlmTransientException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}