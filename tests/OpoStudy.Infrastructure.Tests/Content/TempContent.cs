using System.Text.Json.Nodes;

namespace OpoStudy.Infrastructure.Tests.Content;

/// <summary>Writes a content tree into a temporary directory, deleted on dispose.</summary>
internal sealed class TempContent : IDisposable
{
    public TempContent()
    {
        Root = Path.Combine(Path.GetTempPath(), "opostudy-content-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public void Write(string relativePath, string json)
    {
        var path = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
    }

    public void WriteExam(string slug, params (string Slug, int Number)[] topics) =>
        Write($"exams/{slug}/exam.json", ExamJson(slug, topics));

    public void WriteTopic(string exam, string topic, params string[] questionJson) =>
        Write($"exams/{exam}/topics/{topic}.json", TopicJson(topic, questionJson));

    public void WritePracticalCase(string exam, string name, string json) =>
        Write($"exams/{exam}/practical-cases/{name}.json", json);

    /// <summary>A valid practical case with <paramref name="partCount"/> parts of two criteria each (3 + 2 points).</summary>
    public static string PracticalCaseJson(string key, int partCount = 2, int points = 3, string title = "Supuesto", string? section = null)
    {
        var parts = Enumerable.Range(0, partCount).Select(i => (JsonNode)new JsonObject
        {
            ["statement"] = "Apartado " + i,
            ["modelAnswer"] = "Respuesta modelo " + i,
            ["criteria"] = new JsonArray(
                new JsonObject { ["description"] = "Criterio A" + i, ["points"] = points },
                new JsonObject { ["description"] = "Criterio B" + i, ["points"] = 2 }),
        });
        var json = new JsonObject
        {
            ["key"] = key,
            ["title"] = title,
            ["statement"] = "Enunciado del supuesto",
            ["estimatedMinutes"] = 30,
            ["parts"] = new JsonArray([.. parts]),
        };
        if (section is not null)
        {
            json["section"] = section;
        }

        return json.ToJsonString();
    }

    public void Dispose() => Directory.Delete(Root, recursive: true);

    public static string ExamJson(string slug, params (string Slug, int Number)[] topics) =>
        new JsonObject
        {
            ["slug"] = slug,
            ["name"] = "Programador Informático",
            ["organization"] = "Diputación Provincial de Málaga",
            ["group"] = "C1",
            ["topics"] = new JsonArray([.. topics.Select(t => (JsonNode)new JsonObject
            {
                ["slug"] = t.Slug,
                ["number"] = t.Number,
                ["title"] = "Tema " + t.Number,
            })]),
        }.ToJsonString();

    public static string TopicJson(string topic, params string[] questionJson) =>
        new JsonObject
        {
            ["topic"] = topic,
            ["questions"] = new JsonArray([.. questionJson.Select(q => JsonNode.Parse(q))]),
        }.ToJsonString();

    /// <summary>A valid question with <paramref name="optionCount"/> options, the first <paramref name="correctCount"/> correct.</summary>
    public static string QuestionJson(string key, string statement = "¿Pregunta?", int optionCount = 3, int correctCount = 1)
    {
        var options = Enumerable.Range(0, optionCount)
            .Select(i => (JsonNode)new JsonObject { ["text"] = "Opción " + i, ["isCorrect"] = i < correctCount });
        return new JsonObject
        {
            ["key"] = key,
            ["statement"] = statement,
            ["options"] = new JsonArray([.. options]),
            ["explanation"] = "Porque sí.",
            ["legalReference"] = "CE, art. 1",
        }.ToJsonString();
    }
}