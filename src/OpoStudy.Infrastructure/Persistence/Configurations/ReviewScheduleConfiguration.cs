using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OpoStudy.Domain.Questions;
using OpoStudy.Domain.Quizzes;
using OpoStudy.Domain.Users;

namespace OpoStudy.Infrastructure.Persistence.Configurations;

internal sealed class ReviewScheduleConfiguration : IEntityTypeConfiguration<ReviewSchedule>
{
    public void Configure(EntityTypeBuilder<ReviewSchedule> builder)
    {
        builder.ToTable("review_schedules");
        builder.HasKey(r => new { r.UserId, r.QuestionId }).HasName(DbConstraintNames.ReviewSchedulesPrimaryKey);

        // Pending reviews of a user: WHERE user_id = ? AND due_on <= ?
        builder.HasIndex(r => new { r.UserId, r.DueOn });

        // Optimistic concurrency with the PostgreSQL system column xmin (no real column: see the migration).
        builder.Property<uint>("Version").IsRowVersion();

        // Deleting an account (GDPR) or a question deletes its schedule.
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Question>().WithMany().HasForeignKey(r => r.QuestionId).OnDelete(DeleteBehavior.Cascade);
    }
}
