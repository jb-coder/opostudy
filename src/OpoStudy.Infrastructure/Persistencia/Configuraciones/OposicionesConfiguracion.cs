using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OpoStudy.Domain.Comun;
using OpoStudy.Domain.Oposiciones;

namespace OpoStudy.Infrastructure.Persistencia.Configuraciones;

internal sealed class OposicionConfiguracion : IEntityTypeConfiguration<Oposicion>
{
    public void Configure(EntityTypeBuilder<Oposicion> builder)
    {
        builder.ToTable("oposiciones");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.Slug).HasMaxLength(Limites.Slug);
        builder.Property(o => o.Nombre).HasMaxLength(Limites.Nombre);
        builder.Property(o => o.Organismo).HasMaxLength(Limites.Nombre);
        builder.Property(o => o.Grupo).HasMaxLength(Limites.Grupo);
        builder.HasIndex(o => o.Slug).IsUnique();
    }
}

internal sealed class TemaConfiguracion : IEntityTypeConfiguration<Tema>
{
    public void Configure(EntityTypeBuilder<Tema> builder)
    {
        builder.ToTable("temas");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Slug).HasMaxLength(Limites.Slug);
        builder.Property(t => t.Titulo).HasMaxLength(Limites.Nombre);
        builder.Property(t => t.Bloque).HasMaxLength(Limites.Nombre);

        builder.HasOne<Oposicion>().WithMany().HasForeignKey(t => t.OposicionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(t => new { t.OposicionId, t.Slug }).IsUnique();
        builder.HasIndex(t => new { t.OposicionId, t.Numero }).IsUnique();
    }
}