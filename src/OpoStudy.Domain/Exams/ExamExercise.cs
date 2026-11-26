using OpoStudy.Domain.Common;
using OpoStudy.Domain.Quizzes;

namespace OpoStudy.Domain.Exams;

/// <summary>An exercise (ejercicio) of an exam: a fixed-format multiple-choice paper taken from one or more syllabus sections.</summary>
public sealed class ExamExercise
{
    private ExamExercise()
    {
        Title = string.Empty;
        Sections = [];
    }

    public Guid Id { get; private set; }
    public Guid ExamId { get; private set; }
    public int Number { get; private set; }
    public string Title { get; private set; }
    public int QuestionCount { get; private set; }
    public int DurationMinutes { get; private set; }
    public decimal MaxScore { get; private set; }

    /// <summary>Fraction of a correct answer that each wrong answer subtracts (0 to 1).</summary>
    public decimal PenaltyRatio { get; private set; }

    /// <summary>Topic sections (<see cref="Topic.Section"/>) the questions are drawn from.</summary>
    public IReadOnlyList<string> Sections { get; private set; }

    /// <summary>Half of <see cref="MaxScore"/>, used when the user does not choose a pass score.</summary>
    public decimal DefaultPassScore => Math.Round(MaxScore / 2, 2, MidpointRounding.AwayFromZero);

    public static ExamExercise Create(
        Guid examId,
        int number,
        string title,
        int questionCount,
        int durationMinutes,
        decimal maxScore,
        decimal penaltyRatio,
        IReadOnlyList<string> sections)
    {
        var exercise = new ExamExercise { Id = Guid.CreateVersion7(), ExamId = examId };
        exercise.Update(number, title, questionCount, durationMinutes, maxScore, penaltyRatio, sections);
        return exercise;
    }

    public void Update(
        int number,
        string title,
        int questionCount,
        int durationMinutes,
        decimal maxScore,
        decimal penaltyRatio,
        IReadOnlyList<string> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);

        // Validate everything first so a failure leaves the entity untouched.
        if (number < 1)
        {
            throw new DomainRuleException("exercise.invalid_number", "El número de ejercicio debe ser mayor que 0.");
        }

        var validTitle = Guard.RequiredText(title, Limits.Name, "exercise.invalid_title");
        if (questionCount is < 1 or > Limits.MaxQuestionsPerQuiz)
        {
            throw new DomainRuleException(
                "exercise.invalid_question_count",
                $"El número de preguntas debe estar entre 1 y {Limits.MaxQuestionsPerQuiz}.");
        }

        if (durationMinutes is < 1 or > Limits.MaxMockExamMinutes)
        {
            throw new DomainRuleException(
                "exercise.invalid_duration",
                $"La duración debe estar entre 1 y {Limits.MaxMockExamMinutes} minutos.");
        }

        if (maxScore <= 0 || maxScore > Limits.MaxMockExamScore || HasMoreThanTwoDecimals(maxScore))
        {
            throw new DomainRuleException(
                "exercise.invalid_max_score",
                $"La nota máxima debe ser mayor que 0 y como mucho {Limits.MaxMockExamScore}, con dos decimales como máximo.");
        }

        if (penaltyRatio is < 0 or > 1 || HasMoreThanTwoDecimals(penaltyRatio))
        {
            throw new DomainRuleException(
                "exercise.invalid_penalty_ratio",
                "La penalización debe estar entre 0 y 1, con dos decimales como máximo.");
        }

        if (sections.Count == 0)
        {
            throw new DomainRuleException("exercise.invalid_sections", "El ejercicio necesita al menos una sección.");
        }

        var validSections = sections.Select(s => Guard.RequiredText(s, Limits.Name, "exercise.invalid_sections")).ToList();
        if (validSections.Distinct(StringComparer.Ordinal).Count() != validSections.Count)
        {
            throw new DomainRuleException("exercise.invalid_sections", "Las secciones del ejercicio no pueden repetirse.");
        }

        Number = number;
        Title = validTitle;
        QuestionCount = questionCount;
        DurationMinutes = durationMinutes;
        MaxScore = maxScore;
        PenaltyRatio = penaltyRatio;
        Sections = validSections;
    }

    /// <summary>The rules a mock exam of this exercise is created with. <c>null</c> uses <see cref="DefaultPassScore"/>.</summary>
    public MockExamRules ToRules(decimal? passScore) =>
        MockExamRules.Create(
            Number,
            Title,
            QuestionCount,
            TimeSpan.FromMinutes(DurationMinutes),
            MaxScore,
            PenaltyRatio,
            passScore ?? DefaultPassScore);

    private static bool HasMoreThanTwoDecimals(decimal value) => decimal.Round(value, 2) != value;
}
