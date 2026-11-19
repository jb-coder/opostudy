using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OpoStudy.Domain.Comun;
using OpoStudy.Domain.Oposiciones;
using OpoStudy.Domain.Usuarios;

namespace OpoStudy.Infrastructure.Persistencia.Configuraciones;

internal sealed class UsuarioConfiguracion : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuarios");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();
        builder.Property(u => u.FirebaseUid).HasMaxLength(Limites.FirebaseUid);
        builder.HasIndex(u => u.FirebaseUid).IsUnique();

        builder.HasOne<Oposicion>().WithMany().HasForeignKey(u => u.OposicionActivaId).OnDelete(DeleteBehavior.SetNull);
    }
}