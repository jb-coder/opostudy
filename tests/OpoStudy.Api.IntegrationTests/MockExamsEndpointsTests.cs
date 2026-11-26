using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

using OpoStudy.Contracts.MockExams;
using OpoStudy.Contracts.Quizzes;

using Shouldly;

namespace OpoStudy.Api.IntegrationTests;

public sealed class MockExamsEndpointsTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string NewUid() => $"uid-{Guid.NewGuid():N}";

    private static Uri MockExamsUri => new("/api/v1/mock-exams", UriKind.Relative);

    private static Uri ExercisesUri(Guid examId) => new($"/api/v1/exams/{examId}/exercises", UriKind.Relative);

    private static Uri SubmissionUri(Guid id) => new($"/api/v1/mock-exams/{id}/submission", UriKind.Relative);

    private static Uri ResultUri(Guid id) => new($"/api/v1/mock-exams/{id}/result", UriKind.Relative);

    private static async Task<MockExamResponse> CreateAsync(HttpClient client, Guid examId, int exercise = 1, decimal? passScore = null)
    {
        var response = await client.PostAsJsonAsync(MockExamsUri, new CreateMockExamRequest(examId, exercise, passScore), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var mockExam = await response.Content.ReadFromJsonAsync<MockExamResponse>(Ct);
        return mockExam.ShouldNotBeNull();
    }

    private static Guid CorrectOption(SeededContent content, Guid questionId) =>
        content.Questions[questionId].CorrectOption.Id;

    private static Guid WrongOption(SeededContent content, Guid questionId) =>
        content.Questions[questionId].Options.First(o => !o.IsCorrect).Id;

    private WebApplicationFactory<Program> WithTime(FakeTimeProvider time) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(time);
        }));

    [Fact]
    public async Task ListExercises_WithoutToken_Returns401()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(ExercisesUri(Guid.NewGuid()), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListExercises_ReturnsExercisesWithAvailableQuestions()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());

        var exercises = await client.GetFromJsonAsync<List<ExamExerciseResponse>>(ExercisesUri(content.Exam.Id), Ct);

        exercises.ShouldNotBeNull();
        exercises.Select(e => e.Number).ShouldBe([1, 2]);
        exercises[0].QuestionCount.ShouldBe(3);
        exercises[0].AvailableQuestionCount.ShouldBe(4);
        exercises[0].DefaultPassScore.ShouldBe(5m);
        exercises[0].PenaltyRatio.ShouldBe(0.5m);
    }

    [Fact]
    public async Task ListExercises_WithUnknownExam_Returns404()
    {
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.GetAsync(ExercisesUri(Guid.NewGuid()), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "exam.not_found");
    }

    [Fact]
    public async Task CreateMockExam_WithoutToken_Returns401()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(MockExamsUri, new CreateMockExamRequest(Guid.NewGuid(), 1, null), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateMockExam_ReturnsExactQuestionsOfTheSectionsWithoutAnswers()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.PostAsJsonAsync(MockExamsUri, new CreateMockExamRequest(content.Exam.Id, 1, null), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var json = await response.Content.ReadAsStringAsync(Ct);
        json.ShouldNotContain("isCorrect", Case.Insensitive);
        json.ShouldNotContain("explanation", Case.Insensitive);
        json.ShouldNotContain("legalReference", Case.Insensitive);
        var mockExam = await response.Content.ReadFromJsonAsync<MockExamResponse>(Ct);
        mockExam.ShouldNotBeNull();
        response.Headers.Location?.ToString().ShouldBe($"/api/v1/mock-exams/{mockExam.Id}/result");
        mockExam.Questions.Count.ShouldBe(3);
        var allowed = content.ActiveQuestionsOf(content.Topic1).Select(q => q.Id).ToHashSet();
        mockExam.Questions.ShouldAllBe(q => allowed.Contains(q.QuestionId));
        mockExam.Questions.Select(q => q.Order).ShouldBe([0, 1, 2]);
        mockExam.DurationMinutes.ShouldBe(10);
        mockExam.DeadlineAt.ShouldBe(mockExam.StartedAt.AddMinutes(10));
        mockExam.PassScore.ShouldBe(5m);
    }

    [Fact]
    public async Task CreateMockExam_WithCustomPassScore_UsesIt()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());

        var mockExam = await CreateAsync(client, content.Exam.Id, passScore: 7.5m);

        mockExam.PassScore.ShouldBe(7.5m);
    }

    [Fact]
    public async Task CreateMockExam_WithPassScoreAboveMax_Returns422()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.PostAsJsonAsync(MockExamsUri, new CreateMockExamRequest(content.Exam.Id, 1, 10.5m), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "mock_exam.invalid_pass_score");
    }

    [Fact]
    public async Task CreateMockExam_WithNonPositivePassScore_Returns400()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.PostAsJsonAsync(MockExamsUri, new CreateMockExamRequest(content.Exam.Id, 1, 0m), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateMockExam_WithNotEnoughQuestions_Returns422()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.PostAsJsonAsync(MockExamsUri, new CreateMockExamRequest(content.Exam.Id, 2, null), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "mock_exam.not_enough_questions");
    }

    [Fact]
    public async Task CreateMockExam_WithUnknownExercise_Returns404()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.PostAsJsonAsync(MockExamsUri, new CreateMockExamRequest(content.Exam.Id, 9, null), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "exercise.not_found");
    }

    [Fact]
    public async Task CreateMockExam_WithUnknownExam_Returns404()
    {
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.PostAsJsonAsync(MockExamsUri, new CreateMockExamRequest(Guid.NewGuid(), 1, null), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "exam.not_found");
    }

    [Fact]
    public async Task SubmitMockExam_WithCorrectWrongAndBlank_ReturnsScoreAndOutcomes()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var mockExam = await CreateAsync(client, content.Exam.Id);
        var q = mockExam.Questions;
        var request = new SubmitMockExamRequest(
        [
            new MockExamAnswer(q[0].QuestionId, CorrectOption(content, q[0].QuestionId)),
            new MockExamAnswer(q[1].QuestionId, WrongOption(content, q[1].QuestionId)),
        ]);

        var response = await client.PostAsJsonAsync(SubmissionUri(mockExam.Id), request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubmitMockExamResponse>(Ct);
        body.ShouldNotBeNull();
        body.XpGained.ShouldBeGreaterThan(0);
        var result = body.Result;
        result.CorrectCount.ShouldBe(1);
        result.IncorrectCount.ShouldBe(1);
        result.BlankCount.ShouldBe(1);
        result.Score.ShouldBe(1.67m); // (1 - 0.5) * 10 / 3
        result.Passed.ShouldBeFalse();
        result.SubmittedLate.ShouldBeFalse();
        result.Items.Select(i => i.Outcome).ShouldBe(["Correct", "Incorrect", "Blank"]);
        result.Items[2].SelectedOptionId.ShouldBeNull();
        result.Items[0].CorrectOptionId.ShouldBe(CorrectOption(content, q[0].QuestionId));
        result.Items[1].Explanation.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SubmitMockExam_Twice_Returns409()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var mockExam = await CreateAsync(client, content.Exam.Id);
        var request = new SubmitMockExamRequest([]);
        (await client.PostAsJsonAsync(SubmissionUri(mockExam.Id), request, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var second = await client.PostAsJsonAsync(SubmissionUri(mockExam.Id), request, Ct);

        await second.ShouldBeProblemAsync(HttpStatusCode.Conflict, "quiz.finished");
    }

    [Fact]
    public async Task GetResult_BeforeAndAfterSubmission_Returns409Then200()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var mockExam = await CreateAsync(client, content.Exam.Id);

        var before = await client.GetAsync(ResultUri(mockExam.Id), Ct);
        await before.ShouldBeProblemAsync(HttpStatusCode.Conflict, "mock_exam.not_submitted");

        await client.PostAsJsonAsync(SubmissionUri(mockExam.Id), new SubmitMockExamRequest([]), Ct);
        var after = await client.GetFromJsonAsync<MockExamResultResponse>(ResultUri(mockExam.Id), Ct);

        after.ShouldNotBeNull();
        after.BlankCount.ShouldBe(3);
        after.Score.ShouldBe(0m);
    }

    [Fact]
    public async Task SubmitAndGetResult_FromAnotherUser_Return404()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var owner = factory.CreateClientFor(NewUid());
        using var other = factory.CreateClientFor(NewUid());
        var mockExam = await CreateAsync(owner, content.Exam.Id);

        var submit = await other.PostAsJsonAsync(SubmissionUri(mockExam.Id), new SubmitMockExamRequest([]), Ct);
        var result = await other.GetAsync(ResultUri(mockExam.Id), Ct);

        await submit.ShouldBeProblemAsync(HttpStatusCode.NotFound, "mock_exam.not_found");
        await result.ShouldBeProblemAsync(HttpStatusCode.NotFound, "mock_exam.not_found");
    }

    [Fact]
    public async Task SubmitMockExam_OnRegularQuiz_Returns404()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var quizResponse = await client.PostAsJsonAsync(
            new Uri("/api/v1/quizzes", UriKind.Relative), new CreateQuizRequest(content.Exam.Id, content.Topic1.Id, 2), Ct);
        var quiz = await quizResponse.Content.ReadFromJsonAsync<QuizResponse>(Ct);

        var response = await client.PostAsJsonAsync(SubmissionUri(quiz!.Id), new SubmitMockExamRequest([]), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "mock_exam.not_found");
    }

    [Fact]
    public async Task SubmitAnswer_OnMockExam_Returns422()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var mockExam = await CreateAsync(client, content.Exam.Id);
        var question = mockExam.Questions[0];

        var response = await client.PostAsJsonAsync(
            new Uri($"/api/v1/quizzes/{mockExam.Id}/answers", UriKind.Relative),
            new SubmitAnswerRequest(question.QuestionId, question.Options[0].Id),
            Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "mock_exam.answer_requires_submission");
    }

    [Fact]
    public async Task SubmitMockExam_WithForeignOption_Returns422()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var mockExam = await CreateAsync(client, content.Exam.Id);
        var q = mockExam.Questions;
        var request = new SubmitMockExamRequest([new MockExamAnswer(q[0].QuestionId, q[1].Options[0].Id)]);

        var response = await client.PostAsJsonAsync(SubmissionUri(mockExam.Id), request, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "quiz.foreign_option");
    }

    [Fact]
    public async Task SubmitMockExam_WithForeignQuestion_Returns422()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var mockExam = await CreateAsync(client, content.Exam.Id);
        var foreign = content.ActiveQuestionsOf(content.Topic2).First();
        var request = new SubmitMockExamRequest([new MockExamAnswer(foreign.Id, foreign.Options[0].Id)]);

        var response = await client.PostAsJsonAsync(SubmissionUri(mockExam.Id), request, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "quiz.foreign_question");
    }

    [Fact]
    public async Task SubmitMockExam_WithRepeatedQuestion_Returns400()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var mockExam = await CreateAsync(client, content.Exam.Id);
        var q = mockExam.Questions[0];
        var request = new SubmitMockExamRequest([new MockExamAnswer(q.QuestionId, q.Options[0].Id), new MockExamAnswer(q.QuestionId, q.Options[1].Id)]);

        var response = await client.PostAsJsonAsync(SubmissionUri(mockExam.Id), request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SubmitMockExam_Mistake_AppearsInReview()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = factory.CreateClientFor(NewUid());
        var mockExam = await CreateAsync(client, content.Exam.Id);
        var q = mockExam.Questions;
        var request = new SubmitMockExamRequest(
        [
            new MockExamAnswer(q[0].QuestionId, WrongOption(content, q[0].QuestionId)),
            new MockExamAnswer(q[1].QuestionId, CorrectOption(content, q[1].QuestionId)),
        ]);
        await client.PostAsJsonAsync(SubmissionUri(mockExam.Id), request, Ct);

        var review = await client.PostAsJsonAsync(
            new Uri("/api/v1/quizzes", UriKind.Relative),
            new CreateQuizRequest(content.Exam.Id, null, 10, MistakesOnly: true),
            Ct);

        review.StatusCode.ShouldBe(HttpStatusCode.Created);
        var quiz = await review.Content.ReadFromJsonAsync<QuizResponse>(Ct);
        quiz.ShouldNotBeNull();
        quiz.Mode.ShouldBe("Review");
        quiz.Questions.ShouldHaveSingleItem().QuestionId.ShouldBe(q[0].QuestionId);
    }

    [Fact]
    public async Task SubmitMockExam_AfterDeadlineAndGrace_IsMarkedLate()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero));
        using var host = WithTime(time);
        var content = await ContentSeeder.SeedAsync(factory.Services);
        using var client = ApiFactory.CreateClientFor(host, NewUid());
        var mockExam = await CreateAsync(client, content.Exam.Id);

        time.Advance(TimeSpan.FromMinutes(13));
        var response = await client.PostAsJsonAsync(SubmissionUri(mockExam.Id), new SubmitMockExamRequest([]), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubmitMockExamResponse>(Ct);
        body.ShouldNotBeNull();
        body.Result.SubmittedLate.ShouldBeTrue();
    }
}