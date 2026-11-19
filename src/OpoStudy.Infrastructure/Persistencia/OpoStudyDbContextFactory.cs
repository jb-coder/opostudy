using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OpoStudy.Infrastructure.Persistencia;

/// <summary>
/// Solo para las herramientas de EF (<c>dotnet ef migrations add</c>), que no necesitan conectarse a la base de datos.
/// La cadena no lleva credenciales a propósito: la real la da Aspire en ejecución.
/// </summary>
internal sealed class OpoStudyDbContextFactory : IDesignTimeDbContextFactory<OpoStudyDbContext>
{
    public OpoStudyDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<OpoStudyDbContext>().UseNpgsql("Host=localhost;Database=opostudy");
        OpoStudyDbContext.AplicarConvenciones(builder);
        return new OpoStudyDbContext(builder.Options);
    }
}