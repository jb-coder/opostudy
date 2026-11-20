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
            (await db.Set<QuizAnswer>().AnyAsync(a => a.QuizAttemptId == attempt.Id, Ct)).ShouldBeFalse();
            (await db.Questions.AnyAsync(q => q.Id == questions[0].Id, Ct)).ShouldBeTrue();
        }
    }

    [Fact]
    public async Task Question_ReloadedWithSixOptions_ExposesOptionsOrderedByOrder()
    {
        var (_, topic, _) = await CreateContentAsync(questionCount: 0);
        AnswerOptionData[] data = [.. Enumerable.Range(0, 6).Select(i => new AnswerOptionData($"Option {i}", i == 3))];
        var question = Question.Create(topic.Id, Unique("key"), "Statement?", "Explanation", "CE art. 1", data);
        await using (var db = postgres.CreateContext())
        {
            db.Questions.Add(question);
            await db.SaveChangesAsync(Ct);
        }

        await using var read = postgres.CreateContext();
        var loaded = await read.Questions.SingleAsync(q => q.Id == question.Id, Ct);

        loaded.Options.Select(o => o.Order).ShouldBe([0, 1, 2, 3, 4, 5]);
        loaded.Options.Select(o => o.Text).ShouldBe(["Option 0", "Option 1", "Option 2", "Option 3", "Option 4", "Option 5"]);
    }

    [Fact]
    public async Task QuizAttempt_ReloadedWithManyQuestions_ExposesAnswersOrderedByOrder()
    {
        var (exam, _, questions) = await CreateContentAsync(questionCount: 10);
        var user = User.Create(Unique("uid"), Now);
        var ids = questions.Select(q => q.Id).Reverse().ToList(); // reversed: Order differs from primary key order
        var attempt = QuizAttempt.Create(user.Id, exam.Id, null, QuizMode.Random, ids, Now);
        await using (var db = postgres.CreateContext())
        {
            db.Users.Add(user);
            db.QuizAttempts.Add(attempt);
            await db.SaveChangesAsync(Ct);
        }

        await using var read = postgres.CreateContext();
        var loaded = await read.QuizAttempts.SingleAsync(a => a.Id == attempt.Id, Ct);

        loaded.Answers.Select(a => a.Order).ShouldBe([.. Enumerable.Range(0, 10)]);
        loaded.Answers.Select(a => a.QuestionId).ShouldBe(ids);
    }

    [Fact]
    public async Task QuizAttempt_AnsweredConcurrentlyFromTwoContexts_SecondSaveThrowsConcurrencyException()
    {
        var (exam, _, questions) = await CreateContentAsync(questionCount: 2);
        var user = User.Create(Unique("uid"), Now);
        var attempt = QuizAttempt.Create(user.Id, exam.Id, null, QuizMode.Random, [.. questions.Select(q => q.Id)], Now);
        await using (var db = postgres.CreateContext())
        {
            db.Users.Add(user);
            db.QuizAttempts.Add(attempt);
            await db.SaveChangesAsync(Ct);
        }

        await using var first = postgres.CreateContext();
        await using var second = postgres.CreateContext();
        var attempt1 = await first.QuizAttempts.SingleAsync(a => a.Id == attempt.Id, Ct);
        var attempt2 = await second.QuizAttempts.SingleAsync(a => a.Id == attempt.Id, Ct);
        var q1 = await first.Questions.SingleAsync(q => q.Id == questions[0].Id, Ct);
        var q2 = await second.Questions.SingleAsync(q => q.Id == questions[1].Id, Ct);
        attempt1.Answer(q1, q1.CorrectOption.Id, Now.AddSeconds(1));
        attempt2.Answer(q2, q2.CorrectOption.Id, Now.AddSeconds(2));

        await first.SaveChangesAsync(Ct);

        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Question_DeletedWhileReferencedByAnAnswer_ViolatesForeignKey()
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

        await using var delete = postgres.CreateContext();
        var ex = await Should.ThrowAsync<PostgresException>(
            () => delete.Questions.Where(q => q.Id == questions[0].Id).ExecuteDeleteAsync(Ct));

        ex.SqlState.ShouldBe(PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task Exam_DeletedWithTopics_ViolatesForeignKey()
    {
        var (exam, _, _) = await CreateContentAsync(questionCount: 0);

        await using var delete = postgres.CreateContext();
        var ex = await Should.ThrowAsync<PostgresException>(
            () => delete.Exams.Where(e => e.Id == exam.Id).ExecuteDeleteAsync(Ct));

        ex.SqlState.ShouldBe(PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task Exam_WithDuplicateSlug_ViolatesUniqueIndex()
    {
        var (exam, _, _) = await CreateContentAsync(questionCount: 0);
        await using var db = postgres.CreateContext();
        db.Exams.Add(Exam.Create(exam.Slug, "Other", "Other", "C1"));

        var ex = await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));

        ex.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task Topic_WithDuplicateSlugInSameExam_ViolatesUniqueIndex()
    {
        var (exam, topic, _) = await CreateContentAsync(questionCount: 0);
        await using var db = postgres.CreateContext();
        db.Topics.Add(Topic.Create(exam.Id, topic.Slug, 2, "Other", null));

        var ex = await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));

        ex.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task Topic_WithDuplicateNumberInSameExam_ViolatesUniqueIndex()
    {
        var (exam, topic, _) = await CreateContentAsync(questionCount: 0);
        await using var db = postgres.CreateContext();
        db.Topics.Add(Topic.Create(exam.Id, "topic-2", topic.Number, "Other", null));

        var ex = await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));

        ex.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task Question_UpdatedAndReloaded_KeepsOptionIdsAndNewValues()
    {
        var (_, _, questions) = await CreateContentAsync(questionCount: 1);
        var originalIds = questions[0].Options.Select(o => o.Id).ToList();
        await using (var db = postgres.CreateContext())
        {
            var question = await db.Questions.SingleAsync(q => q.Id == questions[0].Id, Ct);
            question.Update(question.TopicId, "New statement?", "New explanation", "CE art. 2",
                [new("W", false), new("X", false), new("Y", true), new("Z", false)]);
            await db.SaveChangesAsync(Ct);
        }

        await using var read = postgres.CreateContext();
        var loaded = await read.Questions.SingleAsync(q => q.Id == questions[0].Id, Ct);

        loaded.Statement.ShouldBe("New statement?");
        loaded.Explanation.ShouldBe("New explanation");
        loaded.LegalReference.ShouldBe("CE art. 2");
        loaded.Options.Select(o => o.Text).ShouldBe(["W", "X", "Y", "Z"]);
        loaded.CorrectOption.Order.ShouldBe(2);
        loaded.Options.Select(o => o.Id).ShouldBe(originalIds);
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