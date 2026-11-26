using OpoStudy.Domain.Common;
using OpoStudy.Domain.Questions;
using OpoStudy.Domain.Quizzes;

using Shouldly;

namespace OpoStudy.Domain.Tests;

public sealed class MockExamAttemptTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.CreateVersion7();
    private static readonly Guid ExamId = Guid.CreateVersion7();

    private readonly Question _q1 = QuestionTests.NewQuestion(correct: 0);
    private readonly Question _q2 = QuestionTests.NewQuestion(correct: 1);
    private readonly Question _q3 = QuestionTests.NewQuestion(correct: 2);

    private static MockExamRules Rules(int questionCount = 3, decimal maxScore = 3m, decimal penalty = 0.5m, decimal passScore = 1.5m) =>
        MockExamRules.Create(1, "Primer ejercicio", questionCount, TimeSpan.FromMinutes(10), maxScore, penalty, passScore);

    private QuizAttempt NewMockExam(MockExamRules? rules = null) =>
        QuizAttempt.CreateMockExam(UserId, ExamId, rules ?? Rules(), [_q1.Id, _q2.Id, _q3.Id], Now);

    private static Guid Option(Question q, int order) => q.Options.Single(o => o.Order == order).Id;

    [Fact]
    public void CreateMockExam_WithMatchingQuestions_CreatesMockExam()
    {
        var attempt = NewMockExam();

        attempt.Mode.ShouldBe(QuizMode.MockExam);
        attempt.TopicId.ShouldBeNull();
        attempt.MockExam.ShouldNotBeNull();
        attempt.Score.ShouldBeNull();
        attempt.DeadlineAt.ShouldBe(Now.AddMinutes(10));
        attempt.Answers.Count.ShouldBe(3);
        attempt.BlankCount.ShouldBe(0);
    }

    [Fact]
    public void CreateMockExam_WithWrongQuestionCount_ThrowsDomainRule()
    {
        var ex = Should.Throw<DomainRuleException>(() =>
            QuizAttempt.CreateMockExam(UserId, ExamId, Rules(questionCount: 5), [_q1.Id, _q2.Id], Now));

        ex.Code.ShouldBe("mock_exam.invalid_question_count");
    }

    [Fact]
    public void CreateMockExam_WithDuplicateQuestions_ThrowsDomainRule()
    {
        var ex = Should.Throw<DomainRuleException>(() =>
            QuizAttempt.CreateMockExam(UserId, ExamId, Rules(questionCount: 2), [_q1.Id, _q1.Id], Now));

        ex.Code.ShouldBe("quiz.duplicate_questions");
    }

    [Fact]
    public void Create_WithMockExamMode_ThrowsDomainRule()
    {
        var ex = Should.Throw<DomainRuleException>(() =>
            QuizAttempt.Create(UserId, ExamId, null, QuizMode.MockExam, [_q1.Id], Now));

        ex.Code.ShouldBe("quiz.invalid_mode");
    }

    [Fact]
    public void Answer_OnMockExam_ThrowsDomainRule()
    {
        var attempt = NewMockExam();

        var ex = Should.Throw<DomainRuleException>(() => attempt.Answer(_q1, Option(_q1, 0), Now));

        ex.Code.ShouldBe("mock_exam.answer_requires_submission");
    }

    [Fact]
    public void Submit_WithCorrectWrongAndBlank_RecordsOnlySelectedAndScores()
    {
        var attempt = NewMockExam();
        var selections = new Dictionary<Guid, Guid> { [_q1.Id] = Option(_q1, 0), [_q2.Id] = Option(_q2, 0) };

        var results = attempt.Submit([_q1, _q2, _q3], selections, Now.AddMinutes(5));

        results.Select(r => (r.QuestionId, r.IsCorrect)).ShouldBe([(_q1.Id, true), (_q2.Id, false)], ignoreOrder: true);
        attempt.IsFinished.ShouldBeTrue();
        attempt.FinishedAt.ShouldBe(Now.AddMinutes(5));
        attempt.CorrectCount.ShouldBe(1);
        attempt.IncorrectCount.ShouldBe(1);
        attempt.BlankCount.ShouldBe(1);
        attempt.Score.ShouldBe(0.5m); // (1 - 0.5) * 3 / 3
        attempt.Passed.ShouldBeFalse();
        attempt.Revision.ShouldBe(1);
        attempt.Answers.Single(a => a.QuestionId == _q3.Id).IsAnswered.ShouldBeFalse();
    }

    [Fact]
    public void Submit_WithScoreAtPassScore_Passes()
    {
        var attempt = NewMockExam(Rules(passScore: 1m));

        attempt.Submit([_q1, _q2, _q3], new Dictionary<Guid, Guid> { [_q1.Id] = Option(_q1, 0) }, Now.AddMinutes(1));

        attempt.Score.ShouldBe(1m);
        attempt.Passed.ShouldBeTrue();
    }

    [Fact]
    public void Submit_WithForeignQuestion_ThrowsAndRecordsNothing()
    {
        var attempt = NewMockExam();
        var foreign = QuestionTests.NewQuestion();
        var selections = new Dictionary<Guid, Guid> { [_q1.Id] = Option(_q1, 0), [foreign.Id] = Option(foreign, 0) };

        var ex = Should.Throw<DomainRuleException>(() => attempt.Submit([_q1, _q2, _q3, foreign], selections, Now));

        ex.Code.ShouldBe("quiz.foreign_question");
        attempt.IsFinished.ShouldBeFalse();
        attempt.Answers.ShouldAllBe(a => !a.IsAnswered);
    }

    [Fact]
    public void Submit_WithForeignOption_ThrowsAndRecordsNothing()
    {
        var attempt = NewMockExam();
        var selections = new Dictionary<Guid, Guid> { [_q1.Id] = Option(_q1, 0), [_q2.Id] = Option(_q1, 1) };

        var ex = Should.Throw<DomainRuleException>(() => attempt.Submit([_q1, _q2, _q3], selections, Now));

        ex.Code.ShouldBe("quiz.foreign_option");
        attempt.IsFinished.ShouldBeFalse();
        attempt.Answers.ShouldAllBe(a => !a.IsAnswered);
        attempt.Score.ShouldBeNull();
    }

    [Fact]
    public void Submit_Twice_ThrowsFinished()
    {
        var attempt = NewMockExam();
        attempt.Submit([_q1, _q2, _q3], new Dictionary<Guid, Guid>(), Now);

        var ex = Should.Throw<DomainRuleException>(() => attempt.Submit([_q1, _q2, _q3], new Dictionary<Guid, Guid>(), Now));

        ex.Code.ShouldBe("quiz.finished");
    }

    [Fact]
    public void Submit_WithNothingSelected_ScoresZeroAndAllBlank()
    {
        var attempt = NewMockExam();

        attempt.Submit([_q1, _q2, _q3], new Dictionary<Guid, Guid>(), Now);

        attempt.Score.ShouldBe(0m);
        attempt.BlankCount.ShouldBe(3);
    }

    [Fact]
    public void Submit_OnRegularQuiz_ThrowsNotMockExam()
    {
        var attempt = QuizAttempt.Create(UserId, ExamId, null, QuizMode.Random, [_q1.Id], Now);

        var ex = Should.Throw<DomainRuleException>(() => attempt.Submit([_q1], new Dictionary<Guid, Guid>(), Now));

        ex.Code.ShouldBe("quiz.not_mock_exam");
    }

    [Theory]
    [InlineData(10, false)]
    [InlineData(12, false)]
    [InlineData(13, true)]
    public void IsSubmittedLate_DependsOnDeadlineAndGrace(int minutesAfterStart, bool expected)
    {
        var attempt = NewMockExam();
        attempt.Submit([_q1, _q2, _q3], new Dictionary<Guid, Guid>(), Now.AddMinutes(minutesAfterStart));

        attempt.IsSubmittedLate.ShouldBe(expected);
    }

    [Fact]
    public void BlankCount_BeforeSubmission_IsZero()
    {
        NewMockExam().BlankCount.ShouldBe(0);
    }
}