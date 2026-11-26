using OpoStudy.Domain.Common;

namespace OpoStudy.Domain.Questions;

/// <summary>A report of a user about a question they think is wrong. It is reviewed by hand in the database.</summary>
public sealed class QuestionReport
{
    public const int MaxCommentLength = 500;

    private QuestionReport()
    {
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid QuestionId { get; private set; }
    public QuestionReportReason Reason { get; private set; }
    public string? Comment { get; private set; }
    public QuestionReportStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static QuestionReport Create(Guid userId, Guid questionId, QuestionReportReason reason, string? comment, DateTimeOffset now)
    {
        if (userId == Guid.Empty || questionId == Guid.Empty)
        {
            throw new DomainRuleException("question_report.invalid_id", "El reporte necesita usuario y pregunta.");
        }

        if (!Enum.IsDefined(reason))
        {
            throw new DomainRuleException("question_report.invalid_reason", "Elige un motivo.");
        }

        var text = comment?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            text = null;
        }

        if (text is { Length: > MaxCommentLength })
        {
            throw new DomainRuleException("question_report.comment_too_long", "El comentario no puede superar los 500 caracteres.");
        }

        if (reason == QuestionReportReason.Other && text is null)
        {
            throw new DomainRuleException("question_report.comment_required", "Cuéntanos qué está mal.");
        }

        return new QuestionReport
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            QuestionId = questionId,
            Reason = reason,
            Comment = text,
            Status = QuestionReportStatus.Open,
            CreatedAt = now.ToUniversalTime(),
        };
    }
}
