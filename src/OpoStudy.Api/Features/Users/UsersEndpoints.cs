using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

using OpoStudy.Api.Authentication;
using OpoStudy.Contracts.Users;
using OpoStudy.Infrastructure.Persistence;

namespace OpoStudy.Api.Features.Users;

internal static class UsersEndpoints
{
    public static RouteGroupBuilder MapUsersEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/me", GetMe)
            .WithName("GetMe")
            .WithTags("Users")
            .WithSummary("Perfil del usuario autenticado (lo crea en el primer acceso)");

        api.MapDelete("/me", DeleteMe)
            .WithName("DeleteMe")
            .WithTags("Users")
            .WithSummary("Borra el usuario y todos sus datos");

        return api;
    }

    private static async Task<Ok<UserProfileResponse>> GetMe(CurrentUserService currentUser, CancellationToken cancellationToken)
    {
        var user = await currentUser.GetOrCreateAsync(cancellationToken);
        return TypedResults.Ok(new UserProfileResponse(user.Id, user.CreatedAt, user.ActiveExamId, user.DailyGoal, user.TimeZone));
    }

    private static async Task<NoContent> DeleteMe(HttpContext http, OpoStudyDbContext db, CancellationToken cancellationToken)
    {
        var uid = http.User.GetFirebaseUid();

        // Single statement; Postgres cascades to quiz attempts and answers. Idempotent.
        await db.Users.Where(u => u.FirebaseUid == uid).ExecuteDeleteAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}