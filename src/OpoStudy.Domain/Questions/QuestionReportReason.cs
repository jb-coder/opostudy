namespace OpoStudy.Domain.Questions;

/// <summary>Why a user reports a question.</summary>
public enum QuestionReportReason
{
    /// <summary>The correct answer is wrong.</summary>
    WrongAnswer,

    /// <summary>The law is repealed or has been modified.</summary>
    OutdatedLegislation,

    /// <summary>The statement or the options are ambiguous.</summary>
    UnclearStatement,

    /// <summary>A typo.</summary>
    Typo,

    /// <summary>Anything else; the comment is required.</summary>
    Other,
}
