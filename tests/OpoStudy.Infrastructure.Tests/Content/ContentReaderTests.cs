using OpoStudy.Infrastructure.Content;

using Shouldly;

namespace OpoStudy.Infrastructure.Tests.Content;

public sealed class ContentReaderTests
{
    private const string Exam = "exam-a";

    [Fact]
    public void Read_ValidContent_ReturnsExamsWithoutErrors()
    {
        using var content = new TempContent();
        content.WriteExam(Exam, ("uno", 1), ("dos", 2));
        content.WriteTopic(Exam, "uno", TempContent.QuestionJson("exam-a/uno/0001"), TempContent.QuestionJson("exam-a/uno/0002"));
        content.Write("schema/exam.schema.json", "{}");

        var result = ContentReader.Read(content.Root);

        result.Errors.ShouldBeEmpty();
        var exam = result.Exams.ShouldHaveSingleItem();
        exam.Slug.ShouldBe(Exam);
        exam.Topics.Count.ShouldBe(2);
        exam.Topics[0].Questions.Count.ShouldBe(2);
        exam.Topics[1].Questions.ShouldBeEmpty();
    }

    [Fact]
    public void Read_MissingDirectory_ReturnsError()
    {
        var result = ContentReader.Read(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        result.Errors.ShouldHaveSingleItem();
        result.Exams.ShouldBeEmpty();
    }

    [Fact]
    public void Read_MalformedJson_ReportsFileAndLine()
    {
        using var content = new TempContent();
        content.Write($"exams/{Exam}/exam.json", "{\n  \"slug\": \n}");

        var error = ContentReader.Read(content.Root).Errors.ShouldHaveSingleItem();

        error.File.ShouldBe($"exams/{Exam}/exam.json");
        error.Message.ShouldContain("línea");
    }

    [Fact]
    public void Read_UnknownProperty_ReportsIt()
    {
        using var content = new TempContent();
        content.Write($"exams/{Exam}/exam.json", """{ "slug": "exam-a", "name": "N", "organization": "O", "group": "C1", "topics": [], "extra": 1 }""");

        var error = ContentReader.Read(content.Root).Errors.ShouldHaveSingleItem();

        error.Message.ShouldContain("extra");
    }

    [Fact]
    public void Read_MissingRequiredProperty_ReportsIt()
    {
        using var content = new TempContent();
        content.Write($"exams/{Exam}/exam.json", """{ "slug": "exam-a", "name": "N", "organization": "O", "topics": [] }""");

        var error = ContentReader.Read(content.Root).Errors.ShouldHaveSingleItem();

        error.Message.ShouldContain("group");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Read_QuestionWithoutExactlyOneCorrectOption_ReportsOptionsPath(int correctCount)
    {
        using var content = new TempContent();
        content.WriteExam(Exam, ("uno", 1));
        content.WriteTopic(Exam, "uno", TempContent.QuestionJson("exam-a/uno/0001"), TempContent.QuestionJson("exam-a/uno/0002", correctCount: correctCount));

        var error = ContentReader.Read(content.Root).Errors.ShouldHaveSingleItem();

        error.File.ShouldBe($"exams/{Exam}/topics/uno.json");
        error.Path.ShouldBe("$.questions[1].options");
    }

    [Fact]
    public void Read_TopicNotInExam_ReportsError()
    {
        using var content = new TempContent();
        content.WriteExam(Exam, ("uno", 1));
        content.WriteTopic(Exam, "otro", TempContent.QuestionJson("exam-a/otro/0001"));

        var error = ContentReader.Read(content.Root).Errors.ShouldHaveSingleItem();

        error.Path.ShouldBe("$.topic");
        error.Message.ShouldContain("exam.json");
    }

    [Fact]
    public void Read_TopicFieldDifferentFromFileName_ReportsError()
    {
        using var content = new TempContent();
        content.WriteExam(Exam, ("uno", 1), ("dos", 2));
        content.Write($"exams/{Exam}/topics/uno.json", TempContent.TopicJson("dos"));

        var error = ContentReader.Read(content.Root).Errors.ShouldHaveSingleItem();

        error.Path.ShouldBe("$.topic");
    }

    [Fact]
    public void Read_SlugDifferentFromFolder_ReportsError()
    {
        using var content = new TempContent();
        content.Write($"exams/{Exam}/exam.json", TempContent.ExamJson("otro-slug"));

        var error = ContentReader.Read(content.Root).Errors.ShouldHaveSingleItem();

        error.Path.ShouldBe("$.slug");
    }

    [Fact]
    public void Read_DuplicatedTopicSlugAndNumber_ReportsBoth()
    {
        using var content = new TempContent();
        content.WriteExam(Exam, ("uno", 1), ("uno", 1));

        var errors = ContentReader.Read(content.Root).Errors;

        errors.Select(e => e.Path).ShouldBe(["$.topics[1].slug", "$.topics[1].number"], ignoreOrder: true);
    }

    [Fact]
    public void Read_DuplicatedKeyAcrossFiles_ReportsError()
    {
        using var content = new TempContent();
        content.WriteExam(Exam, ("uno", 1), ("dos", 2));
        content.WriteTopic(Exam, "uno", TempContent.QuestionJson("exam-a/comun/0001"));
        content.WriteTopic(Exam, "dos", TempContent.QuestionJson("exam-a/comun/0001"));

        var error = ContentReader.Read(content.Root).Errors.ShouldHaveSingleItem();

        error.File.ShouldBe($"exams/{Exam}/topics/uno.json"); // files are read in alphabetical order
        error.Path.ShouldBe("$.questions[0].key");
    }

    [Fact]
    public void Read_InvalidKeyPattern_ReportsError()
    {
        using var content = new TempContent();
        content.WriteExam(Exam, ("uno", 1));
        content.WriteTopic(Exam, "uno", TempContent.QuestionJson("Exam A/uno"));

        var error = ContentReader.Read(content.Root).Errors.ShouldHaveSingleItem();

        error.Path.ShouldBe("$.questions[0].key");
    }

    [Fact]
    public void Read_FileOutsideStructure_ReportsError()
    {
        using var content = new TempContent();
        content.WriteExam(Exam);
        content.Write("notes/readme.txt", "x");

        var error = ContentReader.Read(content.Root).Errors.ShouldHaveSingleItem();

        error.File.ShouldBe("notes/readme.txt");
    }

    [Fact]
    public void Read_ExamFolderWithoutExamJson_ReportsError()
    {
        using var content = new TempContent();
        content.WriteTopic(Exam, "uno", TempContent.QuestionJson("exam-a/uno/0001"));

        var errors = ContentReader.Read(content.Root).Errors;

        errors.ShouldContain(e => e.Message.Contains("exam.json", StringComparison.Ordinal));
    }

    [Fact]
    public void Read_SeveralProblems_ReportsAllOfThem()
    {
        using var content = new TempContent();
        content.WriteExam(Exam, ("uno", 1), ("dos", 2));
        content.WriteTopic(Exam, "uno", TempContent.QuestionJson("exam-a/uno/0001", correctCount: 0), TempContent.QuestionJson("exam-a/uno/0002", optionCount: 1));
        content.Write($"exams/{Exam}/topics/dos.json", "{ not json");
        content.WriteTopic(Exam, "fantasma", TempContent.QuestionJson("exam-a/fantasma/0001"));

        var result = ContentReader.Read(content.Root);

        result.Exams.ShouldBeEmpty();
        result.Errors.Count.ShouldBeGreaterThanOrEqualTo(4);
        result.Errors.Select(e => e.File).Distinct().Count().ShouldBe(3);
    }
}