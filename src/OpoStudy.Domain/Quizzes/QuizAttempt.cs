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

    /// <summary>Topic of the quiz; <c>null</c> for random quizzes and for reviews of the whole exam.</summary>
    public Guid? TopicId { get; private set; }

    public QuizMode Mode { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }

    /// <summary>Last time a question was answered (UTC). Informational data.</summary>
    public DateTimeOffset? LastAnsweredAt { get; private set; }

    /// <summary>Incremented by every <see cref="Answer"/>, so each answer always modifies the row (and the concurrency token) even if the timestamp repeats.</summary>
    public int Revision { get; private set; }

    /// <summary>The answers sorted by <see cref="QuizAnswer.Order"/>, whatever order the persistence loaded them in.</summary>
    public IReadOnlyList<QuizAnswer> Answers => [.. _answers.OrderBy(a => a.Order)];

    /// <summary>Copy of the rules of the exercise; only for mock exams.</summary>
    public MockExamRules? MockExam { get; private set; }

    /// <summary>Score of a mock exam; <c>null</c> until it is submitted.</summary>
    public decimal? Score { get; private set; }

    /// <summary>Time limit of a mock exam (<c>null</c> for any other quiz).</summary>
    public DateTimeOffset? DeadlineAt => MockExam is null ? null : StartedAt + MockExam.Duration;

    /// <summary>
    /// A finished quiz is not necessarily fully answered: in a mock exam the questions left blank have no answer.
    /// </summary>
    public bool IsFinished => FinishedAt is not null;

    /// <summary>Questions left blank in a submitted mock exam; 0 while it is not finished.</summary>
    public int BlankCount => MockExam is not null && IsFinished ? _answers.Count(a => !a.IsAnswered) : 0;

    public bool Passed => MockExam is not null && Score is { } score && score >= MockExam.PassScore;

    /// <summary>Whether the mock exam was submitted after its deadline plus <see cref="Limits.MockExamSubmissionGrace"/>.</summary>
    public bool IsSubmittedLate => DeadlineAt is { } deadline && FinishedAt > deadline + Limits.MockExamSubmissionGrace;

    public int CorrectCount => _answers.Count(a => a.IsCorrect == true);
    public int IncorrectCount => _answers.Count(a => a.IsCorrect == false);

    public static QuizAttempt Create(
        Guid userId,
        Guid examId,
        Guid? topicId,
        QuizMode mode,
        IReadOnlyList<Guid> questionIds,
        DateTimeOffset now) =>
        Create(Guid.CreateVersion7(), userId, examId, topicId, mode, questionIds, now);

    /// <summary>Creates a quiz with an id chosen by the creator (e.g. the app, when it creates the quiz offline).</summary>
    public static QuizAttempt Create(
        Guid id,
        Guid userId,
        Guid examId,
        Guid? topicId,
        QuizMode mode,
        IReadOnlyList<Guid> questionIds,
        DateTimeOffset now)
    {
        if (mode == QuizMode.MockExam)
        {
            throw new DomainRuleException("quiz.invalid_mode", "Un simulacro solo se crea con sus reglas.");
        }

        return Build(id, userId, examId, topicId, mode, questionIds, now);
    }

    /// <summary>Creates a mock exam with a copy of the rules of its exercise.</summary>
    public static QuizAttempt CreateMockExam(
        Guid userId,
        Guid examId,
        MockExamRules rules,
        IReadOnlyList<Guid> questionIds,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(questionIds);

        if (questionIds.Count != rules.QuestionCount)
        {
            throw new DomainRuleException(
                "mock_exam.invalid_question_count",
                $"El simulacro necesita exactamente {rules.QuestionCount} preguntas.");
        }

        var attempt = Build(Guid.CreateVersion7(), userId, examId, null, QuizMode.MockExam, questionIds, now);
        attempt.MockExam = rules;
        return attempt;
    }

    private static QuizAttempt Build(
        Guid id,
        Guid userId,
        Guid examId,
        Guid? topicId,
        QuizMode mode,
        IReadOnlyList<Guid> questionIds,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(questionIds);

        if (id == Guid.Empty)
        {
            throw new DomainRuleException("quiz.invalid_id", "El identificador del test no es válido.");
        }

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
            Id = id,
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

        if (Mode == QuizMode.MockExam)
        {
            throw new DomainRuleException(
                "mock_exam.answer_requires_submission",
                "En un simulacro las respuestas se envían todas juntas al entregarlo.");
        }

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
        Revision++;
        if (_answers.TrueForAll(a => a.IsAnswered))
        {
            FinishedAt = now;
        }

        return new AnswerResult(option.IsCorrect, question.CorrectOption.Id);
    }

    /// <summary>
    /// Submits a mock exam. <paramref name="selections"/> maps question id to chosen option id; questions that are not in it stay blank.
    /// Everything is validated before anything is recorded.
    /// </summary>
    public IReadOnlyList<SubmittedAnswer> Submit(
        IReadOnlyCollection<Question> questions,
        IReadOnlyDictionary<Guid, Guid> selections,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(questions);
        ArgumentNullException.ThrowIfNull(selections);
        now = now.ToUniversalTime();

        if (Mode != QuizMode.MockExam || MockExam is null)
        {
            throw new DomainRuleException("quiz.not_mock_exam", "Este test no es un simulacro.");
        }

        if (IsFinished)
        {
            throw new DomainRuleException("quiz.finished", "El test ya está finalizado.");
        }

        var recorded = new List<(QuizAnswer Answer, Guid OptionId, bool IsCorrect)>();
        foreach (var (questionId, optionId) in selections)
        {
            var answer = _answers.SingleOrDefault(a => a.QuestionId == questionId)
                ?? throw new DomainRuleException("quiz.foreign_question", "La pregunta no pertenece a este test.");
            var question = questions.FirstOrDefault(q => q.Id == questionId)
                ?? throw new DomainRuleException("quiz.foreign_question", "La pregunta no pertenece a este test.");
            var option = question.Options.SingleOrDefault(o => o.Id == optionId)
                ?? throw new DomainRuleException("quiz.foreign_option", "La opción no pertenece a la pregunta.");
            recorded.Add((answer, option.Id, option.IsCorrect));
        }

        foreach (var (answer, optionId, isCorrect) in recorded)
        {
            answer.Record(optionId, isCorrect, now);
        }

        FinishedAt = now;
        LastAnsweredAt = now;
        Score = MockExamScoring.Calculate(CorrectCount, IncorrectCount, MockExam.QuestionCount, MockExam.MaxScore, MockExam.PenaltyRatio);
        Revision++;
        return [.. recorded.Select(r => new SubmittedAnswer(r.Answer.QuestionId, r.IsCorrect))];
    }
}

/// <summary>Outcome of one answered question of a submitted mock exam.</summary>
public sealed record SubmittedAnswer(Guid QuestionId, bool IsCorrect);

public sealed record AnswerResult(bool IsCorrect, Guid CorrectOptionId);