using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace OpoStudy.Infrastructure.Persistencia;

public static class MigracionesExtensions
{
    /// <summary>
    /// Aplica las migraciones pendientes. Solo para Development; en el VPS se usará un migration bundle.
    /// </summary>
    public static async Task AplicarMigracionesAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OpoStudyDbContext>();
        await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }
}