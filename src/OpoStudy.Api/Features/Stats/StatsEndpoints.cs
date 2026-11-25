using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

using OpoStudy.Api.Errors;
using OpoStudy.Api.Features.Users;
using OpoStudy.Contracts.Stats;
using OpoStudy.Domain.Common;
using OpoStudy.Infrastructure.Persistence;

namespace OpoStudy.Api.Features.Stats;

internal static class StatsEndpoints
{
    public static RouteGroupBuilder MapStatsEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/me/stats", GetStats)
            .WithName("GetStats")
            .WithTags("Stats")
            .WithSummary("Estadísticas de tests y supuestos de una oposición")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();

        api.MapGet("/me/activity", GetActivity)
            .WithName("GetActivity")
            .WithTags("Stats")
            .WithSummary("Actividad diaria (respuestas, XP, objetivo y días protegidos) en un rango de fechas")
            .ProducesValidationProblem()
            .RequireAuthorization();

        return api;
    }

    private static async Task<Results<Ok<StatsResponse>, ValidationProblem, ProblemHttpResult>> GetStats(
        Guid? examId,
        CurrentUserService currentUser,
        OpoStudyDbContext db,
        CancellationToken cancellationToken)
    {
        if (examId is null || examId == Guid.Empty)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["examId"] = ["La oposición es obligatoria."],
            });
        }

        var user = await currentUser.GetOrCreateAsync(cancellationToken);

        if (!await db.Exams.AnyAsync(e => e.Id == examId && e.IsActive, cancellationToken))
        {
            return DomainErrors.Problem(DomainErrors.ExamNotFound);
        }

        var quizzes = await StatsQueries.GetQuizStatsAsync(db, user.Id, examId.Value, cancellationToken);
        var practicalCases = await StatsQueries.GetPracticalCaseStatsAsync(db, user.Id, examId.Value, cancellationToken);
        return TypedResults.Ok(new StatsResponse(examId.Value, quizzes, practicalCases));
    }

    private static async Task<Results<Ok<ActivityResponse>, ValidationProblem>> GetActivity(
        DateOnly? from,
        DateOnly? to,
        CurrentUserService currentUser,
        OpoStudyDbContext db,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (from is null)
        {
            errors["from"] = ["La fecha es obligatoria."];
        }

        if (to is null)
        {
            errors["to"] = ["La fecha es obligatoria."];
        }

        if (from is { } start && to is { } end)
        {
            // DayNumber arithmetic: AddDays would overflow near DateOnly.MaxValue.
            if (start.DayNumber > end.DayNumber)
            {
                errors["from"] = ["La fecha inicial no puede ser posterior a la final."];
            }
            else if (end.DayNumber - start.DayNumber + 1 > Limits.MaxActivityRangeDays)
            {
                errors["to"] = [$"El periodo no puede superar los {Limits.MaxActivityRangeDays} días."];
            }
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var user = await currentUser.GetOrCreateAsync(cancellationToken);
        var days = await StatsQueries.GetActivityAsync(db, user.Id, from!.Value, to!.Value, cancellationToken);
        return TypedResults.Ok(new ActivityResponse(user.TodayAt(timeProvider.GetUtcNow()), user.DailyGoal, days));
    }
}