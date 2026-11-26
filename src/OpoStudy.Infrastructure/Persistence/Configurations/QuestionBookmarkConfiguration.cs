using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OpoStudy.Domain.Questions;
using OpoStudy.Domain.Users;

namespace OpoStudy.Infrastructure.Persistence.Configurations;

internal sealed class QuestionBookmarkConfiguration : IEntityTypeConfiguration<QuestionBookmark>
{
    public void Configure(EntityTypeBuilder<QuestionBookmark> builder)
    {
        builder.ToTable("question_bookmarks");
        builder.HasKey(b => new { b.UserId, b.QuestionId }).HasName(DbConstraintNames.QuestionBookmarksPrimaryKey);

        // Deleting an account (GDPR) or a question deletes its bookmarks.
        builder.HasOne<User>().WithMany().HasForeignKey(b => b.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Question>().WithMany().HasForeignKey(b => b.QuestionId).OnDelete(DeleteBehavior.Cascade);
    }
}