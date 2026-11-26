using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using OpoStudy.Domain.Common;
using OpoStudy.Domain.Exams;
using OpoStudy.Domain.PracticalCases;
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
        ["exercise.invalid_number"] = "number",
        ["exercise.invalid_title"] = "title",
        ["exercise.invalid_question_count"] = "questionCount",
        ["exercise.invalid_duration"] = "durationMinutes",
        ["exercise.invalid_max_score"] = "maxScore",
        ["exercise.invalid_penalty_ratio"] = "penaltyRatio",
        ["exercise.invalid_sections"] = "sections",
        ["practical_case.invalid_key"] = "key",
        ["practical_case.invalid_title"] = "title",
        ["practical_case.invalid_section"] = "section",
        ["practical_case.invalid_statement"] = "statement",
        ["practical_case.invalid_estimated_minutes"] = "estimatedMinutes",
        ["practical_case.invalid_part_count"] = "parts",
        ["practical_case.invalid_part_statement"] = "statement",
        ["practical_case.invalid_model_answer"] = "modelAnswer",
        ["practical_case.invalid_criterion_count"] = "criteria",
        ["practical_case.invalid_criterion_description"] = "description",
        ["practical_case.invalid_criterion_points"] = "points",
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
        var caseKeys = new Dictionary<string, string>(StringComparer.Ordinal);
        var examsDir = Path.Combine(root, "exams");
        if (Directory.Exists(examsDir))
        {
            foreach (var examDir in Directory.GetDirectories(examsDir).Where(d => !IsLink(d)).Order(StringComparer.Ordinal))
            {
                var exam = ReadExam(root, examDir, keys, caseKeys, errors);
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
        var options = new EnumerationOptions { AttributesToSkip = 0, RecurseSubdirectories = false, IgnoreInaccessible = false };
        var pending = new Stack<string>();
        pending.Push(root);
        var files = new List<string>();
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory, "*", options))
            {
                if (IsLink(entry))
                {
                    errors.Add(new ContentError(Relative(root, entry), "$", "Los enlaces simbólicos no están permitidos en el contenido."));
                }
                else if (Directory.Exists(entry))
                {
                    pending.Push(entry);
                }
                else
                {
                    files.Add(entry);
                }
            }
        }

        foreach (var file in files.Order(StringComparer.Ordinal))
        {
            var parts = Relative(root, file).Split('/');
            var valid = parts[0] == "schema"
                || (parts[0] == "exams"
                    && ((parts.Length == 3 && parts[2] == "exam.json")
                        || (parts.Length == 4 && parts[2] is "topics" or "practical-cases" && parts[3].EndsWith(".json", StringComparison.Ordinal))));
            if (!valid)
            {
                errors.Add(new ContentError(Relative(root, file), "$", "El fichero está fuera de la estructura de contenido esperada."));
            }
        }
    }

    /// <summary>Symbolic links and other reparse points (files or directories).</summary>
    private static bool IsLink(string path) =>
        File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint) || new FileInfo(path).LinkTarget is not null;

    private static ExamContent? ReadExam(
        string root,
        string examDir,
        Dictionary<string, string> keys,
        Dictionary<string, string> caseKeys,
        List<ContentError> errors)
    {
        var examFilePath = Path.Combine(examDir, "exam.json");
        var examFileName = Relative(root, examFilePath);
        var folderName = Path.GetFileName(examDir);
        var topicsDir = Path.Combine(examDir, "topics");
        var casesDir = Path.Combine(examDir, "practical-cases");

        ExamFile? examFile = null;
        if (File.Exists(examFilePath) && IsLink(examFilePath))
        {
            // Already reported by the structure check; do not follow it.
        }
        else if (File.Exists(examFilePath))
        {
            examFile = ReadJson<ExamFile>(root, examFilePath, errors);
        }
        else
        {
            errors.Add(new ContentError(examFileName, "$", "Falta el fichero exam.json de la oposición."));
        }

        var examValid = examFile is not null && ValidateExam(examFile, folderName, examFileName, errors);
        var topics = examValid ? ValidateTopics(examFile!, examFileName, errors) : [];
        var exercises = examValid ? ValidateExercises(examFile!, topics, examFileName, errors) : [];

        // Topic files are always read so their own errors are reported even when exam.json is broken.
        var questionsByTopic = new Dictionary<string, List<QuestionContent>>(StringComparer.Ordinal);
        if (Directory.Exists(topicsDir) && !IsLink(topicsDir))
        {
            foreach (var topicPath in Directory.GetFiles(topicsDir, "*.json").Where(f => !IsLink(f)).Order(StringComparer.Ordinal))
            {
                ReadTopicFile(root, topicPath, examValid ? topics : null, keys, questionsByTopic, errors);
            }
        }

        // Practical case files are also read when exam.json is broken, for the same reason.
        var cases = new List<PracticalCaseContent>();
        if (Directory.Exists(casesDir) && !IsLink(casesDir))
        {
            foreach (var casePath in Directory.GetFiles(casesDir, "*.json").Where(f => !IsLink(f)).Order(StringComparer.Ordinal))
            {
                ReadPracticalCaseFile(root, casePath, Guid.Empty, caseKeys, cases, errors);
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
            [.. topics.Select(t => t with { Questions = questionsByTopic.GetValueOrDefault(t.Slug) ?? [] })],
            cases,
            exercises);
    }

    private static bool ValidateExam(ExamFile exam, string folderName, string file, List<ContentError> errors)
    {
        var before = errors.Count;
        Guarded(() => Exam.Create(exam.Slug, exam.Name, exam.Organization, exam.Group), file, "$", errors);
        if (exam.Slug.Trim() != exam.Slug)
        {
            errors.Add(new ContentError(file, "$.slug", $"El slug '{exam.Slug}' tiene espacios al principio o al final."));
        }
        else if (exam.Slug != folderName)
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

            // The domain trims slugs; the schema rejects spaces. Reject here too, so the reader and the importer never see different values.
            var slug = topic.Slug.Trim();
            if (slug != topic.Slug)
            {
                errors.Add(new ContentError(file, $"{path}.slug", $"El slug de tema '{topic.Slug}' tiene espacios al principio o al final."));
                ok = false;
            }

            if (!slugs.Add(slug))
            {
                errors.Add(new ContentError(file, $"{path}.slug", $"El slug de tema '{slug}' está repetido."));
                ok = false;
            }

            if (!numbers.Add(topic.Number))
            {
                errors.Add(new ContentError(file, $"{path}.number", $"El número de tema {topic.Number} está repetido."));
                ok = false;
            }

            if (ok)
            {
                topics.Add(new TopicContent(slug, topic.Number, topic.Title, topic.Section, []));
            }
        }

        return topics;
    }

    /// <summary>Validates the optional exercises of exam.json: domain rules, unique numbers and sections that exist in the syllabus.</summary>
    private static List<ExerciseContent> ValidateExercises(ExamFile exam, List<TopicContent> topics, string file, List<ContentError> errors)
    {
        var exercises = new List<ExerciseContent>();
        var numbers = new HashSet<int>();
        // Compared trimmed, as the domain stores both the topic sections and the exercise sections.
        var knownSections = topics.Select(t => t.Section?.Trim()).Where(s => s is not null).ToHashSet(StringComparer.Ordinal);
        var items = exam.Exercises ?? [];
        for (var i = 0; i < items.Count; i++)
        {
            var exercise = items[i];
            var path = $"$.exercises[{i}]";
            var ok = Guarded(
                () => ExamExercise.Create(
                    Guid.Empty,
                    exercise.Number,
                    exercise.Title,
                    exercise.QuestionCount,
                    exercise.DurationMinutes,
                    exercise.MaxScore,
                    exercise.PenaltyRatio,
                    exercise.Sections),
                file,
                path,
                errors);

            if (!numbers.Add(exercise.Number))
            {
                errors.Add(new ContentError(file, $"{path}.number", $"El número de ejercicio {exercise.Number} está repetido."));
                ok = false;
            }

            for (var j = 0; j < exercise.Sections.Count; j++)
            {
                if (!knownSections.Contains(exercise.Sections[j]?.Trim()))
                {
                    errors.Add(new ContentError(
                        file,
                        $"{path}.sections[{j}]",
                        $"La sección '{exercise.Sections[j]}' no existe en ningún tema de la oposición."));
                    ok = false;
                }
            }

            if (ok)
            {
                exercises.Add(new ExerciseContent(
                    exercise.Number,
                    exercise.Title.Trim(),
                    exercise.QuestionCount,
                    exercise.DurationMinutes,
                    exercise.MaxScore,
                    exercise.PenaltyRatio,
                    [.. exercise.Sections.Select(s => s.Trim())]));
            }
        }

        return exercises;
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
        if (topicFile.Topic.Trim() != topicFile.Topic)
        {
            errors.Add(new ContentError(file, "$.topic", $"El tema '{topicFile.Topic}' tiene espacios al principio o al final."));
        }
        else if (topicFile.Topic != expected)
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

            if (q.Key.Trim() != q.Key)
            {
                errors.Add(new ContentError(file, $"{path}.key", $"La key '{q.Key}' tiene espacios al principio o al final."));
            }
            else if (!KeyRegex().IsMatch(q.Key))
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

    private static void ReadPracticalCaseFile(
        string root,
        string casePath,
        Guid examId,
        Dictionary<string, string> caseKeys,
        List<PracticalCaseContent> cases,
        List<ContentError> errors)
    {
        var file = Relative(root, casePath);
        var caseFile = ReadJson<PracticalCaseFile>(root, casePath, errors);
        if (caseFile is null)
        {
            return;
        }

        var parts = caseFile.Parts
            .Select(p => new PracticalCasePartData(
                p.Statement,
                p.ModelAnswer,
                [.. p.Criteria.Select(c => new GradingCriterionData(c.Description, c.Points))]))
            .ToList();
        var ok = ValidatePracticalCase(examId, caseFile, parts, file, errors);

        // The domain treats a blank section as "no section", but in a file it is a mistake (the JSON Schema forbids it too).
        if (caseFile.Section is not null && string.IsNullOrWhiteSpace(caseFile.Section))
        {
            errors.Add(new ContentError(file, "$.section", "La section está vacía: omítela o indica el bloque (practical_case.invalid_section)."));
            ok = false;
        }

        if (caseFile.Key.Trim() != caseFile.Key)
        {
            errors.Add(new ContentError(file, "$.key", $"La key '{caseFile.Key}' tiene espacios al principio o al final."));
            ok = false;
        }
        else if (!KeyRegex().IsMatch(caseFile.Key))
        {
            errors.Add(new ContentError(file, "$.key", $"La key '{caseFile.Key}' no es válida: minúsculas, números, guiones y '/' entre segmentos."));
            ok = false;
        }
        else if (caseKeys.TryGetValue(caseFile.Key, out var firstSeen))
        {
            errors.Add(new ContentError(file, "$.key", $"La key '{caseFile.Key}' está repetida (ya aparece en {firstSeen})."));
            ok = false;
        }
        else
        {
            caseKeys[caseFile.Key] = file;
        }

        if (ok)
        {
            cases.Add(new PracticalCaseContent(caseFile.Key, caseFile.Title, caseFile.Section, caseFile.Statement, caseFile.EstimatedMinutes, parts));
        }
    }

    /// <summary>
    /// Applies the domain rules of a practical case. To report the exact JSON path (e.g. <c>$.parts[1].criteria[0].points</c>),
    /// each element is validated on its own by building a throwaway case in which everything else is valid filler.
    /// </summary>
    private static bool ValidatePracticalCase(
        Guid examId,
        PracticalCaseFile caseFile,
        List<PracticalCasePartData> parts,
        string file,
        List<ContentError> errors)
    {
        var before = errors.Count;
        var criterion = new GradingCriterionData("x", 1);
        var filler = new PracticalCasePartData("x", "x", [criterion]);

        Guarded(
            () => PracticalCase.Create(examId, caseFile.Key, caseFile.Title, caseFile.Section, caseFile.Statement, caseFile.EstimatedMinutes, [filler]),
            file,
            "$",
            errors);
        if (parts.Count is < Limits.MinCaseParts or > Limits.MaxCaseParts)
        {
            Guarded(
                () => PracticalCase.Create(examId, "k", "t", null, "s", Limits.MinEstimatedMinutes, [.. Enumerable.Repeat(filler, parts.Count)]),
                file,
                "$",
                errors);
        }

        for (var i = 0; i < parts.Count; i++)
        {
            var part = parts[i];
            var partPath = $"$.parts[{i}]";
            Probe(new PracticalCasePartData(part.Statement, "x", [criterion]), partPath);
            Probe(new PracticalCasePartData("x", part.ModelAnswer, [criterion]), partPath);
            if (part.Criteria.Count is < Limits.MinCriteriaPerPart or > Limits.MaxCriteriaPerPart)
            {
                Probe(new PracticalCasePartData("x", "x", [.. Enumerable.Repeat(criterion, part.Criteria.Count)]), partPath);
            }

            for (var j = 0; j < part.Criteria.Count; j++)
            {
                Probe(new PracticalCasePartData("x", "x", [part.Criteria[j]]), $"{partPath}.criteria[{j}]");
            }
        }

        return errors.Count == before;

        void Probe(PracticalCasePartData probe, string path) =>
            Guarded(() => PracticalCase.Create(examId, "k", "t", null, "s", Limits.MinEstimatedMinutes, [probe]), file, path, errors);
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