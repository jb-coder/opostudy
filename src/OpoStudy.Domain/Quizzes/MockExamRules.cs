using OpoStudy.Domain.Common;

namespace OpoStudy.Domain.Quizzes;

/// <summary>Copy of the rules of an exercise taken when a mock exam is created, so later changes to the exercise do not affect it.</summary>
public sealed record MockExamRules
{
    private MockExamRules()
    {
        Title = string.Empty;
    }

    public int ExerciseNumber { get; private init; }
    public string Title { get; private init; }
    public int QuestionCount { get; private init; }
    public TimeSpan Duration { get; private init; }
    public decimal MaxScore { get; private init; }
    public decimal PenaltyRatio { get; private init; }
    public decimal PassScore { get; private init; }

    public static MockExamRules Create(
        int exerciseNumber,
        string title,
        int questionCount,
        TimeSpan duration,
        decimal maxScore,
        decimal penaltyRatio,
        decimal passScore)
    {
        if (passScore <= 0 || passScore > maxScore || decimal.Round(passScore, 2) != passScore)
        {
            throw new DomainRuleException(
                "mock_exam.invalid_pass_score",
                "La nota para aprobar debe estar entre 0 y la nota máxima.");
        }

        return new MockExamRules
        {
            ExerciseNumber = exerciseNumber,
            Title = title,
            QuestionCount = questionCount,
            Duration = duration,
            MaxScore = maxScore,
            PenaltyRatio = penaltyRatio,
            PassScore = passScore,
        };
    }
}
