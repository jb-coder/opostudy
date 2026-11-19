using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OpoStudy.Infrastructure.Persistence;

/// <summary>
/// Only for the EF tools (<c>dotnet ef migrations add</c>), which do not need to connect to the database.
/// The connection string has no credentials on purpose: the real one is provided by Aspire at runtime.
/// </summary>
internal sealed class OpoStudyDbContextFactory : IDesignTimeDbContextFactory<OpoStudyDbContext>
{
    public OpoStudyDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<OpoStudyDbContext>().UseNpgsql("Host=localhost;Database=opostudy");
        OpoStudyDbContext.ApplyConventions(builder);
        return new OpoStudyDbContext(builder.Options);
    }
}