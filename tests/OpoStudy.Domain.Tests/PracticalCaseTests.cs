using OpoStudy.Domain.Common;
using OpoStudy.Domain.PracticalCases;

using Shouldly;

namespace OpoStudy.Domain.Tests;

public sealed class PracticalCaseTests
{
    private static readonly Guid ExamId = Guid.CreateVersion7();

    private static PracticalCasePartData Part(
        string statement = "Part statement",
        string modelAnswer = "Model answer",
        params (string Description, int Points)[] criteria) =>
        new(
            statement,
            modelAnswer,
            [.. (criteria.Length == 0 ? [("Criterion A", 2), ("Criterion B", 3)] : criteria).Select(c => new GradingCriterionData(c.Description, c.Points))]);

    private static PracticalCase NewCase(int partCount = 2) =>
        PracticalCase.Create(ExamId, "test/practical-cases/0001", "Title", "Statement", 45, [.. Enumerable.Range(0, partCount).Select(_ => Part())]);

    private static DomainRuleException CreateFails(
        string key = "k",
        string title = "T",
        string statement = "S",
        int minutes = 30,
        params PracticalCasePartData[] parts) =>
        Should.Throw<DomainRuleException>(() => PracticalCase.Create(ExamId, key, title, statement, minutes, parts.Length == 0 ? [Part()] : parts));

    [Fact]
    public void Create_WithValidData_ComputesMaxPointsAndOrdersParts()
    {
        var practicalCase = NewCase(partCount: 3);

        practicalCase.IsActive.ShouldBeTrue();
        practicalCase.Parts.Select(p => p.Order).ShouldBe([0, 1, 2]);
        practicalCase.Parts.ShouldAllBe(p => p.PracticalCaseId == practicalCase.Id && p.MaxPoints == 5);
        practicalCase.MaxPoints.ShouldBe(15);
        practicalCase.Parts[0].Criteria.ShouldBe([new GradingCriterion("Criterion A", 2), new GradingCriterion("Criterion B", 3)]);
    }

    [Fact]
    public void Create_WithPaddedTexts_TrimsThem()
    {
        var practicalCase = PracticalCase.Create(ExamId, " k ", "  Title ", " Statement ", 30, [Part("  S  ", " M ", ("  D  ", 1))]);

        practicalCase.Key.ShouldBe("k");
        practicalCase.Title.ShouldBe("Title");
        practicalCase.Parts[0].Statement.ShouldBe("S");
        practicalCase.Parts[0].Criteria[0].Description.ShouldBe("D");
    }

    [Fact]
    public void Create_WithBlankKey_ThrowsDomainRule() =>
        CreateFails(key: " ").Code.ShouldBe("practical_case.invalid_key");

    [Fact]
    public void Create_WithBlankTitle_ThrowsDomainRule() =>
        CreateFails(title: " ").Code.ShouldBe("practical_case.invalid_title");

    [Fact]
    public void Create_WithTitleOverLimit_ThrowsDomainRule() =>
        CreateFails(title: new string('a', Limits.Name + 1)).Code.ShouldBe("practical_case.invalid_title");

    [Fact]
    public void Create_WithStatementOverLimit_ThrowsDomainRule() =>
        CreateFails(statement: new string('a', Limits.CaseStatement + 1)).Code.ShouldBe("practical_case.invalid_statement");

    [Theory]
    [InlineData(4)]
    [InlineData(241)]
    [InlineData(0)]
    public void Create_WithEstimatedMinutesOutOfRange_ThrowsDomainRule(int minutes) =>
        CreateFails(minutes: minutes).Code.ShouldBe("practical_case.invalid_estimated_minutes");

    [Theory]
    [InlineData(5)]
    [InlineData(240)]
    public void Create_WithEstimatedMinutesAtTheLimits_Works(int minutes) =>
        PracticalCase.Create(ExamId, "k", "T", "S", minutes, [Part()]).EstimatedMinutes.ShouldBe(minutes);

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void Create_WithPartCountOutOfRange_ThrowsDomainRule(int count) =>
        Should.Throw<DomainRuleException>(() => PracticalCase.Create(ExamId, "k", "T", "S", 30, [.. Enumerable.Range(0, count).Select(_ => Part())]))
            .Code.ShouldBe("practical_case.invalid_part_count");

    [Fact]
    public void Create_WithBlankPartStatement_ThrowsDomainRule() =>
        CreateFails(parts: Part(statement: " ")).Code.ShouldBe("practical_case.invalid_part_statement");

    [Fact]
    public void Create_WithPartStatementOverLimit_ThrowsDomainRule() =>
        CreateFails(parts: Part(statement: new string('a', Limits.Statement + 1))).Code.ShouldBe("practical_case.invalid_part_statement");

    [Fact]
    public void Create_WithBlankModelAnswer_ThrowsDomainRule() =>
        CreateFails(parts: Part(modelAnswer: string.Empty)).Code.ShouldBe("practical_case.invalid_model_answer");

    [Fact]
    public void Create_WithModelAnswerOverLimit_ThrowsDomainRule() =>
        CreateFails(parts: Part(modelAnswer: new string('a', Limits.ModelAnswer + 1))).Code.ShouldBe("practical_case.invalid_model_answer");

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void Create_WithCriterionCountOutOfRange_ThrowsDomainRule(int count) =>
        CreateFails(parts: new PracticalCasePartData("S", "M", [.. Enumerable.Range(0, count).Select(i => new GradingCriterionData("C" + i, 1))]))
            .Code.ShouldBe("practical_case.invalid_criterion_count");

    [Fact]
    public void Create_WithBlankCriterionDescription_ThrowsDomainRule() =>
        CreateFails(parts: Part(criteria: (" ", 1))).Code.ShouldBe("practical_case.invalid_criterion_description");

    [Fact]
    public void Create_WithCriterionDescriptionOverLimit_ThrowsDomainRule() =>
        CreateFails(parts: Part(criteria: (new string('a', Limits.CriterionDescription + 1), 1)))
            .Code.ShouldBe("practical_case.invalid_criterion_description");

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(11)]
    public void Create_WithCriterionPointsOutOfRange_ThrowsDomainRule(int points) =>
        CreateFails(parts: Part(criteria: ("C", points))).Code.ShouldBe("practical_case.invalid_criterion_points");

    [Fact]
    public void Update_WithNewTextsAndCriteria_ChangesThemAndRecomputesMaxPoints()
    {
        var practicalCase = NewCase();
        var otherExamId = Guid.CreateVersion7();

        practicalCase.Update(
            otherExamId,
            "New title",
            "New statement",
            60,
            [Part("New part 0", "New answer 0", ("Only", 7)), Part("New part 1", "New answer 1", ("One", 1), ("Two", 1))]);

        practicalCase.ExamId.ShouldBe(otherExamId);
        practicalCase.Title.ShouldBe("New title");
        practicalCase.EstimatedMinutes.ShouldBe(60);
        practicalCase.Parts[0].Statement.ShouldBe("New part 0");
        practicalCase.Parts[0].Criteria.ShouldBe([new GradingCriterion("Only", 7)]);
        practicalCase.Parts.Select(p => p.MaxPoints).ShouldBe([7, 2]);
        practicalCase.MaxPoints.ShouldBe(9);
    }

    [Fact]
    public void Update_WithDifferentPartCount_ThrowsAndLeavesTheCaseUntouched()
    {
        var practicalCase = NewCase(partCount: 2);

        var ex = Should.Throw<DomainRuleException>(() =>
            practicalCase.Update(ExamId, "Changed", "S", 30, [Part(), Part(), Part()]));

        ex.Code.ShouldBe("practical_case.part_count_changed");
        practicalCase.Title.ShouldBe("Title");
        practicalCase.Parts.Count.ShouldBe(2);
    }

    [Fact]
    public void Update_WithInvalidCriterion_LeavesTheCaseUntouched()
    {
        var practicalCase = NewCase();

        Should.Throw<DomainRuleException>(() =>
            practicalCase.Update(ExamId, "Changed", "S", 30, [Part("Changed 0"), Part(criteria: ("C", 0))]));

        practicalCase.Title.ShouldBe("Title");
        practicalCase.Parts[0].Statement.ShouldBe("Part statement");
        practicalCase.MaxPoints.ShouldBe(10);
    }

    [Fact]
    public void DeactivateAndActivate_ToggleIsActive()
    {
        var practicalCase = NewCase();

        practicalCase.Deactivate();
        practicalCase.IsActive.ShouldBeFalse();
        practicalCase.Activate();
        practicalCase.IsActive.ShouldBeTrue();
    }
}