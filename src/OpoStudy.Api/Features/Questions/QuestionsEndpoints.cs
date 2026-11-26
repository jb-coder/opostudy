using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

using Npgsql;

using OpoStudy.Api.Errors;
using OpoStudy.Api.Features.Users;
using OpoStudy.Contracts.Questions;
using OpoStudy.Domain.Questions;
using OpoStudy.Infrastructure.Persistence;

namespace OpoStudy.Api.Features.Questions;

internal static class QuestionsEndpoints
{
    public static RouteGroupBuilder MapQuestionsEndpoints(this RouteGroupBuilder api)
    {
        api.MapPut("/questions/{questionId:guid}/bookmark", BookmarkQuestion)
            .WithName("BookmarkQuestion")
            .WithTags("Questions")
            .WithSummary("Marca una pregunta para repasarla después (idempotente)")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();

        api.MapDelete("/questions/{questionId:guid}/bookmark", UnbookmarkQuestion)
            .WithName("UnbookmarkQuestion")
            .WithTags("Questions")
            .WithSummary("Quita la marca de una pregunta (idempotente)")
            .RequireAuthorization();

        api.MapPost("/questions/{questionId:guid}/reports", ReportQuestion)
            .WithName("ReportQuestion")
            .WithTags("Questions")
            .WithSummary("Reporta una pregunta que parece errónea")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization();

        return api;
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> BookmarkQuestion(
        Guid questionId,
        CurrentUserService currentUser,
        OpoStudyDbContext db,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var user = await currentUser.GetOrCreateAsync(cancellationToken);

        if (!await IsAvailableAsync(db, questionId, cancellationToken))
        {
            return DomainErrors.Problem(DomainErrors.QuestionNotFound);
        }

        var userId = user.Id;
        if (await db.QuestionBookmarks.AnyAsync(b => b.UserId == userId && b.QuestionId == questionId, cancellationToken))
        {
            return TypedResults.NoContent();
        }

        db.QuestionBookmarks.Add(QuestionBookmark.Create(userId, questionId, timeProvider.GetUtcNow()));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: DbConstraintNames.QuestionBookmarksPrimaryKey,
        })
        {
            // A concurrent request bookmarked the same question first: the result is the same.
        }

        return TypedResults.NoContent();
    }

    private static async Task<NoContent> UnbookmarkQuestion(
        Guid questionId,
        CurrentUserService currentUser,
        OpoStudyDbContext db,
        CancellationToken cancellationToken)
    {
        var user = await currentUser.GetOrCreateAsync(cancellationToken);
        var userId = user.Id;

        await db.QuestionBookmarks
            .Where(b => b.UserId == userId && b.QuestionId == questionId)
            .ExecuteDeleteAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ValidationProblem, ProblemHttpResult>> ReportQuestion(
        Guid questionId,
        CreateQuestionReportRequest request,
        CurrentUserService currentUser,
        OpoStudyDbContext db,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new Dictionary<string, string[]>();
        // Match a single enum name exactly: Enum.TryParse would also accept numbers and comma-separated flag lists.
        var reasonName = Enum.GetNames<QuestionReportReason>()
            .FirstOrDefault(name => string.Equals(name, request.Reason?.Trim(), StringComparison.OrdinalIgnoreCase));
        var reason = reasonName is null ? default : Enum.Parse<QuestionReportReason>(reasonName);
        if (reasonName is null)
        {
            errors["reason"] = ["Elige un motivo."];
        }

        var comment = request.Comment?.Trim();
        if (comment is { Length: > QuestionReport.MaxCommentLength })
        {
            errors["comment"] = [$"El comentario no puede superar los {QuestionReport.MaxCommentLength} caracteres."];
        }
        else if (errors.Count == 0 && reason == QuestionReportReason.Other && string.IsNullOrEmpty(comment))
        {
            errors["comment"] = ["Cuéntanos qué está mal."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var user = await currentUser.GetOrCreateAsync(cancellationToken);
        var userId = user.Id;

        if (!await IsAvailableAsync(db, questionId, cancellationToken))
        {
            return DomainErrors.Problem(DomainErrors.QuestionNotFound);
        }

        if (await db.QuestionReports.AnyAsync(
            r => r.UserId == userId && r.QuestionId == questionId && r.Status == QuestionReportStatus.Open,
            cancellationToken))
        {
            return DomainErrors.Problem(DomainErrors.QuestionAlreadyReported);
        }

        // A concurrent duplicate hits the partial unique index; DomainRuleExceptionHandler turns it into the same 409.
        db.QuestionReports.Add(QuestionReport.Create(userId, questionId, reason, comment, timeProvider.GetUtcNow()));
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>Active question of an active exam; anything else is reported as not found.</summary>
    private static Task<bool> IsAvailableAsync(OpoStudyDbContext db, Guid questionId, CancellationToken cancellationToken) =>
        db.Questions.AnyAsync(
            q => q.Id == questionId
                && q.IsActive
                && db.Topics.Any(t => t.Id == q.TopicId && db.Exams.Any(e => e.Id == t.ExamId && e.IsActive)),
            cancellationToken);
}