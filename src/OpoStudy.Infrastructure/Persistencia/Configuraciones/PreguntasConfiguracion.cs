using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OpoStudy.Domain.Comun;
using OpoStudy.Domain.Oposiciones;
using OpoStudy.Domain.Preguntas;

namespace OpoStudy.Infrastructure.Persistencia.Configuraciones;

internal sealed class PreguntaConfiguracion : IEntityTypeConfiguration<Pregunta>
{
    public void Configure(EntityTypeBuilder<Pregunta> builder)
    {
        builder.ToTable("preguntas");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Clave).HasMaxLength(Limites.Clave);
        builder.Property(p => p.Enunciado).HasMaxLength(Limites.Enunciado);
        builder.Property(p => p.Aclaracion).HasMaxLength(Limites.Aclaracion);
        builder.Property(p => p.ReferenciaLegal).HasMaxLength(Limites.ReferenciaLegal);
        builder.Ignore(p => p.OpcionCorrecta);

        builder.HasOne<Tema>().WithMany().HasForeignKey(p => p.TemaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => p.Clave).IsUnique();
        builder.HasIndex(p => p.TemaId);

        builder.HasMany(p => p.Opciones).WithOne().HasForeignKey(o => o.PreguntaId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Opciones).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
    }
}

internal sealed class OpcionConfiguracion : IEntityTypeConfiguration<Opcion>
{
    public void Configure(EntityTypeBuilder<Opcion> builder)
    {
        builder.ToTable("opciones");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.Texto).HasMaxLength(Limites.TextoOpcion);
        builder.HasIndex(o => new { o.PreguntaId, o.Orden }).IsUnique();
    }
}