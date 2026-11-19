using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OpoStudy.Domain.Common;
using OpoStudy.Domain.Exams;
using OpoStudy.Domain.Users;

namespace OpoStudy.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();
        builder.Property(u => u.FirebaseUid).HasMaxLength(Limits.FirebaseUid);
        builder.HasIndex(u => u.FirebaseUid).IsUnique();

        builder.HasOne<Exam>().WithMany().HasForeignKey(u => u.ActiveExamId).OnDelete(DeleteBehavior.SetNull);
    }
}