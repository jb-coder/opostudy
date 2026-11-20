using OpoStudy.Domain.Common;
using OpoStudy.Domain.Questions;

using Shouldly;

namespace OpoStudy.Domain.Tests;

public sealed class QuestionTests
{
    private static readonly Guid TopicId = Guid.CreateVersion7();

    internal static AnswerOptionData[] Options(int count = 4, int correct = 0) =>
        [.. Enumerable.Range(0, count).Select(i => new AnswerOptionData($"Option {i}", i == correct))];

    internal static Question NewQuestion(int optionCount = 4, int correct = 0) =>
        Question.Create(TopicId, "test/t01/0001", "Statement?", "Explanation", "CE art. 1", Options(optionCount, correct));

    [Fact]
    public void Create_WithOneCorrectOption_CreatesOrderedOptions()
    {
        var question = NewQuestion(correct: 2);

        question.IsActive.ShouldBeTrue();
        question.Options.Select(o => o.Order).ShouldBe([0, 1, 2, 3]);
        question.Options.ShouldAllBe(o => o.QuestionId == question.Id);
        question.CorrectOption.Order.ShouldBe(2);
    }

    [Fact]
    public void Create_WithoutCorrectOption_ThrowsDomainRule()
    {
        AnswerOptionData[] options = [new("A", false), new("B", false)];

        var ex = Should.Throw<DomainRuleException>(() => Question.Create(TopicId, "k", "s", "e", "r", options));

        ex.Code.ShouldBe("question.no_correct_option");
    }

    [Fact]
    public void Create_WithTwoCorrectOptions_ThrowsDomainRule()
    {
        AnswerOptionData[] options = [new("A", true), new("B", true), new("C", false)];

        var ex = Should.Throw<DomainRuleException>(() => Question.Create(TopicId, "k", "s", "e", "r", options));

        ex.Code.ShouldBe("question.multiple_correct_options");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    public void Create_WithOptionCountOutOfRange_ThrowsDomainRule(int count)
    {
        var ex = Should.Throw<DomainRuleException>(() => NewQuestion(optionCount: count));

        ex.Code.ShouldBe("question.invalid_option_count");
    }

    [Theory]
    [InlineData("", "e", "r", "question.invalid_statement")]
    [InlineData("s", "  ", "r", "question.invalid_explanation")]
    [InlineData("s", "e", "", "question.invalid_legal_reference")]
    public void Create_WithEmptyText_ThrowsDomainRule(string statement, string explanation, string legalReference, string code)
    {
        var ex = Should.Throw<DomainRuleException>(() =>
            Question.Create(TopicId, "k", statement, explanation, legalReference, Options()));

        ex.Code.ShouldBe(code);
    }

    [Fact]
    public void Create_WithTooLongExplanation_ThrowsDomainRule()
    {
        var explanation = new string('x', Limits.Explanation + 1);

        var ex = Should.Throw<DomainRuleException>(() => Question.Create(TopicId, "k", "s", explanation, "r", Options()));

        ex.Code.ShouldBe("question.invalid_explanation");
    }

    [Fact]
    public void Update_WithSameOptionCount_KeepsOptionIdsAndChangesCorrectOne()
    {
        var question = NewQuestion(correct: 0);
        var ids = question.Options.Select(o => o.Id).ToList();

        question.Update(TopicId, "New", "New explanation", "CE art. 2", Options(correct: 3));

        question.Options.Select(o => o.Id).ShouldBe(ids);
        question.CorrectOption.Order.ShouldBe(3);
        question.Statement.ShouldBe("New");
    }

    [Fact]
    public void Update_WithInvalidOptionText_LeavesQuestionUntouched()
    {
        var question = NewQuestion(correct: 0);
        AnswerOptionData[] invalid = [new("New A", false), new("New B", true), new(" ", false), new("New D", false)];

        var ex = Should.Throw<DomainRuleException>(() => question.Update(TopicId, "New", "New explanation", "CE art. 2", invalid));

        ex.Code.ShouldBe("option.invalid_text");
        question.Statement.ShouldBe("Statement?");
        question.Explanation.ShouldBe("Explanation");
        question.LegalReference.ShouldBe("CE art. 1");
        question.Options.Select(o => o.Text).ShouldBe(["Option 0", "Option 1", "Option 2", "Option 3"]);
        question.CorrectOption.Order.ShouldBe(0);
    }

    [Fact]
    public void Update_WithInvalidExplanation_LeavesQuestionUntouched()
    {
        var question = NewQuestion(correct: 0);

        Should.Throw<DomainRuleException>(() => question.Update(TopicId, "New", " ", "CE art. 2", Options(correct: 2)));

        question.Statement.ShouldBe("Statement?");
        question.Options.Select(o => o.Text).ShouldBe(["Option 0", "Option 1", "Option 2", "Option 3"]);
        question.CorrectOption.Order.ShouldBe(0);
    }

    [Fact]
    public void Update_ChangingOptionCount_ThrowsDomainRule()
    {
        var question = NewQuestion(optionCount: 4);

        var ex = Should.Throw<DomainRuleException>(() => question.Update(TopicId, "s", "e", "r", Options(count: 3)));

        ex.Code.ShouldBe("question.option_count_changed");
    }
}