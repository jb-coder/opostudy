using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OpoStudy.Domain.Intentos;
using OpoStudy.Domain.Oposiciones;
using OpoStudy.Domain.Preguntas;
using OpoStudy.Domain.Usuarios;

namespace OpoStudy.Infrastructure.Persistencia.Configuraciones;

internal sealed class IntentoTestConfiguracion : IEntityTypeConfiguration<IntentoTest>
{
    public void Configure(EntityTypeBuilder<IntentoTest> builder)
    {
        builder.ToTable("intentos_test");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.Modo).HasConversion<string>().HasMaxLength(20);
        builder.Ignore(i => i.EstaFinalizado);
        builder.Ignore(i => i.Aciertos);
        builder.Ignore(i => i.Fallos);

        // Al borrar la cuenta se borra su historial (RGPD); el contenido nunca se borra en cascada.
        builder.HasOne<Usuario>().WithMany().HasForeignKey(i => i.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Oposicion>().WithMany().HasForeignKey(i => i.OposicionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tema>().WithMany().HasForeignKey(i => i.TemaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(i => new { i.UsuarioId, i.FechaInicio });

        builder.HasMany(i => i.Respuestas).WithOne().HasForeignKey(r => r.IntentoTestId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(i => i.Respuestas).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
    }
}

internal sealed class RespuestaUsuarioConfiguracion : IEntityTypeConfiguration<RespuestaUsuario>
{
    public void Configure(EntityTypeBuilder<RespuestaUsuario> builder)
    {
        builder.ToTable("respuestas_usuario");
        builder.HasKey(r => new { r.IntentoTestId, r.PreguntaId });
        builder.Ignore(r => r.EstaRespondida);
        builder.HasIndex(r => new { r.IntentoTestId, r.Orden }).IsUnique();

        builder.HasOne<Pregunta>().WithMany().HasForeignKey(r => r.PreguntaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Opcion>().WithMany().HasForeignKey(r => r.OpcionElegidaId).OnDelete(DeleteBehavior.Restrict);
    }
}