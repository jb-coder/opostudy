using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using OpoStudy.Infrastructure.Persistence;

using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(OpoStudy.Infrastructure.Tests.PostgresFixture))]

namespace OpoStudy.Infrastructure.Tests;

/// <summary>A single real Postgres (Docker) for the whole assembly, migrated once.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync() => await _postgres.DisposeAsync();

    public OpoStudyDbContext CreateContext(params IInterceptor[] interceptors)
    {
        var builder = new DbContextOptionsBuilder<OpoStudyDbContext>().UseNpgsql(_postgres.GetConnectionString());
        builder.AddInterceptors(interceptors);
        OpoStudyDbContext.ApplyConventions(builder);
        return new OpoStudyDbContext(builder.Options);
    }
}