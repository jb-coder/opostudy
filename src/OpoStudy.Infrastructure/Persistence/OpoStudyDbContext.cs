using Microsoft.EntityFrameworkCore;

using OpoStudy.Domain.Exams;
using OpoStudy.Domain.Questions;
using OpoStudy.Domain.Quizzes;
using OpoStudy.Domain.Users;

namespace OpoStudy.Infrastructure.Persistence;

public sealed class OpoStudyDbContext(DbContextOptions<OpoStudyDbContext> options) : DbContext(options)
{
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<User> Users => Set<User>();
    public DbSet<QuizAttempt> QuizAttempts => Set<QuizAttempt>();

    /// <summary>
    /// Shared conventions (snake_case tables and columns). Applied when registering the context because Aspire
    /// uses DbContext pooling, which does not allow <c>OnConfiguring</c>. Use it in the app, the tests and the EF tools.
    /// </summary>
    public static void ApplyConventions(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        optionsBuilder.UseSnakeCaseNamingConvention();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OpoStudyDbContext).Assembly);
    }
}