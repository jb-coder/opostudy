using Microsoft.EntityFrameworkCore;

using OpoStudy.Infrastructure.Persistencia;

using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(OpoStudy.Infrastructure.Tests.PostgresFixture))]

namespace OpoStudy.Infrastructure.Tests;

/// <summary>Un único Postgres real (Docker) para todo el ensamblado, migrado una vez.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = CrearContexto();
        await db.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync() => await _postgres.DisposeAsync();

    public OpoStudyDbContext CrearContexto()
    {
        var builder = new DbContextOptionsBuilder<OpoStudyDbContext>().UseNpgsql(_postgres.GetConnectionString());
        OpoStudyDbContext.AplicarConvenciones(builder);
        return new OpoStudyDbContext(builder.Options);
    }
}