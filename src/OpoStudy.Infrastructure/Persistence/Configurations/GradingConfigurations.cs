using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OpoStudy.Domain.Common;
using OpoStudy.Domain.Gradings;
using OpoStudy.Domain.PracticalCases;
using OpoStudy.Domain.Users;

namespace OpoStudy.Infrastructure.Persistence.Configurations;

internal sealed class GradingConfiguration : IEntityTypeConfiguration<Grading>
{
    public void Configure(EntityTypeBuilder<Grading> builder)
    {
        builder.ToTable("gradings");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).ValueGeneratedNever();
        builder.Property(g => g.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(g => g.Score).HasPrecision(5, 2);
        builder.Property(g => g.Feedback).HasMaxLength(Limits.GradingFeedback);
        builder.Property(g => g.FailureCode).HasMaxLength(Limits.FailureCode);
        builder.Property(g => g.MetDailyGoal).HasDefaultValue(false);
        builder.Property(g => g.Corrector).HasMaxLength(Limits.Corrector);

        // Optimistic concurrency with the PostgreSQL system column xmin (no real column: see the migration).
        builder.Property<uint>("Version").IsRowVersion();

        // Deleting an account deletes its gradings (GDPR); the content is never deleted in cascade.
        builder.HasOne<User>().WithMany().HasForeignKey(g => g.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<PracticalCase>().WithMany().HasForeignKey(g => g.PracticalCaseId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(g => new { g.UserId, g.SubmittedAt }).IsDescending(false, true);
        builder.HasIndex(g => new { g.Status, g.NextAttemptAt });

        // At most one grading in progress per user, even with concurrent submissions.
        builder.HasIndex(g => g.UserId)
            .IsUnique()
            .HasDatabaseName(DbConstraintNames.GradingsOneInProgressPerUserIndex)
            .HasFilter("status IN ('Pending', 'Processing')");

        builder.HasMany(g => g.Answers).WithOne().HasForeignKey(a => a.GradingId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(g => g.Answers).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
    }
}

internal sealed class GradingAnswerConfiguration : IEntityTypeConfiguration<GradingAnswer>
{
    public void Configure(EntityTypeBuilder<GradingAnswer> builder)
    {
        builder.ToTable("grading_answers");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Text).HasMaxLength(Limits.PartAnswer);
        builder.Property(a => a.Score).HasPrecision(5, 2);
        builder.Property(a => a.Feedback).HasMaxLength(Limits.GradingFeedback);
        builder.HasIndex(a => new { a.GradingId, a.Order }).IsUnique();

        // The rubric copy lives in a jsonb column; the lengths are enforced by the domain.
        builder.OwnsMany(a => a.Criteria, criteria => criteria.ToJson("criteria"));
        builder.Navigation(a => a.Criteria).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
