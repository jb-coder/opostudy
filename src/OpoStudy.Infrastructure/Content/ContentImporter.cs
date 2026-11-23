using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using OpoStudy.Domain.Common;
using OpoStudy.Domain.Exams;
using OpoStudy.Domain.PracticalCases;
using OpoStudy.Domain.Questions;
using OpoStudy.Infrastructure.Persistence;

namespace OpoStudy.Infrastructure.Content;

/// <summary>
/// Idempotent upsert of the content into the database, in a single transaction (spec 0003).
/// Questions that disappear from the content are deactivated, never deleted.
/// Known limitation: topic numbers that collide with the unique index (exam_id, number) during one import fail (see spec 0003).
/// </summary>
public sealed class ContentImporter(OpoStudyDbContext db)
{
    public async Task<ImportReport> ImportAsync(IReadOnlyList<ExamContent> exams, bool dryRun, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(exams);

        // Aspire enables connection retries, so the whole unit of work runs inside the execution strategy.
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(
            async token =>
            {
                db.ChangeTracker.Clear();
                try
                {
                    await using var transaction = await db.Database.BeginTransactionAsync(token);
                    var report = await ApplyAsync(exams, dryRun, token);
                    if (dryRun)
                    {
                        await transaction.RollbackAsync(token);
                    }
                    else
                    {
                        await transaction.CommitAsync(token);
                    }

                    return report;
                }
                finally
                {
                    db.ChangeTracker.Clear();
                }
            },
            ct);
    }

    private static bool SameOptions(Question question, QuestionContent content)
    {
        var current = question.Options;
        if (current.Count != content.Options.Count)
        {
            return false;
        }

        return !current.Where((o, i) => o.Text != content.Options[i].Text.Trim() || o.IsCorrect != content.Options[i].IsCorrect).Any();
    }

    private static bool SameQuestion(Question question, Guid topicId, QuestionContent content) =>
        question.TopicId == topicId
        && question.Statement == content.Statement.Trim()
        && question.Explanation == content.Explanation.Trim()
        && question.LegalReference == content.LegalReference.Trim()
        && SameOptions(question, content);

    private async Task<ImportReport> ApplyAsync(IReadOnlyList<ExamContent> exams, bool dryRun, CancellationToken ct)
    {
        var examCounts = new Counter();
        var topicCounts = new Counter();
        var questionCounts = new Counter();
        var caseCounts = new Counter();
        var warnings = new List<string>();

        var slugs = exams.Select(e => e.Slug).ToList();
        var contentKeys = exams.SelectMany(e => e.Topics).SelectMany(t => t.Questions).Select(q => q.Key).ToHashSet(StringComparer.Ordinal);
        var keyList = contentKeys.ToList();
        var contentCaseKeys = exams.SelectMany(e => e.PracticalCases).Select(c => c.Key).ToHashSet(StringComparer.Ordinal);
        var caseKeyList = contentCaseKeys.ToList();

        var dbExams = await db.Exams.Where(e => slugs.Contains(e.Slug)).ToDictionaryAsync(e => e.Slug, StringComparer.Ordinal, ct);
        var examIds = dbExams.Values.Select(e => e.Id).ToList();
        var dbTopics = await db.Topics.Where(t => examIds.Contains(t.ExamId)).ToListAsync(ct);
        var topicIds = dbTopics.Select(t => t.Id).ToList();
        var dbQuestions = await db.Questions
            .Where(q => topicIds.Contains(q.TopicId) || keyList.Contains(q.Key))
            .ToDictionaryAsync(q => q.Key, StringComparer.Ordinal, ct);

        var dbCases = await db.PracticalCases
            .Where(c => examIds.Contains(c.ExamId) || caseKeyList.Contains(c.Key))
            .ToDictionaryAsync(c => c.Key, StringComparer.Ordinal, ct);

        foreach (var content in exams)
        {
            var exam = UpsertExam(content, dbExams, examCounts);
            var existingTopics = dbTopics.Where(t => t.ExamId == exam.Id).ToList();
            var topicIdsBySlug = UpsertTopics(exam, content, existingTopics, topicCounts, warnings);

            foreach (var topic in content.Topics)
            {
                foreach (var question in topic.Questions)
                {
                    UpsertQuestion(question, topicIdsBySlug[topic.Slug], dbQuestions, questionCounts);
                }
            }

            foreach (var practicalCase in content.PracticalCases)
            {
                UpsertPracticalCase(practicalCase, exam.Id, dbCases, caseCounts);
            }

            foreach (var practicalCase in dbCases.Values.Where(c => c.IsActive && c.ExamId == exam.Id && !contentCaseKeys.Contains(c.Key)))
            {
                practicalCase.Deactivate();
                caseCounts.Deactivated++;
            }

            // Questions of this exam's topics (including topics no longer in the content) that are gone from the content.
            var examTopicIds = existingTopics.Select(t => t.Id).ToHashSet();
            foreach (var question in dbQuestions.Values.Where(q => q.IsActive && examTopicIds.Contains(q.TopicId) && !contentKeys.Contains(q.Key)))
            {
                question.Deactivate();
                questionCounts.Deactivated++;
            }
        }

        await db.SaveChangesAsync(ct);
        return new ImportReport(examCounts.ToCounts(), topicCounts.ToCounts(), questionCounts.ToCounts(), caseCounts.ToCounts(), warnings, dryRun);
    }

    private Exam UpsertExam(ExamContent content, Dictionary<string, Exam> dbExams, Counter counts)
    {
        if (!dbExams.TryGetValue(content.Slug, out var exam))
        {
            exam = Exam.Create(content.Slug, content.Name, content.Organization, content.Group);
            db.Exams.Add(exam);
            dbExams[content.Slug] = exam;
            counts.Created++;
            return exam;
        }

        var changed = false;
        if (exam.Name != content.Name.Trim() || exam.Organization != content.Organization.Trim() || exam.Group != content.Group.Trim())
        {
            exam.Update(content.Name, content.Organization, content.Group);
            changed = true;
        }

        if (!exam.IsActive)
        {
            exam.Activate();
            counts.Reactivated++;
        }

        if (changed)
        {
            counts.Updated++;
        }

        return exam;
    }

    private Dictionary<string, Guid> UpsertTopics(Exam exam, ExamContent content, List<Topic> existing, Counter counts, List<string> warnings)
    {
        var ids = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var bySlug = existing.ToDictionary(t => t.Slug, StringComparer.Ordinal);
        foreach (var topicContent in content.Topics)
        {
            var section = string.IsNullOrWhiteSpace(topicContent.Section) ? null : topicContent.Section.Trim();
            if (bySlug.TryGetValue(topicContent.Slug, out var topic))
            {
                if (topic.Number != topicContent.Number || topic.Title != topicContent.Title.Trim() || topic.Section != section)
                {
                    topic.Update(topicContent.Number, topicContent.Title, topicContent.Section);
                    counts.Updated++;
                }
            }
            else
            {
                topic = Topic.Create(exam.Id, topicContent.Slug, topicContent.Number, topicContent.Title, topicContent.Section);
                db.Topics.Add(topic);
                counts.Created++;
            }

            ids[topicContent.Slug] = topic.Id;
        }

        var contentSlugs = content.Topics.Select(t => t.Slug).ToHashSet(StringComparer.Ordinal);
        warnings.AddRange(existing
            .Where(t => !contentSlugs.Contains(t.Slug))
            .Select(t => $"El tema '{t.Slug}' de la oposición '{exam.Slug}' ya no está en el contenido; se deja como está."));
        return ids;
    }

    private void UpsertQuestion(QuestionContent content, Guid topicId, Dictionary<string, Question> dbQuestions, Counter counts)
    {
        var options = content.Options;
        if (!dbQuestions.TryGetValue(content.Key, out var question))
        {
            question = Question.Create(topicId, content.Key, content.Statement, content.Explanation, content.LegalReference, options);
            db.Questions.Add(question);
            dbQuestions[content.Key] = question;
            counts.Created++;
            return;
        }

        if (!SameQuestion(question, topicId, content))
        {
            try
            {
                question.Update(topicId, content.Statement, content.Explanation, content.LegalReference, options);
            }
            catch (DomainRuleException ex)
            {
                throw new ContentImportException($"No se puede importar la pregunta '{content.Key}': {ex.Message} [{ex.Code}]", ex);
            }

            counts.Updated++;
        }

        if (!question.IsActive)
        {
            question.Activate();
            counts.Reactivated++;
        }
    }

    private static bool SamePracticalCase(PracticalCase practicalCase, Guid examId, PracticalCaseContent content)
    {
        if (practicalCase.ExamId != examId
            || practicalCase.Title != content.Title.Trim()
            || practicalCase.Section != (string.IsNullOrWhiteSpace(content.Section) ? null : content.Section.Trim())
            || practicalCase.Statement != content.Statement.Trim()
            || practicalCase.EstimatedMinutes != content.EstimatedMinutes
            || practicalCase.Parts.Count != content.Parts.Count)
        {
            return false;
        }

        return !practicalCase.Parts.Where((part, i) => !SamePart(part, content.Parts[i])).Any();
    }

    private static bool SamePart(PracticalCasePart part, PracticalCasePartData data) =>
        part.Statement == data.Statement.Trim()
        && part.ModelAnswer == data.ModelAnswer.Trim()
        && part.Criteria.SequenceEqual(data.Criteria.Select(c => new GradingCriterion(c.Description.Trim(), c.Points)));

    private void UpsertPracticalCase(PracticalCaseContent content, Guid examId, Dictionary<string, PracticalCase> dbCases, Counter counts)
    {
        if (!dbCases.TryGetValue(content.Key, out var practicalCase))
        {
            practicalCase = PracticalCase.Create(examId, content.Key, content.Title, content.Section, content.Statement, content.EstimatedMinutes, content.Parts);
            db.PracticalCases.Add(practicalCase);
            dbCases[content.Key] = practicalCase;
            counts.Created++;
            return;
        }

        if (!SamePracticalCase(practicalCase, examId, content))
        {
            try
            {
                practicalCase.Update(examId, content.Title, content.Section, content.Statement, content.EstimatedMinutes, content.Parts);
            }
            catch (DomainRuleException ex)
            {
                throw new ContentImportException($"No se puede importar el supuesto '{content.Key}': {ex.Message} [{ex.Code}]", ex);
            }

            counts.Updated++;
        }

        if (!practicalCase.IsActive)
        {
            practicalCase.Activate();
            counts.Reactivated++;
        }
    }

    private sealed class Counter
    {
        public int Created { get; set; }
        public int Updated { get; set; }
        public int Deactivated { get; set; }
        public int Reactivated { get; set; }

        public ImportCounts ToCounts() => new(Created, Updated, Deactivated, Reactivated);
    }
}

/// <summary>Registration of the content import services.</summary>
public static class ContentServiceCollectionExtensions
{
    public static IServiceCollection AddContentImport(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddScoped<ContentImporter>();
    }
}