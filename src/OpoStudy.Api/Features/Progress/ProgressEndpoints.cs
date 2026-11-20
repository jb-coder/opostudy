using Microsoft.AspNetCore.Http.HttpResults;

using OpoStudy.Api.Features.Users;
using OpoStudy.Contracts.Progress;
using OpoStudy.Domain.Common;
using OpoStudy.Domain.Users;
using OpoStudy.Infrastructure.Persistence;

namespace OpoStudy.Api.Features.Progress;

internal static class ProgressEndpoints
{
    public static RouteGroupBuilder MapProgressEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/me/streak", GetStreak)
            .WithName("GetStreak")
            .WithTags("Progress")
            .WithSummary("Progreso del día, racha y últimos 7 días")
            .RequireAuthorization();

        api.MapGet("/me/level", GetLevel)
            .WithName("GetLevel")
            .WithTags("Progress")
            .WithSummary("Nivel, XP total, XP dentro del nivel y XP de hoy")
            .RequireAuthorization();

        api.MapPut("/me/daily-goal", UpdateDailyGoal)
            .WithName("UpdateDailyGoal")
            .WithTags("Progress")
            .WithSummary("Cambia el objetivo diario de preguntas (se aplica desde hoy)")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization();

        api.MapPut("/me/time-zone", UpdateTimeZone)
            .WithName("UpdateTimeZone")
            .WithTags("Progress")
            .WithSummary("Cambia la zona horaria (IANA) que define el día del usuario")
            .ProducesValidationProblem()
            .RequireAuthorization();

        return api;
    }

    private static async Task<Ok<StreakResponse>> GetStreak(
        CurrentUserService currentUser,
        ProgressRecorder progress,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var user = await currentUser.GetOrCreateAsync(cancellationToken);
        return TypedResults.Ok(await progress.GetStreakAsync(user, timeProvider.GetUtcNow(), cancellationToken));
    }

    private static async Task<Ok<LevelResponse>> GetLevel(
        CurrentUserService currentUser,
        ProgressRecorder progress,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var user = await currentUser.GetOrCreateAsync(cancellationToken);
        return TypedResults.Ok(await progress.GetLevelAsync(user, timeProvider.GetUtcNow(), cancellationToken));
    }

    private static async Task<Results<Ok<StreakResponse>, ValidationProblem>> UpdateDailyGoal(
        UpdateDailyGoalRequest request,
        CurrentUserService currentUser,
        ProgressRecorder progress,
        OpoStudyDbContext db,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (request.DailyGoal is < Limits.MinDailyGoal or > Limits.MaxDailyGoal)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["dailyGoal"] = [$"El objetivo diario debe estar entre {Limits.MinDailyGoal} y {Limits.MaxDailyGoal} preguntas."],
            });
        }

        var user = await currentUser.GetOrCreateAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        await progress.ChangeDailyGoalAsync(user, request.DailyGoal, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(await progress.GetStreakAsync(user, now, cancellationToken));
    }

    private static async Task<Results<NoContent, ValidationProblem>> UpdateTimeZone(
        UpdateTimeZoneRequest request,
        CurrentUserService currentUser,
        OpoStudyDbContext db,
        CancellationToken cancellationToken)
    {
        if (!User.IsValidTimeZone(request.TimeZone))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["timeZone"] = ["La zona horaria no es válida."],
            });
        }

        var user = await currentUser.GetOrCreateAsync(cancellationToken);
        user.SetTimeZone(request.TimeZone);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}