using Microsoft.EntityFrameworkCore;

using OpoStudy.Domain.Intentos;
using OpoStudy.Domain.Oposiciones;
using OpoStudy.Domain.Preguntas;
using OpoStudy.Domain.Usuarios;

namespace OpoStudy.Infrastructure.Persistencia;

public sealed class OpoStudyDbContext(DbContextOptions<OpoStudyDbContext> options) : DbContext(options)
{
    public DbSet<Oposicion> Oposiciones => Set<Oposicion>();
    public DbSet<Tema> Temas => Set<Tema>();
    public DbSet<Pregunta> Preguntas => Set<Pregunta>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<IntentoTest> IntentosTest => Set<IntentoTest>();

    /// <summary>
    /// Convenciones comunes (tablas y columnas en snake_case). Se aplican al registrar el contexto, porque
    /// Aspire usa un pool de DbContext y el pool no admite <c>OnConfiguring</c>. Úsalo en la app, los tests y las herramientas.
    /// </summary>
    public static void AplicarConvenciones(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        optionsBuilder.UseSnakeCaseNamingConvention();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OpoStudyDbContext).Assembly);
    }
}