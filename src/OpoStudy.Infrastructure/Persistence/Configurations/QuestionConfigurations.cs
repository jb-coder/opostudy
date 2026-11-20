using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OpoStudy.Domain.Common;
using OpoStudy.Domain.Exams;
using OpoStudy.Domain.Questions;

namespace OpoStudy.Infrastructure.Persistence.Configurations;

internal sealed class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable("questions");
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).ValueGeneratedNever();
        builder.Property(q => q.Key).HasMaxLength(Limits.Key);
        builder.Property(q => q.Statement).HasMaxLength(Limits.Statement);
        builder.Property(q => q.Explanation).HasMaxLength(Limits.Explanation);
        builder.Property(q => q.LegalReference).HasMaxLength(Limits.LegalReference);
        builder.Ignore(q => q.CorrectOption);

        builder.HasOne<Topic>().WithMany().HasForeignKey(q => q.TopicId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(q => q.Key).IsUnique();
        builder.HasIndex(q => q.TopicId);

        builder.HasMany(q => q.Options).WithOne().HasForeignKey(o => o.QuestionId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(q => q.Options).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
    }
}

internal sealed class AnswerOptionConfiguration : IEntityTypeConfiguration<AnswerOption>
{
    public void Configure(EntityTypeBuilder<AnswerOption> builder)
    {
        builder.ToTable("answer_options");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.Text).HasMaxLength(Limits.OptionText);
        builder.HasIndex(o => new { o.QuestionId, o.Order }).IsUnique();
    }
}