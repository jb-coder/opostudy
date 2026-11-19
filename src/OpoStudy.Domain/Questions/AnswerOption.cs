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
        Text = ValidateText(data),
        IsCorrect = data.IsCorrect,
    };

    internal static string ValidateText(AnswerOptionData data) =>
        Guard.RequiredText(data.Text, Limits.OptionText, "option.invalid_text");

    /// <summary>Applies already validated data (see <see cref="ValidateText"/>); it cannot fail.</summary>
    internal void Apply(string validText, bool isCorrect)
    {
        Text = validText;
        IsCorrect = isCorrect;
    }
}

/// <summary>Input data of an answer option. Its order is its position in the list.</summary>
public sealed record AnswerOptionData(string Text, bool IsCorrect);