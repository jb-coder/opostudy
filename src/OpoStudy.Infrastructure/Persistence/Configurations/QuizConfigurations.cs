using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OpoStudy.Domain.Exams;
using OpoStudy.Domain.Questions;
using OpoStudy.Domain.Quizzes;
using OpoStudy.Domain.Users;

namespace OpoStudy.Infrastructure.Persistence.Configurations;

internal sealed class QuizAttemptConfiguration : IEntityTypeConfiguration<QuizAttempt>
{
    public void Configure(EntityTypeBuilder<QuizAttempt> builder)
    {
        builder.ToTable("quiz_attempts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Mode).HasConversion<string>().HasMaxLength(20);

        // Optimistic concurrency with the PostgreSQL system column xmin (no real column: see the migration).
        // Every Answer increments Revision, so every answer modifies the row and concurrent answers to the same attempt conflict.
        builder.Property<uint>("Version").IsRowVersion();
        builder.Ignore(a => a.IsFinished);
        builder.Ignore(a => a.CorrectCount);
        builder.Ignore(a => a.IncorrectCount);

        // Deleting an account deletes its history (GDPR); content is never deleted in cascade.
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Exam>().WithMany().HasForeignKey(a => a.ExamId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Topic>().WithMany().HasForeignKey(a => a.TopicId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => new { a.UserId, a.StartedAt });

        builder.HasMany(a => a.Answers).WithOne().HasForeignKey(a => a.QuizAttemptId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(a => a.Answers).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
    }
}

internal sealed class QuizAnswerConfiguration : IEntityTypeConfiguration<QuizAnswer>
{
    public void Configure(EntityTypeBuilder<QuizAnswer> builder)
    {
        builder.ToTable("quiz_answers");
        builder.HasKey(a => new { a.QuizAttemptId, a.QuestionId });
        builder.Ignore(a => a.IsAnswered);
        builder.HasIndex(a => new { a.QuizAttemptId, a.Order }).IsUnique();

        builder.HasOne<Question>().WithMany().HasForeignKey(a => a.QuestionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AnswerOption>().WithMany().HasForeignKey(a => a.SelectedOptionId).OnDelete(DeleteBehavior.Restrict);
    }
}