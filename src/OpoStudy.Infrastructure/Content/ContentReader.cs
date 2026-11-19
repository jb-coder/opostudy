using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using OpoStudy.Domain.Common;
using OpoStudy.Domain.Exams;
using OpoStudy.Domain.Questions;

namespace OpoStudy.Infrastructure.Content;

/// <summary>
/// Reads and validates <c>content/</c> (see spec 0003). It collects every error instead of stopping at the first one.
/// Domain rules are checked by building the domain entities, so there is a single source of truth.
/// </summary>
public static partial class ContentReader
{
    private const long MaxFileBytes = 5 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
    };

    /// <summary>Maps a domain error code to the JSON property it refers to.</summary>
    private static readonly Dictionary<string, string> CodeProperties = new(StringComparer.Ordinal)
    {
        ["exam.invalid_slug"] = "slug",
        ["exam.invalid_name"] = "name",
        ["exam.invalid_organization"] = "organization",
        ["exam.invalid_group"] = "group",
        ["topic.invalid_slug"] = "slug",
        ["topic.invalid_number"] = "number",
        ["topic.invalid_title"] = "title",
        ["topic.invalid_section"] = "section",
        ["question.invalid_key"] = "key",
        ["question.invalid_statement"] = "statement",
        ["question.invalid_explanation"] = "explanation",
        ["question.invalid_legal_reference"] = "legalReference",
        ["question.invalid_option_count"] = "options",
        ["question.no_correct_option"] = "options",
        ["question.multiple_correct_options"] = "options",
        ["option.invalid_text"] = "options",
    };

    /// <summary>Reads the content under <paramref name="contentRoot"/>. <see cref="ContentReadResult.Exams"/> is empty if there are errors.</summary>
    public static ContentReadResult Read(string contentRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRoot);

        var errors = new List<ContentError>();
        if (!Directory.Exists(contentRoot))
        {
            errors.Add(new ContentError(".", "$", $"No existe el directorio de contenido '{contentRoot}'."));
            return new ContentReadResult([], errors);
        }

        var root = Path.GetFullPath(contentRoot);
        CheckStructure(root, errors);

        var exams = new List<ExamContent>();
        var keys = new Dictionary<string, string>(StringComparer.Ordinal);
        var examsDir = Path.Combine(root, "exams");
        if (Directory.Exists(examsDir))
        {
            foreach (var examDir in Directory.GetDirectories(examsDir).Order(StringComparer.Ordinal))
            {
                var exam = ReadExam(root, examDir, keys, errors);
                if (exam is not null)
                {
                    exams.Add(exam);
                }
            }
        }

        return new ContentReadResult(errors.Count == 0 ? exams : [], errors);
    }

    private static void CheckStructure(string root, List<ContentError> errors)
    {
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var parts = Relative(root, file).Split('/');
            var valid = parts[0] == "schema"
                || (parts[0] == "exams"
                    && ((parts.Length == 3 && parts[2] == "exam.json")
                        || (parts.Length == 4 && parts[2] == "topics" && parts[3].EndsWith(".json", StringComparison.Ordinal))));
            if (!valid)
            {
                errors.Add(new ContentError(Relative(root, file), "$", "El fichero está fuera de la estructura de contenido esperada."));
            }
        }
    }

    private static ExamContent? ReadExam(string root, string examDir, Dictionary<string, string> keys, List<ContentError> errors)
    {
        var examFilePath = Path.Combine(examDir, "exam.json");
        var examFileName = Relative(root, examFilePath);
        var folderName = Path.GetFileName(examDir);
        var topicsDir = Path.Combine(examDir, "topics");

        ExamFile? examFile = null;
        if (File.Exists(examFilePath))
        {
            examFile = ReadJson<ExamFile>(root, examFilePath, errors);
        }
        else
        {
            errors.Add(new ContentError(examFileName, "$", "Falta el fichero exam.json de la oposición."));
        }

        var examValid = examFile is not null && ValidateExam(examFile, folderName, examFileName, errors);
        var topics = examValid ? ValidateTopics(examFile!, examFileName, errors) : [];

        // Topic files are always read so their own errors are reported even when exam.json is broken.
        var questionsByTopic = new Dictionary<string, List<QuestionContent>>(StringComparer.Ordinal);
        if (Directory.Exists(topicsDir))
        {
            foreach (var topicPath in Directory.GetFiles(topicsDir, "*.json").Order(StringComparer.Ordinal))
            {
                ReadTopicFile(root, topicPath, examValid ? topics : null, keys, questionsByTopic, errors);
            }
        }

        if (!examValid)
        {
            return null;
        }

        return new ExamContent(
            examFile!.Slug,
            examFile.Name,
            examFile.Organization,
            examFile.Group,
            [.. topics.Select(t => t with { Questions = questionsByTopic.GetValueOrDefault(t.Slug) ?? [] })]);
    }

    private static bool ValidateExam(ExamFile exam, string folderName, string file, List<ContentError> errors)
    {
        var before = errors.Count;
        Guarded(() => Exam.Create(exam.Slug, exam.Name, exam.Organization, exam.Group), file, "$", errors);
        if (exam.Slug != folderName)
        {
            errors.Add(new ContentError(file, "$.slug", $"El slug '{exam.Slug}' no coincide con el nombre de la carpeta '{folderName}'."));
        }

        return errors.Count == before;
    }

    /// <summary>Validates the topic list of exam.json. Returns the valid topics (without questions yet).</summary>
    private static List<TopicContent> ValidateTopics(ExamFile exam, string file, List<ContentError> errors)
    {
        var topics = new List<TopicContent>();
        var slugs = new HashSet<string>(StringComparer.Ordinal);
        var numbers = new HashSet<int>();
        for (var i = 0; i < exam.Topics.Count; i++)
        {
            var topic = exam.Topics[i];
            var path = $"$.topics[{i}]";
            var ok = Guarded(() => Topic.Create(Guid.Empty, topic.Slug, topic.Number, topic.Title, topic.Section), file, path, errors);
            if (!slugs.Add(topic.Slug))
            {
                errors.Add(new ContentError(file, $"{path}.slug", $"El slug de tema '{topic.Slug}' está repetido."));
                ok = false;
            }

            if (!numbers.Add(topic.Number))
            {
                errors.Add(new ContentError(file, $"{path}.number", $"El número de tema {topic.Number} está repetido."));
                ok = false;
            }

            if (ok)
            {
                topics.Add(new TopicContent(topic.Slug, topic.Number, topic.Title, topic.Section, []));
            }
        }

        return topics;
    }

    private static void ReadTopicFile(
        string root,
        string topicPath,
        List<TopicContent>? examTopics,
        Dictionary<string, string> keys,
        Dictionary<string, List<QuestionContent>> questionsByTopic,
        List<ContentError> errors)
    {
        var file = Relative(root, topicPath);
        var topicFile = ReadJson<TopicFile>(root, topicPath, errors);
        if (topicFile is null)
        {
            return;
        }

        var expected = Path.GetFileNameWithoutExtension(topicPath);
        if (topicFile.Topic != expected)
        {
            errors.Add(new ContentError(file, "$.topic", $"El tema '{topicFile.Topic}' no coincide con el nombre del fichero '{expected}'."));
        }
        else if (examTopics is not null && examTopics.All(t => t.Slug != topicFile.Topic))
        {
            errors.Add(new ContentError(file, "$.topic", $"El tema '{topicFile.Topic}' no figura en exam.json."));
        }

        var questions = new List<QuestionContent>();
        for (var i = 0; i < topicFile.Questions.Count; i++)
        {
            var q = topicFile.Questions[i];
            var path = $"$.questions[{i}]";
            var options = q.Options.Select(o => new AnswerOptionData(o.Text, o.IsCorrect)).ToList();
            var ok = Guarded(() => Question.Create(Guid.Empty, q.Key, q.Statement, q.Explanation, q.LegalReference, options), file, path, errors);

            if (!KeyRegex().IsMatch(q.Key))
            {
                errors.Add(new ContentError(file, $"{path}.key", $"La key '{q.Key}' no es válida: minúsculas, números, guiones y '/' entre segmentos."));
            }
            else if (keys.TryGetValue(q.Key, out var firstSeen))
            {
                errors.Add(new ContentError(file, $"{path}.key", $"La key '{q.Key}' está repetida (ya aparece en {firstSeen})."));
            }
            else
            {
                keys[q.Key] = $"{file} {path}";
            }

            if (ok)
            {
                questions.Add(new QuestionContent(q.Key, q.Statement, q.Explanation, q.LegalReference, options));
            }
        }

        questionsByTopic[topicFile.Topic] = questions;
    }

    private static T? ReadJson<T>(string root, string path, List<ContentError> errors)
        where T : class
    {
        var file = Relative(root, path);
        if (new FileInfo(path).Length > MaxFileBytes)
        {
            errors.Add(new ContentError(file, "$", "El fichero supera el tamaño máximo de 5 MB."));
            return null;
        }

        try
        {
            var result = JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions);
            if (result is null)
            {
                errors.Add(new ContentError(file, "$", "El fichero no contiene un objeto JSON."));
            }

            return result;
        }
        catch (JsonException ex)
        {
            var line = ex.LineNumber is { } n ? $" (línea {n + 1})" : string.Empty;
            errors.Add(new ContentError(file, ex.Path ?? "$", TranslateJsonError(ex.Message) + line));
            return null;
        }
    }

    private static string TranslateJsonError(string message)
    {
        if (message.Contains("missing required properties", StringComparison.Ordinal))
        {
            var names = RequiredNamesRegex().Match(message).Groups[1].Value;
            return $"Faltan propiedades obligatorias: {names}.";
        }

        var unknown = UnmappedRegex().Match(message);
        if (unknown.Success)
        {
            return $"Propiedad desconocida '{unknown.Groups[1].Value}'.";
        }

        return $"JSON no válido: {message}";
    }

    /// <summary>Runs an entity factory and turns a domain rule violation into a content error. Returns whether it passed.</summary>
    private static bool Guarded(Action build, string file, string path, List<ContentError> errors)
    {
        try
        {
            build();
            return true;
        }
        catch (DomainRuleException ex)
        {
            var property = CodeProperties.TryGetValue(ex.Code, out var name) ? $"{path}.{name}" : path;
            errors.Add(new ContentError(file, property, $"[{ex.Code}] {ex.Message}"));
            return false;
        }
    }

    private static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');

    [GeneratedRegex("^[a-z0-9-]+(/[a-z0-9-]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyRegex();

    [GeneratedRegex(@"including: (.+?)\.?$", RegexOptions.CultureInvariant)]
    private static partial Regex RequiredNamesRegex();

    [GeneratedRegex("JSON property '([^']+)'", RegexOptions.CultureInvariant)]
    private static partial Regex UnmappedRegex();
}