using Microsoft.EntityFrameworkCore;

using Npgsql;

using OpoStudy.Api.Authentication;
using OpoStudy.Domain.Users;
using OpoStudy.Infrastructure.Persistence;

namespace OpoStudy.Api.Features.Users;

/// <summary>Resolves the <see cref="User"/> of the current request, creating it on first access.</summary>
internal sealed class CurrentUserService(
    OpoStudyDbContext db,
    IHttpContextAccessor httpContextAccessor,
    TimeProvider timeProvider)
{
    public async Task<User> GetOrCreateAsync(CancellationToken cancellationToken)
    {
        var principal = httpContextAccessor.HttpContext?.User
            ?? throw new InvalidOperationException("There is no current HTTP request.");
        var uid = principal.GetFirebaseUid();

        var user = await FindAsync(uid, cancellationToken);
        if (user is not null)
        {
            return user;
        }

        user = User.Create(uid, timeProvider.GetUtcNow());
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return user;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // A concurrent request created the same user first: discard ours and read the existing one.
            db.Entry(user).State = EntityState.Detached;
            return await FindAsync(uid, cancellationToken)
                ?? throw new InvalidOperationException("The user disappeared after a unique violation.", ex);
        }
    }

    private Task<User?> FindAsync(string uid, CancellationToken cancellationToken) =>
        db.Users.FirstOrDefaultAsync(u => u.FirebaseUid == uid, cancellationToken);
}