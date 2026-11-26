using OpoStudy.Domain.Common;
using OpoStudy.Domain.Exams;

using Shouldly;

namespace OpoStudy.Domain.Tests;

public sealed class ExamExerciseTests
{
    private static readonly Guid ExamId = Guid.CreateVersion7();

    private static ExamExercise NewExercise(
        int number = 1,
        string title = "Primer ejercicio",
        int questionCount = 55,
        int durationMinutes = 50,
        decimal maxScore = 20m,
        decimal penaltyRatio = 0.5m,
        IReadOnlyList<string>? sections = null) =>
        ExamExercise.Create(ExamId, number, title, questionCount, durationMinutes, maxScore, penaltyRatio, sections ?? ["Bloque I"]);

    [Fact]
    public void Create_WithValidData_SetsPropertiesAndDefaultPassScore()
    {
        var exercise = NewExercise(maxScore: 25m);

        exercise.Id.ShouldNotBe(Guid.Empty);
        exercise.ExamId.ShouldBe(ExamId);
        exercise.Sections.ShouldBe(["Bloque I"]);
        exercise.DefaultPassScore.ShouldBe(12.5m);
    }

    [Fact]
    public void DefaultPassScore_WithOddMaxScore_RoundsAwayFromZero()
    {
        NewExercise(maxScore: 0.05m).DefaultPassScore.ShouldBe(0.03m);
    }

    [Theory]
    [InlineData(0, "t", 5, 5, 10, 0.5, "exercise.invalid_number")]
    [InlineData(1, " ", 5, 5, 10, 0.5, "exercise.invalid_title")]
    [InlineData(1, "t", 0, 5, 10, 0.5, "exercise.invalid_question_count")]
    [InlineData(1, "t", 101, 5, 10, 0.5, "exercise.invalid_question_count")]
    [InlineData(1, "t", 5, 0, 10, 0.5, "exercise.invalid_duration")]
    [InlineData(1, "t", 5, 241, 10, 0.5, "exercise.invalid_duration")]
    [InlineData(1, "t", 5, 5, 0, 0.5, "exercise.invalid_max_score")]
    [InlineData(1, "t", 5, 5, 101, 0.5, "exercise.invalid_max_score")]
    [InlineData(1, "t", 5, 5, 10.001, 0.5, "exercise.invalid_max_score")]
    [InlineData(1, "t", 5, 5, 10, -0.1, "exercise.invalid_penalty_ratio")]
    [InlineData(1, "t", 5, 5, 10, 1.01, "exercise.invalid_penalty_ratio")]
    [InlineData(1, "t", 5, 5, 10, 0.333, "exercise.invalid_penalty_ratio")]
    public void Create_WithInvalidData_ThrowsDomainRule(
        int number, string title, int questionCount, int duration, double maxScore, double penalty, string code)
    {
        var ex = Should.Throw<DomainRuleException>(() =>
            NewExercise(number, title, questionCount, duration, (decimal)maxScore, (decimal)penalty));

        ex.Code.ShouldBe(code);
    }

    [Fact]
    public void Create_WithoutSections_ThrowsDomainRule()
    {
        Should.Throw<DomainRuleException>(() => NewExercise(sections: [])).Code.ShouldBe("exercise.invalid_sections");
    }

    [Fact]
    public void Create_WithDuplicateSections_ThrowsDomainRule()
    {
        Should.Throw<DomainRuleException>(() => NewExercise(sections: ["A", "A"])).Code.ShouldBe("exercise.invalid_sections");
    }

    [Fact]
    public void Update_WithInvalidData_LeavesEntityUntouched()
    {
        var exercise = NewExercise();

        Should.Throw<DomainRuleException>(() => exercise.Update(2, "Nuevo", 10, 5, 10m, 5m, ["X"]));

        exercise.Number.ShouldBe(1);
        exercise.Title.ShouldBe("Primer ejercicio");
        exercise.Sections.ShouldBe(["Bloque I"]);
    }

    [Fact]
    public void ToRules_WithoutPassScore_UsesDefault()
    {
        var rules = NewExercise().ToRules(null);

        rules.PassScore.ShouldBe(10m);
        rules.QuestionCount.ShouldBe(55);
        rules.Duration.ShouldBe(TimeSpan.FromMinutes(50));
        rules.ExerciseNumber.ShouldBe(1);
    }

    [Fact]
    public void ToRules_WithPassScoreAboveMax_ThrowsDomainRule()
    {
        var ex = Should.Throw<DomainRuleException>(() => NewExercise().ToRules(20.01m));

        ex.Code.ShouldBe("mock_exam.invalid_pass_score");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ToRules_WithNonPositivePassScore_ThrowsDomainRule(int passScore)
    {
        Should.Throw<DomainRuleException>(() => NewExercise().ToRules(passScore)).Code.ShouldBe("mock_exam.invalid_pass_score");
    }
}