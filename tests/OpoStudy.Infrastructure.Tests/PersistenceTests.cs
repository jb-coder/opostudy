using Microsoft.EntityFrameworkCore;

using Npgsql;

using OpoStudy.Domain.Exams;
using OpoStudy.Domain.Questions;
using OpoStudy.Domain.Quizzes;
using OpoStudy.Domain.Users;

using Shouldly;

namespace OpoStudy.Infrastructure.Tests;

/// <summary>Each test creates its own data with unique slugs and keys, so tests never interfere with each other.</summary>
public sealed class PersistenceTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Model_HasNoPendingChangesToMigrate()
    {
        await using var db = postgres.CreateContext();

        db.Database.HasPendingModelChanges().ShouldBeFalse();
    }

    [Fact]
    public async Task QuizAttempt_SavedAndReloaded_KeepsAnswersAndOptions()
    {
        var (exam, topic, questions) = await CreateContentAsync(questionCount: 2);
        var user = User.Create(Unique("uid"), Now);
        var attempt = QuizAttempt.Create(user.Id, exam.Id, topic.Id, QuizMode.ByTopic, [.. questions.Select(q => q.Id)], Now);
        attempt.Answer(questions[0], questions[0].CorrectOption.Id, Now);
        await using (var db = postgres.CreateContext())
        {
            db.Users.Add(user);
            db.QuizAttempts.Add(attempt);
            await db.SaveChangesAsync(Ct);
        }

        await using (var db = postgres.CreateContext())
        {
            var loaded = await db.QuizAttempts.SingleAsync(a => a.Id == attempt.Id, Ct);
            loaded.Mode.ShouldBe(QuizMode.ByTopic);
            loaded.Answers.Count.ShouldBe(2);
            loaded.CorrectCount.ShouldBe(1);
            loaded.IsFinished.ShouldBeFalse();

            var question = await db.Questions.SingleAsync(q => q.Id == questions[1].Id, Ct);
            question.Options.Count.ShouldBe(4);
            loaded.Answer(question, question.CorrectOption.Id, Now);
            await db.SaveChangesAsync(Ct);
        }

        await using (var db = postgres.CreateContext())
        {
            var final = await db.QuizAttempts.SingleAsync(a => a.Id == attempt.Id, Ct);
            final.IsFinished.ShouldBeTrue();
            final.CorrectCount.ShouldBe(2);
        }
    }

    [Fact]
    public async Task Question_WithDuplicateKey_ViolatesUniqueIndex()
    {
        var (_, topic, questions) = await CreateContentAsync(questionCount: 1);
        await using var db = postgres.CreateContext();
        db.Questions.Add(NewQuestion(topic.Id, questions[0].Key));

        var ex = await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));

        ex.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task User_WithDuplicateFirebaseUid_ViolatesUniqueIndex()
    {
        var uid = Unique("uid");
        await using var db = postgres.CreateContext();
        db.Users.AddRange(User.Create(uid, Now), User.Create(uid, Now));

        var ex = await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));

        ex.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task User_WhenDeleted_CascadesToAttemptsAndAnswers()
    {
        var (exam, _, questions) = await CreateContentAsync(questionCount: 1);
        var user = User.Create(Unique("uid"), Now);
        var attempt = QuizAttempt.Create(user.Id, exam.Id, null, QuizMode.Random, [questions[0].Id], Now);
        await using (var db = postgres.CreateContext())
        {
            db.Users.Add(user);
            db.QuizAttempts.Add(attempt);
            await db.SaveChangesAsync(Ct);
        }

        await using (var db = postgres.CreateContext())
        {
            await db.Users.Where(u => u.Id == user.Id).ExecuteDeleteAsync(Ct);
        }

        await using (var db = postgres.CreateContext())
        {
            (await db.QuizAttempts.AnyAsync(a => a.Id == attempt.Id, Ct)).ShouldBeFalse();
            (await db.Questions.AnyAsync(q => q.Id == questions[0].Id, Ct)).ShouldBeTrue();
        }
    }

    private async Task<(Exam Exam, Topic Topic, List<Question> Questions)> CreateContentAsync(int questionCount)
    {
        var exam = Exam.Create(Unique("exam"), "Programador", "Diputación de Málaga", "C1");
        var topic = Topic.Create(exam.Id, "topic-1", 1, "Constitución", "Bloque I");
        var questions = Enumerable.Range(0, questionCount).Select(_ => NewQuestion(topic.Id, Unique("key"))).ToList();

        await using var db = postgres.CreateContext();
        db.Exams.Add(exam);
        db.Topics.Add(topic);
        db.Questions.AddRange(questions);
        await db.SaveChangesAsync(Ct);
        return (exam, topic, questions);
    }

    private static Question NewQuestion(Guid topicId, string key) =>
        Question.Create(topicId, key, "Statement?", "Explanation", "CE art. 1",
            [new("A", true), new("B", false), new("C", false), new("D", false)]);

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";
}