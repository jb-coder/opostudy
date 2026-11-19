using System.Text.Json;

using Json.Schema;

using OpoStudy.Infrastructure.Content;

using Shouldly;

namespace OpoStudy.Infrastructure.Tests.Content;

/// <summary>Validates the real <c>content/</c> of the repository, so invalid content fails the CI.</summary>
public sealed class RepositoryContentTests
{
    private static readonly string ContentRoot = Path.Combine(FindRepositoryRoot(), "content");

    [Fact]
    public void Read_RepositoryContent_HasNoErrors()
    {
        var result = ContentReader.Read(ContentRoot);

        result.Errors.ShouldBeEmpty(string.Join(Environment.NewLine, result.Errors));
        result.Exams.ShouldNotBeEmpty();
    }

    [Fact]
    public void JsonSchema_RepositoryFiles_AreValid()
    {
        var examSchema = LoadSchema("exam.schema.json");
        var topicSchema = LoadSchema("topic.schema.json");
        var problems = new List<string>();

        foreach (var file in Directory.EnumerateFiles(Path.Combine(ContentRoot, "exams"), "*.json", SearchOption.AllDirectories))
        {
            var schema = Path.GetFileName(file) == "exam.json" ? examSchema : topicSchema;
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            var result = schema.Evaluate(document.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
            if (!result.IsValid)
            {
                var details = (result.Details ?? []).Where(d => d.Errors is not null).SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Value}"));
                problems.Add($"{Path.GetRelativePath(ContentRoot, file)}: {string.Join("; ", details)}");
            }
        }

        problems.ShouldBeEmpty(string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void JsonSchema_InvalidExam_IsRejected()
    {
        var schema = LoadSchema("exam.schema.json");
        using var document = JsonDocument.Parse("""{ "slug": "Mal Slug", "name": "N", "organization": "O", "group": "C1", "topics": [], "extra": true }""");

        schema.Evaluate(document.RootElement).IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void JsonSchema_QuestionWithoutExactlyOneCorrectOption_IsRejected(int correctCount)
    {
        var schema = LoadSchema("topic.schema.json");
        var question = TempContent.QuestionJson("a/b/0001", correctCount: correctCount);
        using var document = JsonDocument.Parse(TempContent.TopicJson("b", question));

        schema.Evaluate(document.RootElement).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void JsonSchema_ValidTopicWithQuestions_IsAccepted()
    {
        var schema = LoadSchema("topic.schema.json");
        using var document = JsonDocument.Parse(TempContent.TopicJson("tema-uno", TempContent.QuestionJson("a/tema-uno/0001"), TempContent.QuestionJson("a/tema-uno/0002")));

        schema.Evaluate(document.RootElement).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void JsonSchema_WhitespaceOnlyStatement_IsRejected()
    {
        var schema = LoadSchema("topic.schema.json");
        using var document = JsonDocument.Parse(TempContent.TopicJson("tema-uno", TempContent.QuestionJson("a/tema-uno/0001", statement: "   ")));

        schema.Evaluate(document.RootElement).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void JsonSchema_WhitespaceOnlyExamName_IsRejected()
    {
        var schema = LoadSchema("exam.schema.json");
        using var document = JsonDocument.Parse("""{ "slug": "a", "name": "  ", "organization": "O", "group": "C1", "topics": [] }""");

        schema.Evaluate(document.RootElement).IsValid.ShouldBeFalse();
    }

    private static JsonSchema LoadSchema(string name) =>
        JsonSchema.FromText(File.ReadAllText(Path.Combine(ContentRoot, "schema", name)));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpoStudy.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("No se encuentra OpoStudy.slnx subiendo desde el directorio de ejecución.");
    }
}