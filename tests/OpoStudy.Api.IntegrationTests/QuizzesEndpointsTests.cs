using System.Net;
using System.Net.Http.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using OpoStudy.Contracts.Quizzes;
using OpoStudy.Infrastructure.Persistence;

using Shouldly;

namespace OpoStudy.Api.IntegrationTests;

public sealed class QuizzesEndpointsTests(ApiFactory factory)
{
    private static readonly Uri QuizzesUri = new("/api/v1/quizzes", UriKind.Relative);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string NewUid() => $"uid-{Guid.NewGuid():N}";

    private static Uri QuizUri(Guid quizId) => new($"/api/v1/quizzes/{quizId}", UriKind.Relative);

    private static Uri AnswersUri(Guid quizId) => new($"/api/v1/quizzes/{quizId}/answers", UriKind.Relative);

    // ---- Create ----

    [Fact]
    public async Task CreateQuiz_WithoutToken_Returns401()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(QuizzesUri, new CreateQuizRequest(Guid.NewGuid(), null, 5), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateQuiz_ByTopicWithMoreRequestedThanAvailable_ReturnsAllActiveQuestionsOfTheTopic()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.PostAsJsonAsync(QuizzesUri, new CreateQuizRequest(content.Exam.Id, content.Topic1.Id, 50), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var quiz = await response.Content.ReadFromJsonAsync<QuizResponse>(Ct);
        quiz.ShouldNotBeNull();
        response.Headers.Location?.ToString().ShouldBe($"/api/v1/quizzes/{quiz.Id}");
        quiz.Mode.ShouldBe("ByTopic");
        quiz.TopicId.ShouldBe(content.Topic1.Id);
        quiz.Questions.Count.ShouldBe(4);
        quiz.Questions.Select(q => q.QuestionId).ShouldBe(
            content.ActiveQuestionsOf(content.Topic1).Select(q => q.Id), ignoreOrder: true);
        quiz.Questions.Select(q => q.QuestionId).ShouldNotContain(content.InactiveQuestionOf(content.Topic1).Id);
        quiz.Questions.Select(q => q.Order).ShouldBe([0, 1, 2, 3]);
        quiz.Questions.ShouldAllBe(q => q.Feedback == null && q.Options.Count == 4);
    }

    [Fact]
    public async Task CreateQuiz_WithFewerRequestedThanAvailable_ReturnsRequestedCount()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.PostAsJsonAsync(QuizzesUri, new CreateQuizRequest(content.Exam.Id, content.Topic1.Id, 2), Ct);

        var quiz = await response.Content.ReadFromJsonAsync<QuizResponse>(Ct);
        quiz.ShouldNotBeNull();
        quiz.Questions.Count.ShouldBe(2);
    }

    [Fact]
    public async Task CreateQuiz_Response_DoesNotRevealAnswers()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.PostAsJsonAsync(QuizzesUri, new CreateQuizRequest(content.Exam.Id, content.Topic1.Id, 4), Ct);

        var json = await response.Content.ReadAsStringAsync(Ct);
        json.ShouldNotContain("isCorrect", Case.Insensitive);
        json.ShouldNotContain("explanation", Case.Insensitive);
        json.ShouldNotContain("legalReference", Case.Insensitive);
        json.ShouldContain("\"feedback\":null");
    }

    [Fact]
    public async Task CreateQuiz_Random_ReturnsQuestionsOfSeveralTopicsOfTheExam()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.PostAsJsonAsync(QuizzesUri, new CreateQuizRequest(content.Exam.Id, null, 100), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var quiz = await response.Content.ReadFromJsonAsync<QuizResponse>(Ct);
        quiz.ShouldNotBeNull();
        quiz.Mode.ShouldBe("Random");
        quiz.TopicId.ShouldBeNull();
        quiz.Questions.Count.ShouldBe(7); // 4 + 3 active questions; nothing from other exams or inactive ones.
        var topicIds = quiz.Questions.Select(q => content.Questions[q.QuestionId].TopicId).Distinct().ToList();
        topicIds.ShouldBe([content.Topic1.Id, content.Topic2.Id], ignoreOrder: true);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task CreateQuiz_WithQuestionCountOutOfRange_ReturnsValidationProblem(int questionCount)
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.PostAsJsonAsync(QuizzesUri, new CreateQuizRequest(content.Exam.Id, content.Topic1.Id, questionCount), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("questionCount");
    }

    [Fact]
    public async Task CreateQuiz_WithEmptyExamId_ReturnsValidationProblem()
    {
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.PostAsJsonAsync(QuizzesUri, new CreateQuizRequest(Guid.Empty, null, 5), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("examId");
    }

    [Fact]
    public async Task CreateQuiz_WithEmptyTopicId_ReturnsValidationProblem()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.PostAsJsonAsync(QuizzesUri, new CreateQuizRequest(content.Exam.Id, Guid.Empty, 5), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("topicId");
    }

    [Fact]
    public async Task CreateQuiz_WithEmptyJsonBody_Returns400Problem()
    {
        using var client = factory.CreateClientFor(NewUid());
        using var content = new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync(QuizzesUri, content, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task CreateQuiz_WithoutBody_Returns400Problem()
    {
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.PostAsync(QuizzesUri, null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task CreateQuiz_WithTopicOfAnotherExam_Returns404()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.PostAsJsonAsync(QuizzesUri, new CreateQuizRequest(content.Exam.Id, content.OtherExamTopic.Id, 5), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "topic.not_found");
    }

    [Fact]
    public async Task CreateQuiz_WithInactiveExam_Returns404()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.PostAsJsonAsync(QuizzesUri, new CreateQuizRequest(content.InactiveExam.Id, null, 5), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "exam.not_found");
    }

    [Fact]
    public async Task CreateQuiz_WithTopicWithoutActiveQuestions_Returns422()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.PostAsJsonAsync(QuizzesUri, new CreateQuizRequest(content.Exam.Id, content.EmptyTopic.Id, 5), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "quiz.no_questions_available");
    }

    // ---- Answer ----

    [Fact]
    public async Task SubmitAnswer_WithCorrectAndWrongOptions_ReturnsFeedbackAndFinishesOnLastAnswer()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var quiz = await CreateQuizAsync(client, content.Exam.Id, content.Topic2.Id, 3);
        var first = quiz.Questions[0];
        var second = quiz.Questions[1];
        var third = quiz.Questions[2];

        // Correct answer.
        var firstQuestion = content.Questions[first.QuestionId];
        var correct = await AnswerAsync(client, quiz.Id, first.QuestionId, firstQuestion.CorrectOption.Id);
        correct.StatusCode.ShouldBe(HttpStatusCode.OK);
        var correctBody = await correct.Content.ReadFromJsonAsync<AnswerFeedbackResponse>(Ct);
        correctBody.ShouldNotBeNull();
        correctBody.Feedback.IsCorrect.ShouldBeTrue();
        correctBody.Feedback.CorrectOptionId.ShouldBe(firstQuestion.CorrectOption.Id);
        correctBody.Feedback.SelectedOptionId.ShouldBe(firstQuestion.CorrectOption.Id);
        correctBody.Feedback.Explanation.ShouldBe(firstQuestion.Explanation);
        correctBody.Feedback.LegalReference.ShouldBe(firstQuestion.LegalReference);
        correctBody.QuizFinished.ShouldBeFalse();
        (correctBody.CorrectCount, correctBody.IncorrectCount, correctBody.AnsweredCount, correctBody.QuestionCount).ShouldBe((1, 0, 1, 3));

        // Wrong answer.
        var secondQuestion = content.Questions[second.QuestionId];
        var wrongOption = secondQuestion.Options.First(o => !o.IsCorrect);
        var wrong = await AnswerAsync(client, quiz.Id, second.QuestionId, wrongOption.Id);
        var wrongBody = await wrong.Content.ReadFromJsonAsync<AnswerFeedbackResponse>(Ct);
        wrongBody.ShouldNotBeNull();
        wrongBody.Feedback.IsCorrect.ShouldBeFalse();
        wrongBody.Feedback.CorrectOptionId.ShouldBe(secondQuestion.CorrectOption.Id);
        wrongBody.Feedback.SelectedOptionId.ShouldBe(wrongOption.Id);
        wrongBody.QuizFinished.ShouldBeFalse();

        // Last answer finishes the quiz.
        var thirdQuestion = content.Questions[third.QuestionId];
        var last = await AnswerAsync(client, quiz.Id, third.QuestionId, thirdQuestion.CorrectOption.Id);
        var lastBody = await last.Content.ReadFromJsonAsync<AnswerFeedbackResponse>(Ct);
        lastBody.ShouldNotBeNull();
        lastBody.QuizFinished.ShouldBeTrue();
        (lastBody.CorrectCount, lastBody.IncorrectCount, lastBody.AnsweredCount, lastBody.QuestionCount).ShouldBe((2, 1, 3, 3));
    }

    [Fact]
    public async Task SubmitAnswer_WithEmptyIds_ReturnsValidationProblem()
    {
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.PostAsJsonAsync(AnswersUri(Guid.NewGuid()), new SubmitAnswerRequest(Guid.Empty, Guid.Empty), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadAsStringAsync(Ct);
        json.ShouldContain("questionId");
        json.ShouldContain("optionId");
    }

    [Fact]
    public async Task SubmitAnswer_RepeatedQuestion_Returns409()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var quiz = await CreateQuizAsync(client, content.Exam.Id, content.Topic2.Id, 3);
        var question = quiz.Questions[0];
        var optionId = content.Questions[question.QuestionId].CorrectOption.Id;
        (await AnswerAsync(client, quiz.Id, question.QuestionId, optionId)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await AnswerAsync(client, quiz.Id, question.QuestionId, optionId);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "quiz.already_answered");
    }

    [Fact]
    public async Task SubmitAnswer_WithOptionOfAnotherQuestion_Returns422()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var quiz = await CreateQuizAsync(client, content.Exam.Id, content.Topic2.Id, 3);
        var optionOfOther = quiz.Questions[1].Options[0].Id;

        var response = await AnswerAsync(client, quiz.Id, quiz.Questions[0].QuestionId, optionOfOther);

        await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "quiz.foreign_option");
    }

    [Fact]
    public async Task SubmitAnswer_WithQuestionOutsideTheQuiz_Returns422()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var quiz = await CreateQuizAsync(client, content.Exam.Id, content.Topic2.Id, 3);
        var outsider = content.ActiveQuestionsOf(content.Topic1).First();

        var response = await AnswerAsync(client, quiz.Id, outsider.Id, outsider.CorrectOption.Id);

        await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "quiz.foreign_question");
    }

    [Fact]
    public async Task SubmitAnswer_OnFinishedQuiz_Returns409()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var quiz = await CreateQuizAsync(client, content.Exam.Id, content.Topic1.Id, 1);
        var question = content.Questions[quiz.Questions[0].QuestionId];
        (await AnswerAsync(client, quiz.Id, question.Id, question.CorrectOption.Id)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await AnswerAsync(client, quiz.Id, question.Id, question.CorrectOption.Id);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "quiz.finished");
    }

    [Fact]
    public async Task SubmitAnswer_ConcurrentAnswersToTheSameQuiz_NeverReturn500AndStoreOnlyAcceptedOnes()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services, activeQuestionsInTopic1: 10);
        using var client = factory.CreateClientFor(NewUid());
        var quiz = await CreateQuizAsync(client, content.Exam.Id, content.Topic1.Id, 10);

        var responses = await Task.WhenAll(quiz.Questions.Select(q =>
            AnswerAsync(client, quiz.Id, q.QuestionId, content.Questions[q.QuestionId].CorrectOption.Id)));

        var statuses = responses.Select(r => r.StatusCode).ToList();
        statuses.ShouldAllBe(s => s == HttpStatusCode.OK || s == HttpStatusCode.Conflict);
        var accepted = statuses.Count(s => s == HttpStatusCode.OK);
        accepted.ShouldBeGreaterThanOrEqualTo(1);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpoStudyDbContext>();
        var stored = await db.QuizAttempts.AsNoTracking().SingleAsync(a => a.Id == quiz.Id, Ct);
        stored.Answers.Count(a => a.IsAnswered).ShouldBe(accepted);
    }

    // ---- Read ----

    [Fact]
    public async Task GetQuiz_AfterAnsweringOneQuestion_ReturnsFeedbackOnlyForThatQuestion()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var quiz = await CreateQuizAsync(client, content.Exam.Id, content.Topic2.Id, 3);
        var answered = content.Questions[quiz.Questions[1].QuestionId];
        (await AnswerAsync(client, quiz.Id, answered.Id, answered.CorrectOption.Id)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var loaded = await client.GetFromJsonAsync<QuizResponse>(QuizUri(quiz.Id), Ct);

        loaded.ShouldNotBeNull();
        loaded.Questions.Select(q => q.QuestionId).ShouldBe(quiz.Questions.Select(q => q.QuestionId));
        loaded.Questions[1].Feedback.ShouldNotBeNull();
        loaded.Questions[1].Feedback!.IsCorrect.ShouldBeTrue();
        loaded.Questions[1].Feedback!.Explanation.ShouldBe(answered.Explanation);
        loaded.Questions[0].Feedback.ShouldBeNull();
        loaded.Questions[2].Feedback.ShouldBeNull();
        (loaded.CorrectCount, loaded.IncorrectCount).ShouldBe((1, 0));
    }

    [Fact]
    public async Task GetQuiz_OfAnotherUser_Returns404()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var owner = factory.CreateClientFor(NewUid());
        using var intruder = factory.CreateClientFor(NewUid());
        var quiz = await CreateQuizAsync(owner, content.Exam.Id, content.Topic2.Id, 3);

        var response = await intruder.GetAsync(QuizUri(quiz.Id), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "quiz.not_found");
    }

    [Fact]
    public async Task GetQuiz_Unknown_Returns404()
    {
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.GetAsync(QuizUri(Guid.NewGuid()), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "quiz.not_found");
    }

    [Fact]
    public async Task SubmitAnswer_OnQuizOfAnotherUser_Returns404AndDoesNotAnswer()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var owner = factory.CreateClientFor(NewUid());
        using var intruder = factory.CreateClientFor(NewUid());
        var quiz = await CreateQuizAsync(owner, content.Exam.Id, content.Topic2.Id, 3);
        var question = content.Questions[quiz.Questions[0].QuestionId];

        var response = await AnswerAsync(intruder, quiz.Id, question.Id, question.CorrectOption.Id);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "quiz.not_found");
        var loaded = await owner.GetFromJsonAsync<QuizResponse>(QuizUri(quiz.Id), Ct);
        loaded.ShouldNotBeNull();
        loaded.Questions.ShouldAllBe(q => q.Feedback == null);
    }

    // ---- History ----

    [Fact]
    public async Task ListQuizzes_WithTwoQuizzes_ReturnsOwnQuizzesNewestFirstWithCounts()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        using var other = factory.CreateClientFor(NewUid());
        var older = await CreateQuizAsync(client, content.Exam.Id, content.Topic2.Id, 3);
        var newer = await CreateQuizAsync(client, content.Exam.Id, null, 5);
        await CreateQuizAsync(other, content.Exam.Id, null, 5);
        var answered = content.Questions[older.Questions[0].QuestionId];
        var wrongOption = answered.Options.First(o => !o.IsCorrect);
        (await AnswerAsync(client, older.Id, answered.Id, wrongOption.Id)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var history = await client.GetFromJsonAsync<List<QuizSummaryResponse>>(QuizzesUri, Ct);

        history.ShouldNotBeNull();
        history.Select(h => h.Id).ShouldBe([newer.Id, older.Id]);
        history[0].Mode.ShouldBe("Random");
        history[0].TopicId.ShouldBeNull();
        history[0].TopicTitle.ShouldBeNull();
        history[0].QuestionCount.ShouldBe(5);
        history[1].Mode.ShouldBe("ByTopic");
        history[1].TopicTitle.ShouldBe(content.Topic2.Title);
        (history[1].QuestionCount, history[1].CorrectCount, history[1].IncorrectCount).ShouldBe((3, 0, 1));
    }

    [Fact]
    public async Task ListQuizzes_WithSkipAndTake_PagesTheHistory()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        await CreateQuizAsync(client, content.Exam.Id, null, 2);
        var second = await CreateQuizAsync(client, content.Exam.Id, null, 2);
        await CreateQuizAsync(client, content.Exam.Id, null, 2);

        var page = await client.GetFromJsonAsync<List<QuizSummaryResponse>>(new Uri("/api/v1/quizzes?skip=1&take=1", UriKind.Relative), Ct);

        page.ShouldNotBeNull();
        page.Select(p => p.Id).ShouldBe([second.Id]);
    }

    [Theory]
    [InlineData("take=0")]
    [InlineData("take=51")]
    [InlineData("skip=-1")]
    public async Task ListQuizzes_WithOutOfRangePaging_Returns400(string query)
    {
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.GetAsync(new Uri($"/api/v1/quizzes?{query}", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    // ---- Helpers ----

    private static async Task<QuizResponse> CreateQuizAsync(HttpClient client, Guid examId, Guid? topicId, int questionCount)
    {
        var response = await client.PostAsJsonAsync(QuizzesUri, new CreateQuizRequest(examId, topicId, questionCount), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var quiz = await response.Content.ReadFromJsonAsync<QuizResponse>(Ct);
        return quiz ?? throw new InvalidOperationException("The quiz response was empty.");
    }

    private static Task<HttpResponseMessage> AnswerAsync(HttpClient client, Guid quizId, Guid questionId, Guid optionId) =>
        client.PostAsJsonAsync(AnswersUri(quizId), new SubmitAnswerRequest(questionId, optionId), Ct);
}