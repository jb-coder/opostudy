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
        builder.Property(u => u.DailyGoal).HasDefaultValue(Limits.DefaultDailyGoal);
        builder.Property(u => u.TimeZone).HasMaxLength(Limits.TimeZone).HasDefaultValue(Limits.DefaultTimeZone);
        builder.Property(u => u.Xp).HasDefaultValue(0);
        builder.Property(u => u.XpSpent).HasDefaultValue(0);
        builder.Ignore(u => u.SpendableXp);
        builder.Ignore(u => u.Level);

        // Optimistic concurrency with the PostgreSQL system column xmin (no real column: see the migration).
        builder.Property<uint>("Version").IsRowVersion();
        builder.ComplexProperty(u => u.Streak, streak =>
        {
            streak.Property(s => s.Longest).HasColumnName("streak_longest").HasDefaultValue(0);
            streak.Property(s => s.LastLength).HasColumnName("streak_last_length").HasDefaultValue(0);
            streak.Property(s => s.LastMetOn).HasColumnName("streak_last_met_on");
            streak.Property(s => s.Freezes).HasColumnName("streak_freezes").HasDefaultValue(0);
            streak.Property(s => s.ProtectedThrough).HasColumnName("streak_protected_through");
        });

        builder.HasOne<Exam>().WithMany().HasForeignKey(u => u.ActiveExamId).OnDelete(DeleteBehavior.SetNull);
    }
}