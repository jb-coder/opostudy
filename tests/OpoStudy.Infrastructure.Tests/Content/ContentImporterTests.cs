using Microsoft.EntityFrameworkCore;

using OpoStudy.Domain.Questions;
using OpoStudy.Infrastructure.Content;

using Shouldly;

namespace OpoStudy.Infrastructure.Tests.Content;

/// <summary>Each test uses its own exam slug (and keys derived from it), so deactivations never touch other tests.</summary>
public sealed class ContentImporterTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueSlug() => "t" + Guid.NewGuid().ToString("N")[..12];

    private static QuestionContent Question(string slug, int number, string statement = "¿Pregunta?", int optionCount = 3)
    {
        var options = Enumerable.Range(0, optionCount).Select(i => new AnswerOptionData("Opción " + i, i == 0)).ToList();
        return new QuestionContent($"{slug}/uno/{number:0000}", statement, "Porque sí.", "CE, art. 1", options);
    }

    private static ExamContent Exam(string slug, params QuestionContent[] questions) =>
        new(slug, "Programador", "Diputación", "C1", [new TopicContent("uno", 1, "Tema uno", "Bloque I", questions)]);

    private async Task<ImportReport> ImportAsync(ExamContent exam, bool dryRun = false)
    {
        await using var db = postgres.CreateContext();
        return await new ContentImporter(db).ImportAsync([exam], dryRun, Ct);
    }

    [Fact]
    public async Task ImportAsync_FirstImport_CreatesEverything()
    {
        var slug = UniqueSlug();

        var report = await ImportAsync(Exam(slug, Question(slug, 1), Question(slug, 2)));

        report.Exams.Created.ShouldBe(1);
        report.Topics.Created.ShouldBe(1);
        report.Questions.Created.ShouldBe(2);
        await using var db = postgres.CreateContext();
        (await db.Questions.CountAsync(q => q.Key.StartsWith(slug), Ct)).ShouldBe(2);
        (await db.Exams.SingleAsync(e => e.Slug == slug, Ct)).IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task ImportAsync_SameContentTwice_ChangesNothing()
    {
        var slug = UniqueSlug();
        var exam = Exam(slug, Question(slug, 1));
        await ImportAsync(exam);

        var report = await ImportAsync(exam);

        report.Exams.ShouldBe(new ImportCounts(0, 0, 0, 0));
        report.Topics.ShouldBe(new ImportCounts(0, 0, 0, 0));
        report.Questions.ShouldBe(new ImportCounts(0, 0, 0, 0));
        await using var db = postgres.CreateContext();
        (await db.Questions.CountAsync(q => q.Key.StartsWith(slug), Ct)).ShouldBe(1);
        (await db.Exams.CountAsync(e => e.Slug == slug, Ct)).ShouldBe(1);
    }

    [Fact]
    public async Task ImportAsync_ModifiedQuestion_UpdatesItKeepingTheId()
    {
        var slug = UniqueSlug();
        await ImportAsync(Exam(slug, Question(slug, 1)));
        await using var before = postgres.CreateContext();
        var id = (await before.Questions.SingleAsync(q => q.Key == $"{slug}/uno/0001", Ct)).Id;

        var report = await ImportAsync(Exam(slug, Question(slug, 1, statement: "¿Otra pregunta?")));

        report.Questions.ShouldBe(new ImportCounts(0, 1, 0, 0));
        await using var db = postgres.CreateContext();
        var question = await db.Questions.SingleAsync(q => q.Key == $"{slug}/uno/0001", Ct);
        question.Id.ShouldBe(id);
        question.Statement.ShouldBe("¿Otra pregunta?");
    }

    [Fact]
    public async Task ImportAsync_QuestionRemovedAndRestored_IsDeactivatedThenReactivated()
    {
        var slug = UniqueSlug();
        await ImportAsync(Exam(slug, Question(slug, 1), Question(slug, 2)));

        var removed = await ImportAsync(Exam(slug, Question(slug, 1)));

        removed.Questions.ShouldBe(new ImportCounts(0, 0, 1, 0));
        await using (var db = postgres.CreateContext())
        {
            (await db.Questions.SingleAsync(q => q.Key == $"{slug}/uno/0002", Ct)).IsActive.ShouldBeFalse();
            (await db.Questions.SingleAsync(q => q.Key == $"{slug}/uno/0001", Ct)).IsActive.ShouldBeTrue();
        }

        var restored = await ImportAsync(Exam(slug, Question(slug, 1), Question(slug, 2)));

        restored.Questions.ShouldBe(new ImportCounts(0, 0, 0, 1));
        await using (var db = postgres.CreateContext())
        {
            (await db.Questions.SingleAsync(q => q.Key == $"{slug}/uno/0002", Ct)).IsActive.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task ImportAsync_OptionCountChanged_AbortsWithoutPartialChanges()
    {
        var slug = UniqueSlug();
        await ImportAsync(Exam(slug, Question(slug, 1, optionCount: 3)));
        var changed = Exam(slug, Question(slug, 2), Question(slug, 1, statement: "Cambiada", optionCount: 4));

        var ex = await Should.ThrowAsync<ContentImportException>(() => ImportAsync(changed));

        ex.Message.ShouldContain($"{slug}/uno/0001");
        await using var db = postgres.CreateContext();
        (await db.Questions.CountAsync(q => q.Key.StartsWith(slug), Ct)).ShouldBe(1);
        var question = await db.Questions.SingleAsync(q => q.Key == $"{slug}/uno/0001", Ct);
        question.Statement.ShouldBe("¿Pregunta?");
        question.Options.Count.ShouldBe(3);
    }

    [Fact]
    public async Task ImportAsync_DryRun_PersistsNothing()
    {
        var slug = UniqueSlug();

        var report = await ImportAsync(Exam(slug, Question(slug, 1)), dryRun: true);

        report.DryRun.ShouldBeTrue();
        report.Questions.Created.ShouldBe(1);
        await using var db = postgres.CreateContext();
        (await db.Exams.AnyAsync(e => e.Slug == slug, Ct)).ShouldBeFalse();
        (await db.Questions.AnyAsync(q => q.Key.StartsWith(slug), Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task ImportAsync_DryRunWithChangesOnImportedContent_DoesNotPersistThem()
    {
        var slug = UniqueSlug();
        await ImportAsync(Exam(slug, Question(slug, 1), Question(slug, 2)));
        var changed = Exam(slug, Question(slug, 1, statement: "¿Cambiada?"), Question(slug, 3));

        var report = await ImportAsync(changed, dryRun: true);

        report.DryRun.ShouldBeTrue();
        report.Questions.ShouldBe(new ImportCounts(1, 1, 1, 0));
        await using var db = postgres.CreateContext();
        (await db.Questions.SingleAsync(q => q.Key == $"{slug}/uno/0001", Ct)).Statement.ShouldBe("¿Pregunta?");
        (await db.Questions.SingleAsync(q => q.Key == $"{slug}/uno/0002", Ct)).IsActive.ShouldBeTrue();
        (await db.Questions.AnyAsync(q => q.Key == $"{slug}/uno/0003", Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task ImportAsync_DeactivatedExamAppearsAgain_IsReactivated()
    {
        var slug = UniqueSlug();
        var exam = Exam(slug, Question(slug, 1));
        await ImportAsync(exam);
        await using (var db = postgres.CreateContext())
        {
            (await db.Exams.SingleAsync(e => e.Slug == slug, Ct)).Deactivate();
            await db.SaveChangesAsync(Ct);
        }

        var report = await ImportAsync(exam);

        report.Exams.ShouldBe(new ImportCounts(0, 0, 0, 1));
        await using var check = postgres.CreateContext();
        (await check.Exams.SingleAsync(e => e.Slug == slug, Ct)).IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task ImportAsync_TopicChanged_UpdatesTitleNumberAndSection()
    {
        var slug = UniqueSlug();
        await ImportAsync(Exam(slug, Question(slug, 1)));
        var changed = new ExamContent(slug, "Programador", "Diputación", "C1", [new TopicContent("uno", 5, "Título nuevo", "Bloque II", [Question(slug, 1)])]);

        var report = await ImportAsync(changed);

        report.Topics.ShouldBe(new ImportCounts(0, 1, 0, 0));
        await using var db = postgres.CreateContext();
        var topic = await db.Topics.SingleAsync(t => t.Slug == "uno" && db.Exams.Any(e => e.Id == t.ExamId && e.Slug == slug), Ct);
        topic.Number.ShouldBe(5);
        topic.Title.ShouldBe("Título nuevo");
        topic.Section.ShouldBe("Bloque II");
    }

    [Fact]
    public async Task ImportAsync_TopicRemovedFromContent_KeepsItAndWarns()
    {
        var slug = UniqueSlug();
        await ImportAsync(Exam(slug, Question(slug, 1)));
        var withoutTopics = new ExamContent(slug, "Programador", "Diputación", "C1", []);

        var report = await ImportAsync(withoutTopics);

        report.Warnings.ShouldHaveSingleItem().ShouldContain("uno");
        report.Questions.Deactivated.ShouldBe(1);
        await using var db = postgres.CreateContext();
        (await db.Topics.CountAsync(t => t.Slug == "uno" && db.Exams.Any(e => e.Id == t.ExamId && e.Slug == slug), Ct)).ShouldBe(1);
    }
}