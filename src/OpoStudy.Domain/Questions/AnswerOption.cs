using OpoStudy.Domain.Common;

namespace OpoStudy.Domain.Questions;

/// <summary>An answer option of a <see cref="Question"/>. Only created and modified through the question.</summary>
public sealed class AnswerOption
{
    private AnswerOption()
    {
        Text = string.Empty;
    }

    public Guid Id { get; private set; }
    public Guid QuestionId { get; private set; }
    public int Order { get; private set; }
    public string Text { get; private set; }
    public bool IsCorrect { get; private set; }

    internal static AnswerOption Create(Guid questionId, int order, AnswerOptionData data) => new()
    {
        Id = Guid.CreateVersion7(),
        QuestionId = questionId,
        Order = order,
        Text = Guard.RequiredText(data.Text, Limits.OptionText, "option.invalid_text"),
        IsCorrect = data.IsCorrect,
    };

    internal void Update(AnswerOptionData data)
    {
        Text = Guard.RequiredText(data.Text, Limits.OptionText, "option.invalid_text");
        IsCorrect = data.IsCorrect;
    }
}

/// <summary>Input data of an answer option. Its order is its position in the list.</summary>
public sealed record AnswerOptionData(string Text, bool IsCorrect);