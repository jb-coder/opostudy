namespace OpoStudy.Domain.Common;

/// <summary>
/// Thrown when an operation breaks a domain rule. <see cref="Code"/> is stable (e.g. <c>question.no_correct_option</c>)
/// so it can be mapped to ProblemDetails; the message is user-facing and therefore written in Spanish.
/// </summary>
public sealed class DomainRuleException : Exception
{
    private const string DefaultCode = "domain.rule_broken";

    public DomainRuleException()
        : this(DefaultCode, "Se ha incumplido una regla del dominio.")
    {
    }

    public DomainRuleException(string message)
        : this(DefaultCode, message)
    {
    }

    public DomainRuleException(string message, Exception innerException)
        : base(message, innerException)
    {
        Code = DefaultCode;
    }

    public DomainRuleException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}