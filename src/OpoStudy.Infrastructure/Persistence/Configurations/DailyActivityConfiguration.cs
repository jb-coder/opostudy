using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OpoStudy.Domain.Users;

namespace OpoStudy.Infrastructure.Persistence.Configurations;

internal sealed class DailyActivityConfiguration : IEntityTypeConfiguration<DailyActivity>
{
    public void Configure(EntityTypeBuilder<DailyActivity> builder)
    {
        builder.ToTable("daily_activities");
        builder.HasKey(a => new { a.UserId, a.Date }).HasName(DbConstraintNames.DailyActivitiesPrimaryKey);
        builder.Ignore(a => a.IsGoalMet);
        builder.Ignore(a => a.IsProtected);
        builder.Property(a => a.XpEarned).HasDefaultValue(0);

        // Optimistic concurrency with the PostgreSQL system column xmin (no real column: see the migration).
        builder.Property<uint>("Version").IsRowVersion();

        // Deleting an account deletes its activity (GDPR).
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}