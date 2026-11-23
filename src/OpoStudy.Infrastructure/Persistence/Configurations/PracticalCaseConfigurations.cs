using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OpoStudy.Domain.Common;
using OpoStudy.Domain.Exams;
using OpoStudy.Domain.PracticalCases;

namespace OpoStudy.Infrastructure.Persistence.Configurations;

internal sealed class PracticalCaseConfiguration : IEntityTypeConfiguration<PracticalCase>
{
    public void Configure(EntityTypeBuilder<PracticalCase> builder)
    {
        builder.ToTable("practical_cases");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Key).HasMaxLength(Limits.Key);
        builder.Property(c => c.Title).HasMaxLength(Limits.Name);
        builder.Property(c => c.Statement).HasMaxLength(Limits.CaseStatement);
        builder.Ignore(c => c.MaxPoints);

        builder.HasOne<Exam>().WithMany().HasForeignKey(c => c.ExamId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => c.Key).IsUnique();
        builder.HasIndex(c => c.ExamId);

        builder.HasMany(c => c.Parts).WithOne().HasForeignKey(p => p.PracticalCaseId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Parts).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
    }
}

internal sealed class PracticalCasePartConfiguration : IEntityTypeConfiguration<PracticalCasePart>
{
    public void Configure(EntityTypeBuilder<PracticalCasePart> builder)
    {
        builder.ToTable("practical_case_parts");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Statement).HasMaxLength(Limits.Statement);
        builder.Property(p => p.ModelAnswer).HasMaxLength(Limits.ModelAnswer);
        builder.HasIndex(p => new { p.PracticalCaseId, p.Order }).IsUnique();

        // Criteria live in a jsonb column; the length of each description is enforced by the domain.
        builder.OwnsMany(p => p.Criteria, criteria => criteria.ToJson("criteria"));
        builder.Navigation(p => p.Criteria).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}