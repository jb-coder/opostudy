using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

using OpoStudy.Contracts.Quizzes;
using OpoStudy.Infrastructure.Persistence;

using Shouldly;

namespace OpoStudy.Api.IntegrationTests;

public sealed class MistakesEndpointsTests(ApiFactory factory)
{
    private static readonly Uri QuizzesUri = new("/api/v1/quizzes", UriKind.Relative);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string NewUid() => $"uid-{Guid.NewGuid():N}";

    private static Uri MistakesUri(Guid examId) => new($"/api/v1/exams/{examId}/mistakes", UriKind.Relative);

    [Fact]
    public async Task GetMistakes_WithoutToken_Returns401()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(MistakesUri(Guid.NewGuid()), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMistakes_UnknownExam_Returns404()
    {
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.GetAsync(MistakesUri(Guid.NewGuid()), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetMistakes_NewUser_ReturnsZeroAndNoTopics()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());

        var summary = await GetMistakesAsync(client, content.Exam.Id);

        summary.Total.ShouldBe(0);
        summary.Topics.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetMistakes_TwoTopics_ReturnsCountsOrderedByTopicNumber()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var quiz = await CreateQuizAsync(client, content.Exam.Id, null, 100);

        // Topic 2 gets 1 mistake, topic 1 gets 2: topic 1 must come first.
        var topic1 = quiz.Questions.Where(q => content.Questions[q.QuestionId].TopicId == content.Topic1.Id).ToList();
        var topic2 = quiz.Questions.Where(q => content.Questions[q.QuestionId].TopicId == content.Topic2.Id).ToList();
        await AnswerAsync(client, quiz, topic2[0], content, correct: false);
        await AnswerAsync(client, quiz, topic1[0], content, correct: false);
        await AnswerAsync(client, quiz, topic1[1], content, correct: false);
        await AnswerAsync(client, quiz, topic1[2], content, correct: true);

        var summary = await GetMistakesAsync(client, content.Exam.Id);

        summary.Total.ShouldBe(3);
        summary.Topics.ShouldBe([new TopicMistakeCountResponse(content.Topic1.Id, 2), new TopicMistakeCountResponse(content.Topic2.Id, 1)]);
    }

    [Fact]
    public async Task GetMistakes_MistakeThenCorrectAnswerInAnotherQuiz_NoLongerCounts()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero));
        using var host = WithTime(time);
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = ApiFactory.CreateClientFor(host, NewUid());
        var first = await CreateQuizAsync(client, content.Exam.Id, content.Topic2.Id, 1);
        await AnswerAsync(client, first, first.Questions[0], content, correct: false);
        (await GetMistakesAsync(client, content.Exam.Id)).Total.ShouldBe(1);

        time.Advance(TimeSpan.FromMinutes(5));
        var review = await CreateQuizAsync(client, content.Exam.Id, content.Topic2.Id, 5, mistakesOnly: true);
        review.Questions.Count.ShouldBe(1);
        await AnswerAsync(client, review, review.Questions[0], content, correct: true);

        (await GetMistakesAsync(client, content.Exam.Id)).Total.ShouldBe(0);
    }

    [Fact]
    public async Task GetMistakes_CorrectAnswerThenMistakeInAnotherQuiz_CountsAsMistake()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero));
        using var host = WithTime(time);
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = ApiFactory.CreateClientFor(host, NewUid());
        var first = await CreateQuizAsync(client, content.Exam.Id, content.Topic2.Id, 1);
        await AnswerAsync(client, first, first.Questions[0], content, correct: true);
        (await GetMistakesAsync(client, content.Exam.Id)).Total.ShouldBe(0);

        time.Advance(TimeSpan.FromMinutes(5));
        var second = await CreateQuizAsync(client, content.Exam.Id, content.Topic2.Id, 3);
        var same = second.Questions.Single(q => q.QuestionId == first.Questions[0].QuestionId);
        await AnswerAsync(client, second, same, content, correct: false);

        var summary = await GetMistakesAsync(client, content.Exam.Id);
        summary.Total.ShouldBe(1);
        summary.Topics.Single().TopicId.ShouldBe(content.Topic2.Id);
    }

    [Fact]
    public async Task GetMistakes_UnansweredQuestionsAndInactiveQuestions_AreNotCounted()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var quiz = await CreateQuizAsync(client, content.Exam.Id, content.Topic1.Id, 4);
        await AnswerAsync(client, quiz, quiz.Questions[0], content, correct: false);
        await AnswerAsync(client, quiz, quiz.Questions[1], content, correct: false);
        (await GetMistakesAsync(client, content.Exam.Id)).Total.ShouldBe(2);

        await DeactivateAsync(quiz.Questions[1].QuestionId);

        // Questions 2 and 3 stay unanswered, question 1 is now inactive.
        (await GetMistakesAsync(client, content.Exam.Id)).Total.ShouldBe(1);
    }

    [Fact]
    public async Task GetMistakes_MistakesOfAnotherUser_AreNotCounted()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var other = factory.CreateClientFor(NewUid());
        using var client = factory.CreateClientFor(NewUid());
        var quiz = await CreateQuizAsync(other, content.Exam.Id, content.Topic1.Id, 2);
        await AnswerAsync(other, quiz, quiz.Questions[0], content, correct: false);

        (await GetMistakesAsync(client, content.Exam.Id)).Total.ShouldBe(0);
        var response = await client.PostAsJsonAsync(QuizzesUri, new CreateQuizRequest(content.Exam.Id, null, 5, true), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CreateQuiz_MistakesOnlyByTopic_UsesOnlyMistakesOfTheTopicAndRespectsCount()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var quiz = await CreateQuizAsync(client, content.Exam.Id, null, 100);
        var wrong = new List<Guid>();
        foreach (var question in quiz.Questions)
        {
            await AnswerAsync(client, quiz, question, content, correct: false);
            wrong.Add(question.QuestionId);
        }

        var review = await CreateQuizAsync(client, content.Exam.Id, content.Topic1.Id, 2, mistakesOnly: true);

        review.Mode.ShouldBe("Review");
        review.TopicId.ShouldBe(content.Topic1.Id);
        review.Questions.Count.ShouldBe(2);
        review.Questions.ShouldAllBe(q => wrong.Contains(q.QuestionId) && content.Questions[q.QuestionId].TopicId == content.Topic1.Id);
    }

    [Fact]
    public async Task CreateQuiz_MistakesOnlyWholeExam_UsesOnlyMistakes()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var quiz = await CreateQuizAsync(client, content.Exam.Id, null, 100);
        var wrong = quiz.Questions.Take(3).Select(q => q.QuestionId).ToList();
        foreach (var question in quiz.Questions.Take(5))
        {
            await AnswerAsync(client, quiz, question, content, correct: !wrong.Contains(question.QuestionId));
        }

        var review = await CreateQuizAsync(client, content.Exam.Id, null, 50, mistakesOnly: true);

        review.Mode.ShouldBe("Review");
        review.TopicId.ShouldBeNull();
        review.Questions.Select(q => q.QuestionId).ShouldBe(wrong, ignoreOrder: true);

        var list = await client.GetFromJsonAsync<List<QuizSummaryResponse>>(new Uri("/api/v1/quizzes?skip=0&take=1", UriKind.Relative), Ct);
        list.ShouldNotBeNull();
        list.Single().Mode.ShouldBe("Review");
    }

    [Fact]
    public async Task CreateQuiz_MistakesOnlyWithoutMistakes_Returns422NoMistakes()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.PostAsJsonAsync(QuizzesUri, new CreateQuizRequest(content.Exam.Id, content.Topic1.Id, 5, true), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var json = await response.Content.ReadAsStringAsync(Ct);
        json.ShouldContain("quiz.no_mistakes");
        json.ShouldContain("No tienes preguntas falladas pendientes de repasar aqu");
    }

    [Fact]
    public async Task SubmitAnswer_InAReview_UpdatesMistakesAndXp()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var quiz = await CreateQuizAsync(client, content.Exam.Id, content.Topic2.Id, 3);
        foreach (var question in quiz.Questions)
        {
            await AnswerAsync(client, quiz, question, content, correct: false);
        }

        var review = await CreateQuizAsync(client, content.Exam.Id, null, 10, mistakesOnly: true);
        review.Questions.Count.ShouldBe(3);
        AnswerFeedbackResponse? last = null;
        foreach (var question in review.Questions)
        {
            last = await AnswerAsync(client, review, question, content, correct: true);
        }

        last.ShouldNotBeNull();
        last.QuizFinished.ShouldBeTrue();
        last.Xp.Gained.ShouldBeGreaterThan(0);
        last.DailyProgress.AnsweredToday.ShouldBe(6);
        (await GetMistakesAsync(client, content.Exam.Id)).Total.ShouldBe(0);
    }

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, false, true)]
    public async Task GetMistakes_AnswersAtTheSameInstant_NewestQuizIdDecides(bool olderCorrect, bool newerCorrect, bool expectedMistake)
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero));
        using var host = WithTime(time);
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = ApiFactory.CreateClientFor(host, NewUid());
        var older = await CreateQuizAsync(client, content.Exam.Id, content.Topic2.Id, 3);
        await Task.Delay(20, Ct); // UUID v7 ids only order across milliseconds.
        var newer = await CreateQuizAsync(client, content.Exam.Id, content.Topic2.Id, 3);
        var questionId = older.Questions[0].QuestionId;

        // The clock is frozen: both answers have exactly the same AnsweredAt.
        await AnswerAsync(client, older, older.Questions.Single(q => q.QuestionId == questionId), content, olderCorrect);
        await AnswerAsync(client, newer, newer.Questions.Single(q => q.QuestionId == questionId), content, newerCorrect);

        var summary = await GetMistakesAsync(client, content.Exam.Id);
        summary.Total.ShouldBe(expectedMistake ? 1 : 0);
    }

    [Fact]
    public async Task CreateQuiz_MistakesOnlyWithTopicOfAnotherExam_Returns404TopicNotFound()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.PostAsJsonAsync(
            QuizzesUri,
            new CreateQuizRequest(content.Exam.Id, content.OtherExamTopic.Id, 5, true),
            Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("topic.not_found");
    }

    // ---- Helpers ----

    private WebApplicationFactory<Program> WithTime(FakeTimeProvider time) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(time);
        }));

    private async Task DeactivateAsync(Guid questionId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpoStudyDbContext>();
        var question = await db.Questions.FirstAsync(q => q.Id == questionId, Ct);
        question.Deactivate();
        await db.SaveChangesAsync(Ct);
    }

    private static async Task<MistakeSummaryResponse> GetMistakesAsync(HttpClient client, Guid examId)
    {
        var summary = await client.GetFromJsonAsync<MistakeSummaryResponse>(MistakesUri(examId), Ct);
        return summary ?? throw new InvalidOperationException("The mistakes response was empty.");
    }

    private static async Task<QuizResponse> CreateQuizAsync(
        HttpClient client, Guid examId, Guid? topicId, int count, bool mistakesOnly = false)
    {
        var response = await client.PostAsJsonAsync(QuizzesUri, new CreateQuizRequest(examId, topicId, count, mistakesOnly), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var quiz = await response.Content.ReadFromJsonAsync<QuizResponse>(Ct);
        return quiz ?? throw new InvalidOperationException("The quiz response was empty.");
    }

    private static async Task<AnswerFeedbackResponse> AnswerAsync(
        HttpClient client, QuizResponse quiz, QuizQuestionResponse question, SeededContent content, bool correct)
    {
        var correctId = content.Questions[question.QuestionId].CorrectOption.Id;
        var optionId = correct ? correctId : question.Options.First(o => o.Id != correctId).Id;
        var response = await client.PostAsJsonAsync(
            new Uri($"/api/v1/quizzes/{quiz.Id}/answers", UriKind.Relative),
            new SubmitAnswerRequest(question.QuestionId, optionId),
            Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AnswerFeedbackResponse>(Ct);
        return body ?? throw new InvalidOperationException("The answer response was empty.");
    }
}