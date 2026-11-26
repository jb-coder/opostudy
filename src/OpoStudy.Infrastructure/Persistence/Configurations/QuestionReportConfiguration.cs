using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OpoStudy.Domain.Questions;
using OpoStudy.Domain.Users;

namespace OpoStudy.Infrastructure.Persistence.Configurations;

internal sealed class QuestionReportConfiguration : IEntityTypeConfiguration<QuestionReport>
{
    public void Configure(EntityTypeBuilder<QuestionReport> builder)
    {
        builder.ToTable("question_reports");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Reason).HasConversion<string>().HasMaxLength(30);
        builder.Property(r => r.Comment).HasMaxLength(QuestionReport.MaxCommentLength);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);

        // At most one open report per user and question, even with concurrent requests.
        builder.HasIndex(r => new { r.UserId, r.QuestionId })
            .IsUnique()
            .HasDatabaseName(DbConstraintNames.QuestionReportsOneOpenPerUserIndex)
            .HasFilter("status = 'Open'");

        // Review queue: WHERE status = 'Open' ORDER BY created_at
        builder.HasIndex(r => new { r.Status, r.CreatedAt });

        // Deleting an account (GDPR) or a question deletes its reports.
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Question>().WithMany().HasForeignKey(r => r.QuestionId).OnDelete(DeleteBehavior.Cascade);
    }
}