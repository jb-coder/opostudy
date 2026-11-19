using OpoStudy.Domain.Common;
using OpoStudy.Domain.Questions;

namespace OpoStudy.Domain.Quizzes;

/// <summary>A quiz taken by a user: the ordered set of questions and their answers.</summary>
public sealed class QuizAttempt
{
    private readonly List<QuizAnswer> _answers = [];

    private QuizAttempt()
    {
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid ExamId { get; private set; }

    /// <summary>Topic of the quiz; <c>null</c> for random quizzes.</summary>
    public Guid? TopicId { get; private set; }

    public QuizMode Mode { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }
    
    /// <summary>Last time a question was answered (UTC). Always changes on <see cref="Answer"/>, so every answer modifies the row.</summary>
    public DateTimeOffset? LastAnsweredAt { get; private set; }

    /// <summary>The answers sorted by <see cref="QuizAnswer.Order"/>, whatever order the persistence loaded them in.</summary>
    public IReadOnlyList<QuizAnswer> Answers => [.. _answers.OrderBy(a => a.Order)];

    public bool IsFinished => FinishedAt is not null;
    public int CorrectCount => _answers.Count(a => a.IsCorrect == true);
    public int IncorrectCount => _answers.Count(a => a.IsCorrect == false);

    public static QuizAttempt Create(
        Guid userId,
        Guid examId,
        Guid? topicId,
        QuizMode mode,
        IReadOnlyList<Guid> questionIds,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(questionIds);

        if (mode == QuizMode.ByTopic && topicId is null)
        {
            throw new DomainRuleException("quiz.topic_required", "Un test por tema necesita un tema.");
        }

        if (mode == QuizMode.Random && topicId is not null)
        {
            throw new DomainRuleException("quiz.topic_not_allowed", "Un test aleatorio no tiene tema.");
        }

        if (questionIds.Count is 0 or > Limits.MaxQuestionsPerQuiz)
        {
            throw new DomainRuleException(
                "quiz.invalid_question_count",
                $"Un test debe tener entre 1 y {Limits.MaxQuestionsPerQuiz} preguntas.");
        }

        if (questionIds.Distinct().Count() != questionIds.Count)
        {
            throw new DomainRuleException("quiz.duplicate_questions", "Un test no puede repetir preguntas.");
        }

        var attempt = new QuizAttempt
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            ExamId = examId,
            TopicId = topicId,
            Mode = mode,
            StartedAt = now.ToUniversalTime(),
        };
        for (var order = 0; order < questionIds.Count; order++)
        {
            attempt._answers.Add(QuizAnswer.Create(attempt.Id, questionIds[order], order));
        }

        return attempt;
    }

    public AnswerResult Answer(Question question, Guid optionId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(question);
        now = now.ToUniversalTime();

        if (IsFinished)
        {
            throw new DomainRuleException("quiz.finished", "El test ya está finalizado.");
        }

        var answer = _answers.SingleOrDefault(a => a.QuestionId == question.Id)
            ?? throw new DomainRuleException("quiz.foreign_question", "La pregunta no pertenece a este test.");

        if (answer.IsAnswered)
        {
            throw new DomainRuleException("quiz.already_answered", "La pregunta ya está respondida.");
        }

        var option = question.Options.SingleOrDefault(o => o.Id == optionId)
            ?? throw new DomainRuleException("quiz.foreign_option", "La opción no pertenece a la pregunta.");

        answer.Record(option.Id, option.IsCorrect, now);
        LastAnsweredAt = now;
        if (_answers.TrueForAll(a => a.IsAnswered))
        {
            FinishedAt = now;
        }

        return new AnswerResult(option.IsCorrect, question.CorrectOption.Id);
    }
}

public sealed record AnswerResult(bool IsCorrect, Guid CorrectOptionId);