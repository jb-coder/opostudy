using Microsoft.EntityFrameworkCore;

using OpoStudy.Domain.PracticalCases;
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
        new(slug, "Programador", "Diputación", "C1", [new TopicContent("uno", 1, "Tema uno", "Bloque I", questions)], []);

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
        var changed = new ExamContent(slug, "Programador", "Diputación", "C1", [new TopicContent("uno", 5, "Título nuevo", "Bloque II", [Question(slug, 1)])], []);

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
        var withoutTopics = new ExamContent(slug, "Programador", "Diputación", "C1", [], []);

        var report = await ImportAsync(withoutTopics);

        report.Warnings.ShouldHaveSingleItem().ShouldContain("uno");
        report.Questions.Deactivated.ShouldBe(1);
        await using var db = postgres.CreateContext();
        (await db.Topics.CountAsync(t => t.Slug == "uno" && db.Exams.Any(e => e.Id == t.ExamId && e.Slug == slug), Ct)).ShouldBe(1);
    }

    private static PracticalCaseContent Case(string slug, int number, string title = "Supuesto", int partCount = 2, int points = 3, string criterionA = "Criterio A", string? section = null)
    {
        var parts = Enumerable.Range(0, partCount)
            .Select(i => new PracticalCasePartData(
                "Apartado " + i,
                "Respuesta " + i,
                [new GradingCriterionData(criterionA, points), new GradingCriterionData("Criterio B", 2)]))
            .ToList();
        return new PracticalCaseContent($"{slug}/practical-cases/{number:0000}", title, section, "Enunciado", 30, parts);
    }

    private static ExamContent ExamWithCases(string slug, params PracticalCaseContent[] cases) =>
        new(slug, "Programador", "Diputación", "C1", [], cases);

    [Fact]
    public async Task ImportAsync_NewPracticalCase_CreatesItWithPartsAndCriteria()
    {
        var slug = UniqueSlug();

        var report = await ImportAsync(ExamWithCases(slug, Case(slug, 1)));

        report.PracticalCases.ShouldBe(new ImportCounts(1, 0, 0, 0));
        await using var db = postgres.CreateContext();
        var practicalCase = await db.PracticalCases.SingleAsync(c => c.Key == $"{slug}/practical-cases/0001", Ct);
        practicalCase.Parts.Count.ShouldBe(2);
        practicalCase.MaxPoints.ShouldBe(10);
        practicalCase.Parts[1].Criteria.ShouldBe([new GradingCriterion("Criterio A", 3), new GradingCriterion("Criterio B", 2)]);
    }

    [Fact]
    public async Task ImportAsync_PracticalCasesTwice_ChangesNothing()
    {
        var slug = UniqueSlug();
        var exam = ExamWithCases(slug, Case(slug, 1));
        await ImportAsync(exam);

        var report = await ImportAsync(exam);

        report.PracticalCases.ShouldBe(new ImportCounts(0, 0, 0, 0));
        await using var db = postgres.CreateContext();
        (await db.PracticalCases.CountAsync(c => c.Key.StartsWith(slug), Ct)).ShouldBe(1);
    }

    [Fact]
    public async Task ImportAsync_PracticalCaseOnlySectionChanged_CountsAnUpdate()
    {
        var slug = UniqueSlug();
        await ImportAsync(ExamWithCases(slug, Case(slug, 1)));

        var report = await ImportAsync(ExamWithCases(slug, Case(slug, 1, section: "Bloque II-A. Materias específicas")));

        report.PracticalCases.ShouldBe(new ImportCounts(0, 1, 0, 0));
        await using var db = postgres.CreateContext();
        (await db.PracticalCases.SingleAsync(c => c.Key == $"{slug}/practical-cases/0001", Ct)).Section.ShouldBe("Bloque II-A. Materias específicas");
    }

    [Fact]
    public async Task ImportAsync_PracticalCaseWithSectionTwice_ChangesNothing()
    {
        var slug = UniqueSlug();
        var exam = ExamWithCases(slug, Case(slug, 1, section: "Bloque II-C. Materias específicas"));
        await ImportAsync(exam);

        var report = await ImportAsync(exam);

        report.PracticalCases.ShouldBe(new ImportCounts(0, 0, 0, 0));
    }

    [Fact]
    public async Task ImportAsync_PracticalCaseTextsAndCriteriaChanged_UpdatesThem()
    {
        var slug = UniqueSlug();
        await ImportAsync(ExamWithCases(slug, Case(slug, 1)));

        var report = await ImportAsync(ExamWithCases(slug, Case(slug, 1, title: "Título nuevo", points: 5)));

        report.PracticalCases.ShouldBe(new ImportCounts(0, 1, 0, 0));
        await using var db = postgres.CreateContext();
        var practicalCase = await db.PracticalCases.SingleAsync(c => c.Key == $"{slug}/practical-cases/0001", Ct);
        practicalCase.Title.ShouldBe("Título nuevo");
        practicalCase.MaxPoints.ShouldBe(14);
    }

    [Fact]
    public async Task ImportAsync_PracticalCaseRemovedAndBack_DeactivatesAndReactivatesIt()
    {
        var slug = UniqueSlug();
        await ImportAsync(ExamWithCases(slug, Case(slug, 1), Case(slug, 2)));

        var removed = await ImportAsync(ExamWithCases(slug, Case(slug, 1)));
        await using (var db = postgres.CreateContext())
        {
            (await db.PracticalCases.SingleAsync(c => c.Key == $"{slug}/practical-cases/0002", Ct)).IsActive.ShouldBeFalse();
        }

        var back = await ImportAsync(ExamWithCases(slug, Case(slug, 1), Case(slug, 2)));

        removed.PracticalCases.ShouldBe(new ImportCounts(0, 0, 1, 0));
        back.PracticalCases.ShouldBe(new ImportCounts(0, 0, 0, 1));
        await using var verify = postgres.CreateContext();
        (await verify.PracticalCases.SingleAsync(c => c.Key == $"{slug}/practical-cases/0002", Ct)).IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task ImportAsync_PracticalCasePartCountChanged_FailsWithoutPartialChanges()
    {
        var slug = UniqueSlug();
        await ImportAsync(ExamWithCases(slug, Case(slug, 1), Case(slug, 2)));
        var changed = ExamWithCases(slug, Case(slug, 1, title: "Cambiado"), Case(slug, 2, partCount: 3));

        var exception = await Should.ThrowAsync<ContentImportException>(() => ImportAsync(changed));

        exception.Message.ShouldContain($"{slug}/practical-cases/0002");
        await using var db = postgres.CreateContext();
        (await db.PracticalCases.SingleAsync(c => c.Key == $"{slug}/practical-cases/0001", Ct)).Title.ShouldBe("Supuesto");
    }

    [Fact]
    public async Task ImportAsync_PracticalCasesDryRun_SavesNothing()
    {
        var slug = UniqueSlug();

        var report = await ImportAsync(ExamWithCases(slug, Case(slug, 1)), dryRun: true);

        report.PracticalCases.Created.ShouldBe(1);
        await using var db = postgres.CreateContext();
        (await db.PracticalCases.CountAsync(c => c.Key.StartsWith(slug), Ct)).ShouldBe(0);
    }

    [Fact]
    public async Task ImportAsync_OnlyCriterionDescriptionChanged_PersistsTheNewDescription()
    {
        var slug = UniqueSlug();
        await ImportAsync(ExamWithCases(slug, Case(slug, 1)));

        var report = await ImportAsync(ExamWithCases(slug, Case(slug, 1, criterionA: "Criterio reescrito")));

        report.PracticalCases.ShouldBe(new ImportCounts(0, 1, 0, 0));
        await using var db = postgres.CreateContext();
        var practicalCase = await db.PracticalCases.SingleAsync(c => c.Key == $"{slug}/practical-cases/0001", Ct);
        practicalCase.Parts.ShouldAllBe(p => p.Criteria[0].Description == "Criterio reescrito" && p.Criteria[0].Points == 3);
        practicalCase.MaxPoints.ShouldBe(10);
    }

    [Fact]
    public async Task ImportAsync_OnlyCriterionPointsChanged_PersistsPointsAndMaxPoints()
    {
        var slug = UniqueSlug();
        await ImportAsync(ExamWithCases(slug, Case(slug, 1)));

        var report = await ImportAsync(ExamWithCases(slug, Case(slug, 1, points: 7)));

        report.PracticalCases.ShouldBe(new ImportCounts(0, 1, 0, 0));
        await using var db = postgres.CreateContext();
        var practicalCase = await db.PracticalCases.SingleAsync(c => c.Key == $"{slug}/practical-cases/0001", Ct);
        practicalCase.Parts.ShouldAllBe(p => p.Criteria[0].Points == 7 && p.MaxPoints == 9);
        practicalCase.MaxPoints.ShouldBe(18);
    }

    [Fact]
    public async Task ImportAsync_PracticalCaseMovedToAnotherExam_ChangesExamIdAndIsNotDeactivated()
    {
        var slugA = UniqueSlug();
        var slugB = UniqueSlug();
        var movedKey = $"{slugA}/practical-cases/0001";
        var moved = Case(slugA, 1);
        await ImportAsync(ExamWithCases(slugA, moved));
        await ImportAsync(ExamWithCases(slugB));

        await using var importDb = postgres.CreateContext();
        var report = await new ContentImporter(importDb).ImportAsync([ExamWithCases(slugA), ExamWithCases(slugB, moved)], false, Ct);

        report.PracticalCases.ShouldBe(new ImportCounts(0, 1, 0, 0));
        await using var db = postgres.CreateContext();
        var practicalCase = await db.PracticalCases.SingleAsync(c => c.Key == movedKey, Ct);
        practicalCase.ExamId.ShouldBe((await db.Exams.SingleAsync(e => e.Slug == slugB, Ct)).Id);
        practicalCase.IsActive.ShouldBeTrue();
    }
}
