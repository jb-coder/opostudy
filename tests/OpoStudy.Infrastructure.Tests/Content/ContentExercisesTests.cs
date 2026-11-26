using Microsoft.EntityFrameworkCore;

using OpoStudy.Infrastructure.Content;

using Shouldly;

namespace OpoStudy.Infrastructure.Tests.Content;

public sealed class ContentExercisesTests(PostgresFixture postgres)
{
    private const string Exam = "exam-a";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueSlug() => "t" + Guid.NewGuid().ToString("N")[..12];

    private static string ExamJson(string exercises) =>
        "{ \"slug\": \"exam-a\", \"name\": \"N\", \"organization\": \"O\", \"group\": \"C1\","
        + " \"topics\": [ { \"slug\": \"uno\", \"number\": 1, \"title\": \"T\", \"section\": \"Bloque I\" } ],"
        + " \"exercises\": " + exercises + " }";

    private static string ExerciseJson(int number = 1, string sections = "[\"Bloque I\"]") =>
        "{ \"number\": " + number + ", \"title\": \"Primer ejercicio\", \"questionCount\": 5, \"durationMinutes\": 10,"
        + " \"maxScore\": 10, \"penaltyRatio\": 0.5, \"sections\": " + sections + " }";

    private static ExamContent Content(string slug, params ExerciseContent[] exercises) =>
        new(slug, "Programador", "Diputación", "C1", [new TopicContent("uno", 1, "Tema uno", "Bloque I", [])], [], exercises);

    private static ExerciseContent Exercise(int number, string title = "Primer ejercicio", int questionCount = 5) =>
        new(number, title, questionCount, 10, 10m, 0.5m, ["Bloque I"]);

    private async Task<ImportReport> ImportAsync(ExamContent exam)
    {
        await using var db = postgres.CreateContext();
        return await new ContentImporter(db).ImportAsync([exam], false, Ct);
    }

    [Fact]
    public void Read_ValidExercises_ReturnsThem()
    {
        using var content = new TempContent();
        content.Write($"exams/{Exam}/exam.json", ExamJson("[" + ExerciseJson() + "]"));

        var result = ContentReader.Read(content.Root);

        result.Errors.ShouldBeEmpty();
        var exercise = result.Exams.Single().Exercises.ShouldNotBeNull().ShouldHaveSingleItem();
        exercise.Number.ShouldBe(1);
        exercise.MaxScore.ShouldBe(10m);
        exercise.Sections.ShouldBe(["Bloque I"]);
    }

    [Fact]
    public void Read_UnknownSection_ReportsError()
    {
        using var content = new TempContent();
        content.Write($"exams/{Exam}/exam.json", ExamJson("[" + ExerciseJson(sections: "[\"Bloque X\"]") + "]"));

        var result = ContentReader.Read(content.Root);

        var error = result.Errors.ShouldHaveSingleItem();
        error.Path.ShouldBe("$.exercises[0].sections[0]");
        result.Exams.ShouldBeEmpty();
    }

    [Fact]
    public void Read_RepeatedNumber_ReportsError()
    {
        using var content = new TempContent();
        content.Write($"exams/{Exam}/exam.json", ExamJson("[" + ExerciseJson() + "," + ExerciseJson() + "]"));

        var error = ContentReader.Read(content.Root).Errors.ShouldHaveSingleItem();

        error.Path.ShouldBe("$.exercises[1].number");
    }

    [Fact]
    public void Read_InvalidQuestionCount_ReportsDomainError()
    {
        using var content = new TempContent();
        var json = ExerciseJson().Replace("\"questionCount\": 5", "\"questionCount\": 500", StringComparison.Ordinal);
        content.Write($"exams/{Exam}/exam.json", ExamJson("[" + json + "]"));

        var error = ContentReader.Read(content.Root).Errors.ShouldHaveSingleItem();

        error.Path.ShouldBe("$.exercises[0].questionCount");
    }

    [Fact]
    public async Task ImportAsync_Exercises_AreCreatedUpdatedAndIdempotent()
    {
        var slug = UniqueSlug();

        var first = await ImportAsync(Content(slug, Exercise(1), Exercise(2)));
        var again = await ImportAsync(Content(slug, Exercise(1), Exercise(2)));
        var changed = await ImportAsync(Content(slug, Exercise(1, "Otro título"), Exercise(2)));

        first.Exercises.ShouldBe(new ImportCounts(2, 0, 0, 0));
        again.Exercises.ShouldBe(new ImportCounts(0, 0, 0, 0));
        changed.Exercises.ShouldBe(new ImportCounts(0, 1, 0, 0));
        await using var db = postgres.CreateContext();
        var exercises = await db.ExamExercises.Where(x => db.Exams.Any(e => e.Id == x.ExamId && e.Slug == slug)).OrderBy(x => x.Number).ToListAsync(Ct);
        exercises.Select(x => x.Title).ShouldBe(["Otro título", "Primer ejercicio"]);
        exercises[0].Sections.ShouldBe(["Bloque I"]);
    }

    [Fact]
    public async Task ImportAsync_ExerciseRemovedFromContent_DeletesIt()
    {
        var slug = UniqueSlug();
        await ImportAsync(Content(slug, Exercise(1), Exercise(2)));

        var report = await ImportAsync(Content(slug, Exercise(1)));

        report.Exercises.ShouldBe(new ImportCounts(0, 0, 1, 0));
        await using var db = postgres.CreateContext();
        var numbers = await db.ExamExercises.Where(x => db.Exams.Any(e => e.Id == x.ExamId && e.Slug == slug)).Select(x => x.Number).ToListAsync(Ct);
        numbers.ShouldBe([1]);
    }
}
