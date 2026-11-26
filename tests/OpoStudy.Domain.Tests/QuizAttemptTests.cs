using OpoStudy.Domain.Common;
using OpoStudy.Domain.Questions;
using OpoStudy.Domain.Quizzes;

using Shouldly;

namespace OpoStudy.Domain.Tests;

public sealed class QuizAttemptTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 10, 0, 0, TimeSpan.FromHours(1));
    private static readonly Guid UserId = Guid.CreateVersion7();
    private static readonly Guid ExamId = Guid.CreateVersion7();

    private readonly Question _q1 = QuestionTests.NewQuestion(correct: 0);
    private readonly Question _q2 = QuestionTests.NewQuestion(correct: 1);

    private QuizAttempt NewAttempt() =>
        QuizAttempt.Create(UserId, ExamId, null, QuizMode.Random, [_q1.Id, _q2.Id], Now);

    [Fact]
    public void Create_WithQuestions_CreatesEmptyOrderedAnswersAndUtcDate()
    {
        var attempt = NewAttempt();

        attempt.Answers.Select(a => (a.QuestionId, a.Order)).ShouldBe([(_q1.Id, 0), (_q2.Id, 1)]);
        attempt.Answers.ShouldAllBe(a => !a.IsAnswered);
        attempt.StartedAt.Offset.ShouldBe(TimeSpan.Zero);
        attempt.StartedAt.ShouldBe(Now);
        attempt.IsFinished.ShouldBeFalse();
        attempt.Revision.ShouldBe(0);
    }

    [Fact]
    public void Create_WithGivenId_UsesThatId()
    {
        var id = Guid.CreateVersion7();

        var attempt = QuizAttempt.Create(id, UserId, ExamId, null, QuizMode.Random, [_q1.Id, _q2.Id], Now);

        attempt.Id.ShouldBe(id);
        attempt.Answers.Select(a => a.QuizAttemptId).ShouldAllBe(x => x == id);
    }

    [Fact]
    public void Create_WithEmptyId_ThrowsDomainRule()
    {
        var ex = Should.Throw<DomainRuleException>(() =>
            QuizAttempt.Create(Guid.Empty, UserId, ExamId, null, QuizMode.Random, [_q1.Id], Now));

        ex.Code.ShouldBe("quiz.invalid_id");
    }

    [Fact]
    public void Create_ByTopicWithoutTopic_ThrowsDomainRule()
    {
        var ex = Should.Throw<DomainRuleException>(() =>
            QuizAttempt.Create(UserId, ExamId, null, QuizMode.ByTopic, [_q1.Id], Now));

        ex.Code.ShouldBe("quiz.topic_required");
    }

    [Fact]
    public void Create_RandomWithTopic_ThrowsDomainRule()
    {
        var ex = Should.Throw<DomainRuleException>(() =>
            QuizAttempt.Create(UserId, ExamId, Guid.CreateVersion7(), QuizMode.Random, [_q1.Id], Now));

        ex.Code.ShouldBe("quiz.topic_not_allowed");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Create_SpacedReviewWithOrWithoutTopic_Succeeds(bool withTopic)
    {
        Guid? topicId = withTopic ? Guid.CreateVersion7() : null;

        var attempt = QuizAttempt.Create(UserId, ExamId, topicId, QuizMode.SpacedReview, [_q1.Id, _q2.Id], Now);

        attempt.Mode.ShouldBe(QuizMode.SpacedReview);
        attempt.TopicId.ShouldBe(topicId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Create_ReviewWithOrWithoutTopic_Succeeds(bool withTopic)
    {
        Guid? topicId = withTopic ? Guid.CreateVersion7() : null;

        var attempt = QuizAttempt.Create(UserId, ExamId, topicId, QuizMode.Review, [_q1.Id, _q2.Id], Now);

        attempt.Mode.ShouldBe(QuizMode.Review);
        attempt.TopicId.ShouldBe(topicId);
        attempt.Answers.Count.ShouldBe(2);
    }

    [Fact]
    public void Create_ReviewWithoutQuestions_ThrowsDomainRule()
    {
        var ex = Should.Throw<DomainRuleException>(() =>
            QuizAttempt.Create(UserId, ExamId, null, QuizMode.Review, [], Now));

        ex.Code.ShouldBe("quiz.invalid_question_count");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(Limits.MaxQuestionsPerQuiz + 1)]
    public void Create_WithQuestionCountOutOfRange_ThrowsDomainRule(int count)
    {
        var ids = Enumerable.Range(0, count).Select(_ => Guid.CreateVersion7()).ToList();

        var ex = Should.Throw<DomainRuleException>(() => QuizAttempt.Create(UserId, ExamId, null, QuizMode.Random, ids, Now));

        ex.Code.ShouldBe("quiz.invalid_question_count");
    }

    [Fact]
    public void Create_WithDuplicateQuestions_ThrowsDomainRule()
    {
        var ex = Should.Throw<DomainRuleException>(() =>
            QuizAttempt.Create(UserId, ExamId, null, QuizMode.Random, [_q1.Id, _q1.Id], Now));

        ex.Code.ShouldBe("quiz.duplicate_questions");
    }

    [Fact]
    public void Answer_CorrectOption_ReturnsCorrect()
    {
        var attempt = NewAttempt();

        var result = attempt.Answer(_q1, _q1.CorrectOption.Id, Now);

        result.ShouldBe(new AnswerResult(true, _q1.CorrectOption.Id));
        attempt.CorrectCount.ShouldBe(1);
        attempt.IncorrectCount.ShouldBe(0);
    }

    [Fact]
    public void Answer_Always_SetsLastAnsweredAtInUtc()
    {
        var attempt = NewAttempt();
        attempt.LastAnsweredAt.ShouldBeNull();

        attempt.Answer(_q1, _q1.CorrectOption.Id, Now.AddMinutes(5));

        attempt.LastAnsweredAt.ShouldBe(Now.AddMinutes(5));
        attempt.LastAnsweredAt!.Value.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void Answer_TwiceWithSameNow_IncrementsRevisionEachTime()
    {
        var attempt = NewAttempt();

        attempt.Answer(_q1, _q1.CorrectOption.Id, Now);
        attempt.Answer(_q2, _q2.CorrectOption.Id, Now);

        attempt.Revision.ShouldBe(2);
    }

    [Fact]
    public void Answer_WrongOption_ReturnsIncorrectAndCorrectOption()
    {
        var attempt = NewAttempt();
        var wrong = _q1.Options.First(o => !o.IsCorrect);

        var result = attempt.Answer(_q1, wrong.Id, Now);

        result.ShouldBe(new AnswerResult(false, _q1.CorrectOption.Id));
        attempt.IncorrectCount.ShouldBe(1);
        var answer = attempt.Answers.Single(a => a.QuestionId == _q1.Id);
        answer.SelectedOptionId.ShouldBe(wrong.Id);
        answer.AnsweredAt.ShouldNotBeNull().Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void Answer_ForeignQuestion_ThrowsDomainRule()
    {
        var attempt = NewAttempt();
        var foreign = QuestionTests.NewQuestion();

        var ex = Should.Throw<DomainRuleException>(() => attempt.Answer(foreign, foreign.CorrectOption.Id, Now));

        ex.Code.ShouldBe("quiz.foreign_question");
    }

    [Fact]
    public void Answer_Twice_ThrowsDomainRule()
    {
        var attempt = NewAttempt();
        attempt.Answer(_q1, _q1.CorrectOption.Id, Now);

        var ex = Should.Throw<DomainRuleException>(() => attempt.Answer(_q1, _q1.CorrectOption.Id, Now));

        ex.Code.ShouldBe("quiz.already_answered");
    }

    [Fact]
    public void Answer_OptionOfAnotherQuestion_ThrowsDomainRule()
    {
        var attempt = NewAttempt();

        var ex = Should.Throw<DomainRuleException>(() => attempt.Answer(_q1, _q2.CorrectOption.Id, Now));

        ex.Code.ShouldBe("quiz.foreign_option");
    }

    [Fact]
    public void Answer_LastQuestion_FinishesTheAttempt()
    {
        var attempt = NewAttempt();
        var end = Now.AddMinutes(5);

        attempt.Answer(_q1, _q1.CorrectOption.Id, Now);
        attempt.IsFinished.ShouldBeFalse();
        attempt.Answer(_q2, _q2.Options.First(o => !o.IsCorrect).Id, end);

        attempt.IsFinished.ShouldBeTrue();
        attempt.FinishedAt.ShouldBe(end);
        attempt.CorrectCount.ShouldBe(1);
        attempt.IncorrectCount.ShouldBe(1);
    }

    [Fact]
    public void Answer_FinishedAttempt_ThrowsDomainRule()
    {
        var attempt = QuizAttempt.Create(UserId, ExamId, null, QuizMode.Random, [_q1.Id], Now);
        attempt.Answer(_q1, _q1.CorrectOption.Id, Now);

        var ex = Should.Throw<DomainRuleException>(() => attempt.Answer(_q1, _q1.CorrectOption.Id, Now));

        ex.Code.ShouldBe("quiz.finished");
    }
}