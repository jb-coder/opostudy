using Microsoft.Extensions.DependencyInjection;

using OpoStudy.Domain.Exams;
using OpoStudy.Domain.PracticalCases;
using OpoStudy.Domain.Questions;
using OpoStudy.Infrastructure.Persistence;

namespace OpoStudy.Api.IntegrationTests;

/// <summary>Content seeded for one test: every call uses unique slugs and keys, so tests do not interfere.</summary>
/// <param name="Exam">Active exam.</param>
/// <param name="Topic1">Topic 1 of <paramref name="Exam"/>: active questions plus one inactive.</param>
/// <param name="Topic2">Topic 2 of <paramref name="Exam"/>: 3 active questions.</param>
/// <param name="EmptyTopic">Topic 3 of <paramref name="Exam"/>: only an inactive question.</param>
/// <param name="InactiveExam">Inactive exam with one topic and one question.</param>
/// <param name="InactiveExamTopic">Topic of <paramref name="InactiveExam"/>.</param>
/// <param name="OtherExam">Active exam, different from <paramref name="Exam"/>, with one topic.</param>
/// <param name="OtherExamTopic">Topic of <paramref name="OtherExam"/>.</param>
/// <param name="PracticalCases">Practical cases of <paramref name="Exam"/> (keys ordered: A, B; C is inactive) and one of <paramref name="InactiveExam"/>.</param>
/// <param name="Exercise1">Exercise 1 of <paramref name="Exam"/>: 3 questions, 10 minutes, out of 10, drawn from "Block I" (4 active questions).</param>
/// <param name="Exercise2">Exercise 2 of <paramref name="Exam"/>: 5 questions from "Block I", so there are not enough.</param>
/// <param name="Questions">Every seeded question by id (with its options, so the correct one is known).</param>
internal sealed record SeededContent(
    Exam Exam,
    Topic Topic1,
    Topic Topic2,
    Topic EmptyTopic,
    Exam InactiveExam,
    Topic InactiveExamTopic,
    Exam OtherExam,
    Topic OtherExamTopic,
    IReadOnlyDictionary<Guid, Question> Questions,
    SeededPracticalCases PracticalCases,
    ExamExercise Exercise1,
    ExamExercise Exercise2)
{
    public IEnumerable<Question> ActiveQuestionsOf(Topic topic) =>
        Questions.Values.Where(q => q.TopicId == topic.Id && q.IsActive);

    public Question InactiveQuestionOf(Topic topic) =>
        Questions.Values.Single(q => q.TopicId == topic.Id && !q.IsActive);
}

internal static class ContentSeeder
{
    public static async Task<SeededContent> SeedAsync(IServiceProvider services, int activeQuestionsInTopic1 = 4)
    {
        var id = Guid.NewGuid().ToString("N");
        var exam = Exam.Create($"exam-{id}", $"Exam {id}", "Test organization", "C1");
        var inactiveExam = Exam.Create($"inactive-{id}", $"Inactive {id}", "Test organization", "C1");
        inactiveExam.Deactivate();
        var otherExam = Exam.Create($"other-{id}", $"Other {id}", "Test organization", "C1");

        var topic1 = Topic.Create(exam.Id, "topic-1", 1, "Topic one", "Block I");
        var topic2 = Topic.Create(exam.Id, "topic-2", 2, "Topic two", null);
        var emptyTopic = Topic.Create(exam.Id, "topic-3", 3, "Empty topic", null);
        var inactiveExamTopic = Topic.Create(inactiveExam.Id, "topic-1", 1, "Inactive exam topic", null);
        var otherExamTopic = Topic.Create(otherExam.Id, "topic-1", 1, "Other exam topic", null);

        var questions = new List<Question>();
        for (var i = 0; i < activeQuestionsInTopic1; i++)
        {
            questions.Add(NewQuestion(topic1, id, "t1", i, correctIndex: i % 4));
        }

        var inactive = NewQuestion(topic1, id, "t1-inactive", 0, correctIndex: 0);
        inactive.Deactivate();
        questions.Add(inactive);

        for (var i = 0; i < 3; i++)
        {
            questions.Add(NewQuestion(topic2, id, "t2", i, correctIndex: 1));
        }

        var emptyTopicQuestion = NewQuestion(emptyTopic, id, "t3-inactive", 0, correctIndex: 2);
        emptyTopicQuestion.Deactivate();
        questions.Add(emptyTopicQuestion);

        questions.Add(NewQuestion(inactiveExamTopic, id, "inactive-exam", 0, correctIndex: 0));
        questions.Add(NewQuestion(otherExamTopic, id, "other-exam", 0, correctIndex: 3));

        var caseA = NewCase(exam, id, "a", partCount: 2);
        var caseB = NewCase(exam, id, "b", partCount: 3);
        var caseC = NewCase(exam, id, "c", partCount: 1);
        caseC.Deactivate();
        var caseOfInactiveExam = NewCase(inactiveExam, id, "inactive-exam", partCount: 1);

        var exercise1 = ExamExercise.Create(exam.Id, 1, "Exercise one", 3, 10, 10m, 0.5m, ["Block I"]);
        var exercise2 = ExamExercise.Create(exam.Id, 2, "Exercise two", 5, 10, 10m, 0.5m, ["Block I"]);

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpoStudyDbContext>();
        db.AddRange(exam, inactiveExam, otherExam, topic1, topic2, emptyTopic, inactiveExamTopic, otherExamTopic);
        db.AddRange(questions);
        db.AddRange(caseA, caseB, caseC, caseOfInactiveExam);
        db.AddRange(exercise1, exercise2);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return new SeededContent(
            exam,
            topic1,
            topic2,
            emptyTopic,
            inactiveExam,
            inactiveExamTopic,
            otherExam,
            otherExamTopic,
            questions.ToDictionary(q => q.Id),
            new SeededPracticalCases(caseA, caseB, caseC, caseOfInactiveExam),
            exercise1,
            exercise2);
    }

    private static Question NewQuestion(Topic topic, string id, string prefix, int index, int correctIndex) =>
        Question.Create(
            topic.Id,
            $"{id}/{prefix}/{index}",
            $"Statement {prefix} {index}?",
            $"Explanation {prefix} {index}",
            $"CE art. {index + 1}",
            [.. Enumerable.Range(0, 4).Select(o => new AnswerOptionData($"Option {o}", o == correctIndex))]);

    private static PracticalCase NewCase(Exam exam, string id, string suffix, int partCount) =>
        PracticalCase.Create(
            exam.Id,
            $"{id}/practical-cases/{suffix}",
            $"Case {suffix}",
            suffix == "a" ? "Section a" : null,
            $"Statement of case {suffix}",
            30,
            [.. Enumerable.Range(0, partCount).Select(i => new PracticalCasePartData(
                $"Part {i} of {suffix}",
                $"SECRET-MODEL-ANSWER {i}",
                [new GradingCriterionData($"SECRET-CRITERION {i}", 2), new GradingCriterionData("Another criterion", 3)]))]);
}

internal sealed record SeededPracticalCases(
    PracticalCase CaseA,
    PracticalCase CaseB,
    PracticalCase InactiveCase,
    PracticalCase CaseOfInactiveExam);