using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OpoStudy.Domain.Common;
using OpoStudy.Domain.Exams;

namespace OpoStudy.Infrastructure.Persistence.Configurations;

internal sealed class ExamConfiguration : IEntityTypeConfiguration<Exam>
{
    public void Configure(EntityTypeBuilder<Exam> builder)
    {
        builder.ToTable("exams");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Slug).HasMaxLength(Limits.Slug);
        builder.Property(e => e.Name).HasMaxLength(Limits.Name);
        builder.Property(e => e.Organization).HasMaxLength(Limits.Name);
        builder.Property(e => e.Group).HasMaxLength(Limits.Group);
        builder.HasIndex(e => e.Slug).IsUnique();
    }
}

internal sealed class ExamExerciseConfiguration : IEntityTypeConfiguration<ExamExercise>
{
    public void Configure(EntityTypeBuilder<ExamExercise> builder)
    {
        builder.ToTable("exam_exercises");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Title).HasMaxLength(Limits.Name);
        builder.Property(e => e.MaxScore).HasPrecision(5, 2);
        builder.Property(e => e.PenaltyRatio).HasPrecision(3, 2);
        builder.Property(e => e.Sections).HasColumnType("text[]");
        builder.Ignore(e => e.DefaultPassScore);

        builder.HasOne<Exam>().WithMany().HasForeignKey(e => e.ExamId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.ExamId, e.Number }).IsUnique();
    }
}

internal sealed class TopicConfiguration : IEntityTypeConfiguration<Topic>
{
    public void Configure(EntityTypeBuilder<Topic> builder)
    {
        builder.ToTable("topics");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Slug).HasMaxLength(Limits.Slug);
        builder.Property(t => t.Title).HasMaxLength(Limits.Name);
        builder.Property(t => t.Section).HasMaxLength(Limits.Name);

        builder.HasOne<Exam>().WithMany().HasForeignKey(t => t.ExamId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(t => new { t.ExamId, t.Slug }).IsUnique();
        builder.HasIndex(t => new { t.ExamId, t.Number }).IsUnique();
    }
}