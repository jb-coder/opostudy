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
    public void Read_TopicSlugsDifferingOnlyBySpaces_ReportsError()
    {
        using var content = new TempContent();
        content.WriteExam(Exam, (" uno ", 1), ("uno", 2));

        var result = ContentReader.Read(content.Root);

        result.Exams.ShouldBeEmpty();
        result.Errors.ShouldContain(e => e.Path == "$.topics[0].slug");
        result.Errors.ShouldContain(e => e.Path == "$.topics[1].slug" && e.Message.Contains("repetido", StringComparison.Ordinal));
    }

    [Fact]
    public void Read_QuestionKeyWithSurroundingSpaces_ReportsError()
    {
        using var content = new TempContent();
        content.WriteExam(Exam, ("uno", 1));
        content.WriteTopic(Exam, "uno", TempContent.QuestionJson(" exam-a/uno/0001 "));

        var error = ContentReader.Read(content.Root).Errors.ShouldHaveSingleItem();

        error.Path.ShouldBe("$.questions[0].key");
    }

    [Fact]
    public void Read_TopicFieldWithSurroundingSpaces_ReportsError()
    {
        using var content = new TempContent();
        content.WriteExam(Exam, ("uno", 1));
        content.Write($"exams/{Exam}/topics/uno.json", TempContent.TopicJson(" uno "));

        var error = ContentReader.Read(content.Root).Errors.ShouldHaveSingleItem();

        error.Path.ShouldBe("$.topic");
    }

    [Fact]
    public void Read_FileOverFiveMegabytes_ReportsSizeError()
    {
        using var content = new TempContent();
        content.Write($"exams/{Exam}/exam.json", new string(' ', (5 * 1024 * 1024) + 1));

        var error = ContentReader.Read(content.Root).Errors.ShouldHaveSingleItem();

        error.File.ShouldBe($"exams/{Exam}/exam.json");
        error.Message.ShouldContain("5 MB");
    }

    [Fact]
    public void Read_SymbolicLink_ReportsError()
    {
        using var content = new TempContent();
        content.WriteExam(Exam, ("uno", 1));
        var target = Path.Combine(content.Root, "exams", Exam, "exam.json");
        var link = Path.Combine(content.Root, "exams", Exam, "topics", "uno.json");
        Directory.CreateDirectory(Path.GetDirectoryName(link)!);
        try
        {
            File.CreateSymbolicLink(link, target);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            Assert.Skip("Cannot create symbolic links in this environment.");
        }

        var result = ContentReader.Read(content.Root);

        result.Exams.ShouldBeEmpty();
        result.Errors.ShouldContain(e => e.File == $"exams/{Exam}/topics/uno.json" && e.Message.Contains("enlaces simbólicos", StringComparison.Ordinal));
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

    [Fact]
    public void Read_ValidPracticalCase_ReturnsItInTheExam()
    {
        using var content = new TempContent();
        content.WriteExam(Exam, ("uno", 1));
        content.WritePracticalCase(Exam, "0001-uno", TempContent.PracticalCaseJson("exam-a/practical-cases/0001"));

        var result = ContentReader.Read(content.Root);

        result.Errors.ShouldBeEmpty();
        var practicalCase = result.Exams.ShouldHaveSingleItem().PracticalCases.ShouldHaveSingleItem();
        practicalCase.Key.ShouldBe("exam-a/practical-cases/0001");
        practicalCase.Parts.Count.ShouldBe(2);
        practicalCase.Parts[1].Criteria.Count.ShouldBe(2);
    }

    [Fact]
    public void Read_PracticalCaseWithUnknownProperty_ReportsIt()
    {
        using var content = new TempContent();
        content.WriteExam(Exam, ("uno", 1));
        content.WritePracticalCase(Exam, "0001-uno", TempContent.PracticalCaseJson("exam-a/practical-cases/0001").Replace("\"title\"", "\"extra\":1,\"title\"", StringComparison.Ordinal));

        var error = ContentReader.Read(content.Root).Errors.ShouldHaveSingleItem();

        error.File.ShouldBe($"exams/{Exam}/practical-cases/0001-uno.json");
        error.Message.ShouldContain("extra");
    }

    [Fact]
    public void Read_PracticalCaseCriterionPointsOutOfRange_ReportsExactPath()
    {
        using var content = new TempContent();
        content.WriteExam(Exam, ("uno", 1));
        content.WritePracticalCase(Exam, "0001-uno", TempContent.PracticalCaseJson("exam-a/practical-cases/0001", points: 0));

        var result = ContentReader.Read(content.Root);

        result.Exams.ShouldBeEmpty();
        result.Errors.Select(e => e.Path).ShouldBe(["$.parts[0].criteria[0].points", "$.parts[1].criteria[0].points"]);
        result.Errors[0].Message.ShouldContain("practical_case.invalid_criterion_points");
    }

    [Fact]
    public void Read_PracticalCaseWithoutParts_ReportsPartsPath()
    {
        using var content = new TempContent();
        content.WriteExam(Exam, ("uno", 1));
        content.WritePracticalCase(Exam, "0001-uno", TempContent.PracticalCaseJson("exam-a/practical-cases/0001", partCount: 0));

        var error = ContentReader.Read(content.Root).Errors.ShouldHaveSingleItem();

        error.Path.ShouldBe("$.parts");
    }

    [Fact]
    public void Read_DuplicatePracticalCaseKey_ReportsTheSecondFile()
    {
        using var content = new TempContent();
        content.WriteExam(Exam, ("uno", 1));
        content.WritePracticalCase(Exam, "0001-uno", TempContent.PracticalCaseJson("exam-a/practical-cases/0001"));
        content.WritePracticalCase(Exam, "0002-dos", TempContent.PracticalCaseJson("exam-a/practical-cases/0001"));

        var error = ContentReader.Read(content.Root).Errors.ShouldHaveSingleItem();

        error.File.ShouldBe($"exams/{Exam}/practical-cases/0002-dos.json");
        error.Path.ShouldBe("$.key");
        error.Message.ShouldContain("repetida");
    }

    [Fact]
    public void Read_PracticalCaseOfExamWithoutExamFile_ReportsStructureError()
    {
        using var content = new TempContent();
        content.WritePracticalCase(Exam, "0001-uno", TempContent.PracticalCaseJson("exam-a/practical-cases/0001"));

        var result = ContentReader.Read(content.Root);

        result.Exams.ShouldBeEmpty();
        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("exam.json");
    }
}
