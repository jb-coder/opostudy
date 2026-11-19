using OpoStudy.Domain.Common;

namespace OpoStudy.Domain.Questions;

/// <summary>
/// A multiple-choice question with its explanation. It is never deleted, only deactivated, to keep the answer history.
/// </summary>
public sealed class Question
{
    private readonly List<AnswerOption> _options = [];

    private Question()
    {
        Key = Statement = Explanation = LegalReference = string.Empty;
    }

    public Guid Id { get; private set; }
    public Guid TopicId { get; private set; }

    /// <summary>Stable identifier coming from the versioned content (e.g. <c>malaga-c1/t01/0001</c>).</summary>
    public string Key { get; private set; }

    public string Statement { get; private set; }
    public string Explanation { get; private set; }
    public string LegalReference { get; private set; }
    public bool IsActive { get; private set; }
    public IReadOnlyList<AnswerOption> Options => _options;

    public AnswerOption CorrectOption => _options.Single(o => o.IsCorrect);

    public static Question Create(
        Guid topicId,
        string key,
        string statement,
        string explanation,
        string legalReference,
        IReadOnlyList<AnswerOptionData> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options);

        var question = new Question
        {
            Id = Guid.CreateVersion7(),
            TopicId = topicId,
            Key = Guard.RequiredText(key, Limits.Key, "question.invalid_key"),
            IsActive = true,
        };
        question.SetTexts(statement, explanation, legalReference);
        for (var order = 0; order < options.Count; order++)
        {
            question._options.Add(AnswerOption.Create(question.Id, order, options[order]));
        }

        return question;
    }

    /// <summary>
    /// Updates the texts and the options (matched by order). The number of options cannot change because
    /// existing answers point to them: in that case create a new question and deactivate this one.
    /// </summary>
    public void Update(
        Guid topicId,
        string statement,
        string explanation,
        string legalReference,
        IReadOnlyList<AnswerOptionData> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options);
        if (options.Count != _options.Count)
        {
            throw new DomainRuleException(
                "question.option_count_changed",
                "No se puede cambiar el número de opciones de una pregunta existente; crea otra y desactiva esta.");
        }

        TopicId = topicId;
        SetTexts(statement, explanation, legalReference);
        foreach (var option in _options)
        {
            option.Update(options[option.Order]);
        }
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    private void SetTexts(string statement, string explanation, string legalReference)
    {
        Statement = Guard.RequiredText(statement, Limits.Statement, "question.invalid_statement");
        Explanation = Guard.RequiredText(explanation, Limits.Explanation, "question.invalid_explanation");
        LegalReference = Guard.RequiredText(legalReference, Limits.LegalReference, "question.invalid_legal_reference");
    }

    private static void ValidateOptions(IReadOnlyList<AnswerOptionData> options)
    {
        if (options.Count is < Limits.MinOptions or > Limits.MaxOptions)
        {
            throw new DomainRuleException(
                "question.invalid_option_count",
                $"Una pregunta debe tener entre {Limits.MinOptions} y {Limits.MaxOptions} opciones.");
        }

        var correctCount = options.Count(o => o.IsCorrect);
        if (correctCount == 0)
        {
            throw new DomainRuleException("question.no_correct_option", "La pregunta no tiene ninguna opción correcta.");
        }

        if (correctCount > 1)
        {
            throw new DomainRuleException("question.multiple_correct_options", "La pregunta tiene más de una opción correcta.");
        }
    }
}