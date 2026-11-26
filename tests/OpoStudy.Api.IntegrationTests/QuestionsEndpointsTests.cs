using System.Net;
using System.Net.Http.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using OpoStudy.Contracts.Questions;
using OpoStudy.Contracts.Quizzes;
using OpoStudy.Contracts.Users;
using OpoStudy.Domain.Questions;
using OpoStudy.Infrastructure.Persistence;

using Shouldly;

namespace OpoStudy.Api.IntegrationTests;

public sealed class QuestionsEndpointsTests(ApiFactory factory)
{
    private static readonly Uri QuizzesUri = new("/api/v1/quizzes", UriKind.Relative);
    private static readonly Uri MeUri = new("/api/v1/me", UriKind.Relative);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Uri BookmarkUri(Guid questionId) => new($"/api/v1/questions/{questionId}/bookmark", UriKind.Relative);

    private static Uri ReportsUri(Guid questionId) => new($"/api/v1/questions/{questionId}/reports", UriKind.Relative);

    // ---- Bookmark / unbookmark ----

    [Fact]
    public async Task Bookmark_Twice_ReturnsNoContentAndKeepsOneRow()
    {
        var (content, client, userId) = await StartAsync();
        var question = content.ActiveQuestionsOf(content.Topic2).First();

        (await client.PutAsync(BookmarkUri(question.Id), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.PutAsync(BookmarkUri(question.Id), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await CountBookmarksAsync(userId, question.Id)).ShouldBe(1);
    }

    [Fact]
    public async Task Bookmark_ConcurrentRequests_AllReturnNoContent()
    {
        var (content, client, userId) = await StartAsync();
        var question = content.ActiveQuestionsOf(content.Topic2).First();

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => client.PutAsync(BookmarkUri(question.Id), null, Ct)));

        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.NoContent);
        (await CountBookmarksAsync(userId, question.Id)).ShouldBe(1);
    }

    [Fact]
    public async Task Unbookmark_WithoutBookmark_ReturnsNoContent()
    {
        var (content, client, _) = await StartAsync();

        var response = await client.DeleteAsync(BookmarkUri(content.ActiveQuestionsOf(content.Topic2).First().Id), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Unbookmark_UnknownQuestion_ReturnsNoContent()
    {
        var (_, client, _) = await StartAsync();

        var response = await client.DeleteAsync(BookmarkUri(Guid.NewGuid()), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Unbookmark_Bookmarked_RemovesIt()
    {
        var (content, client, userId) = await StartAsync();
        var question = content.ActiveQuestionsOf(content.Topic2).First();
        await client.PutAsync(BookmarkUri(question.Id), null, Ct);

        var response = await client.DeleteAsync(BookmarkUri(question.Id), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await CountBookmarksAsync(userId, question.Id)).ShouldBe(0);
    }

    [Fact]
    public async Task Bookmark_UnknownQuestion_Returns404()
    {
        var (_, client, _) = await StartAsync();

        var response = await client.PutAsync(BookmarkUri(Guid.NewGuid()), null, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "question.not_found");
    }

    [Fact]
    public async Task Bookmark_InactiveQuestion_Returns404()
    {
        var (content, client, _) = await StartAsync();

        var response = await client.PutAsync(BookmarkUri(content.InactiveQuestionOf(content.Topic1).Id), null, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "question.not_found");
    }

    [Fact]
    public async Task Bookmark_QuestionOfInactiveExam_Returns404()
    {
        var (content, client, _) = await StartAsync();
        var question = content.Questions.Values.First(q => q.TopicId == content.InactiveExamTopic.Id);

        var response = await client.PutAsync(BookmarkUri(question.Id), null, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "question.not_found");
    }

    [Fact]
    public async Task Bookmark_WithoutToken_Returns401()
    {
        using var client = factory.CreateClient();

        var response = await client.PutAsync(BookmarkUri(Guid.NewGuid()), null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- IsBookmarked in quizzes ----

    [Fact]
    public async Task CreateQuiz_WithBookmarks_MarksOnlyOwnBookmarks()
    {
        var (content, client, _) = await StartAsync();
        using var other = factory.CreateClientFor(NewUid());
        var questions = content.ActiveQuestionsOf(content.Topic2).ToList();
        await client.PutAsync(BookmarkUri(questions[0].Id), null, Ct);
        await other.PutAsync(BookmarkUri(questions[1].Id), null, Ct);

        var created = await CreateQuizAsync(client, new CreateQuizRequest(content.Exam.Id, content.Topic2.Id, 10));
        var fetched = (await client.GetFromJsonAsync<QuizResponse>(new Uri($"/api/v1/quizzes/{created.Id}", UriKind.Relative), Ct))!;

        foreach (var quiz in new[] { created, fetched })
        {
            quiz.Questions.Single(q => q.QuestionId == questions[0].Id).IsBookmarked.ShouldBeTrue();
            quiz.Questions.Single(q => q.QuestionId == questions[1].Id).IsBookmarked.ShouldBeFalse();
            quiz.Questions.Single(q => q.QuestionId == questions[2].Id).IsBookmarked.ShouldBeFalse();
        }
    }

    // ---- BookmarkedOnly ----

    [Fact]
    public async Task CreateQuiz_BookmarkedOnlyByExam_UsesOnlyBookmarkedActiveQuestions()
    {
        var (content, client, _) = await StartAsync();
        var topic1 = content.ActiveQuestionsOf(content.Topic1).First();
        var topic2 = content.ActiveQuestionsOf(content.Topic2).First();
        await client.PutAsync(BookmarkUri(topic1.Id), null, Ct);
        await client.PutAsync(BookmarkUri(topic2.Id), null, Ct);
        await DeactivateAsync(topic2.Id);

        var quiz = await CreateQuizAsync(client, new CreateQuizRequest(content.Exam.Id, null, 10, BookmarkedOnly: true));

        quiz.Mode.ShouldBe("Bookmarks");
        quiz.TopicId.ShouldBeNull();
        quiz.Questions.Select(q => q.QuestionId).ShouldBe([topic1.Id]);
        quiz.Questions[0].IsBookmarked.ShouldBeTrue();
        quiz.Questions[0].Feedback.ShouldBeNull();
        var list = (await client.GetFromJsonAsync<List<QuizSummaryResponse>>(new Uri("/api/v1/quizzes?skip=0&take=1", UriKind.Relative), Ct))!;
        list[0].Mode.ShouldBe("Bookmarks");
    }

    [Fact]
    public async Task CreateQuiz_BookmarkedOnlyByTopic_UsesOnlyThatTopicAndRespectsCount()
    {
        var (content, client, _) = await StartAsync();
        var topic1 = content.ActiveQuestionsOf(content.Topic1).First();
        var topic2 = content.ActiveQuestionsOf(content.Topic2).ToList();
        await client.PutAsync(BookmarkUri(topic1.Id), null, Ct);
        foreach (var question in topic2)
        {
            await client.PutAsync(BookmarkUri(question.Id), null, Ct);
        }

        var quiz = await CreateQuizAsync(client, new CreateQuizRequest(content.Exam.Id, content.Topic2.Id, 2, BookmarkedOnly: true));

        quiz.TopicId.ShouldBe(content.Topic2.Id);
        quiz.Questions.Count.ShouldBe(2);
        quiz.Questions.ShouldAllBe(q => topic2.Any(t => t.Id == q.QuestionId));
    }

    [Fact]
    public async Task CreateQuiz_BookmarkedOnlyWithoutBookmarks_Returns422()
    {
        var (content, client, _) = await StartAsync();
        using var other = factory.CreateClientFor(NewUid());
        await other.PutAsync(BookmarkUri(content.ActiveQuestionsOf(content.Topic2).First().Id), null, Ct);

        var response = await client.PostAsJsonAsync(QuizzesUri, new CreateQuizRequest(content.Exam.Id, null, 10, BookmarkedOnly: true), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "quiz.no_bookmarks");
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task CreateQuiz_BookmarkedOnlyWithOtherReview_Returns400(bool mistakesOnly, bool dueOnly)
    {
        using var client = factory.CreateClientFor(NewUid());

        var response = await client.PostAsJsonAsync(
            QuizzesUri,
            new CreateQuizRequest(Guid.NewGuid(), null, 10, mistakesOnly, dueOnly, BookmarkedOnly: true),
            Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("bookmarkedOnly");
    }

    // ---- Reports ----

    [Fact]
    public async Task Report_Valid_StoresOpenReport()
    {
        var (content, client, userId) = await StartAsync();
        var question = content.ActiveQuestionsOf(content.Topic2).First();

        var response = await client.PostAsJsonAsync(ReportsUri(question.Id), new CreateQuestionReportRequest("wronganswer", "  Article 5 says otherwise  "), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var report = (await FindReportsAsync(userId, question.Id)).ShouldHaveSingleItem();
        report.Status.ShouldBe(QuestionReportStatus.Open);
        report.Reason.ShouldBe(QuestionReportReason.WrongAnswer);
        report.Comment.ShouldBe("Article 5 says otherwise");
    }

    [Fact]
    public async Task Report_WithoutComment_StoresNullComment()
    {
        var (content, client, userId) = await StartAsync();
        var question = content.ActiveQuestionsOf(content.Topic2).First();

        var response = await client.PostAsJsonAsync(ReportsUri(question.Id), new CreateQuestionReportRequest("Typo", null), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await FindReportsAsync(userId, question.Id)).Single().Comment.ShouldBeNull();
    }

    [Theory]
    [InlineData("Unknown", null, "reason")]
    [InlineData("", null, "reason")]
    [InlineData("1", null, "reason")]
    [InlineData("Typo", "TOO_LONG", "comment")]
    [InlineData("Other", null, "comment")]
    [InlineData("Other", "   ", "comment")]
    public async Task Report_Invalid_Returns400(string reason, string? comment, string field)
    {
        var (content, client, userId) = await StartAsync();
        var question = content.ActiveQuestionsOf(content.Topic2).First();
        comment = comment == "TOO_LONG" ? new string('a', QuestionReport.MaxCommentLength + 1) : comment;

        var response = await client.PostAsJsonAsync(ReportsUri(question.Id), new CreateQuestionReportRequest(reason, comment), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain(field);
        (await FindReportsAsync(userId, question.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Report_UnknownQuestion_Returns404()
    {
        var (_, client, _) = await StartAsync();

        var response = await client.PostAsJsonAsync(ReportsUri(Guid.NewGuid()), new CreateQuestionReportRequest("Typo", null), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "question.not_found");
    }

    [Fact]
    public async Task Report_InactiveQuestion_Returns404()
    {
        var (content, client, _) = await StartAsync();

        var response = await client.PostAsJsonAsync(
            ReportsUri(content.InactiveQuestionOf(content.Topic1).Id),
            new CreateQuestionReportRequest("Typo", null),
            Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "question.not_found");
    }

    [Fact]
    public async Task Report_SecondOpenReport_Returns409()
    {
        var (content, client, userId) = await StartAsync();
        var question = content.ActiveQuestionsOf(content.Topic2).First();
        (await client.PostAsJsonAsync(ReportsUri(question.Id), new CreateQuestionReportRequest("Typo", null), Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var response = await client.PostAsJsonAsync(ReportsUri(question.Id), new CreateQuestionReportRequest("WrongAnswer", null), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "question.already_reported");
        (await FindReportsAsync(userId, question.Id)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Report_ConcurrentRequests_OneSucceedsAndOthersConflict()
    {
        var (content, client, userId) = await StartAsync();
        var question = content.ActiveQuestionsOf(content.Topic2).First();

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            client.PostAsJsonAsync(ReportsUri(question.Id), new CreateQuestionReportRequest("Typo", null), Ct)));

        responses.Count(r => r.StatusCode == HttpStatusCode.NoContent).ShouldBe(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(3);
        (await FindReportsAsync(userId, question.Id)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Report_AfterResolved_CanReportAgain()
    {
        var (content, client, userId) = await StartAsync();
        var question = content.ActiveQuestionsOf(content.Topic2).First();
        await client.PostAsJsonAsync(ReportsUri(question.Id), new CreateQuestionReportRequest("Typo", null), Ct);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OpoStudyDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE question_reports SET status = 'Resolved' WHERE user_id = {userId}", Ct);
        }

        var response = await client.PostAsJsonAsync(ReportsUri(question.Id), new CreateQuestionReportRequest("Typo", null), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await FindReportsAsync(userId, question.Id)).Count.ShouldBe(2);
    }

    [Fact]
    public async Task Report_SameQuestionByAnotherUser_Succeeds()
    {
        var (content, client, _) = await StartAsync();
        using var other = factory.CreateClientFor(NewUid());
        var question = content.ActiveQuestionsOf(content.Topic2).First();
        await client.PostAsJsonAsync(ReportsUri(question.Id), new CreateQuestionReportRequest("Typo", null), Ct);

        var response = await other.PostAsJsonAsync(ReportsUri(question.Id), new CreateQuestionReportRequest("Typo", null), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    // ---- Account deletion ----

    [Fact]
    public async Task DeleteMe_WithBookmarksAndReports_DeletesThem()
    {
        var (content, client, userId) = await StartAsync();
        var question = content.ActiveQuestionsOf(content.Topic2).First();
        await client.PutAsync(BookmarkUri(question.Id), null, Ct);
        await client.PostAsJsonAsync(ReportsUri(question.Id), new CreateQuestionReportRequest("Typo", null), Ct);

        var response = await client.DeleteAsync(MeUri, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await CountBookmarksAsync(userId, question.Id)).ShouldBe(0);
        (await FindReportsAsync(userId, question.Id)).ShouldBeEmpty();
    }

    // ---- Helpers ----

    private static string NewUid() => $"uid-{Guid.NewGuid():N}";

    private async Task<(SeededContent Content, HttpClient Client, Guid UserId)> StartAsync()
    {
        var content = await ContentSeeder.SeedAsync(factory.Services);
        var client = factory.CreateClientFor(NewUid());
        var me = await client.GetFromJsonAsync<UserProfileResponse>(MeUri, Ct);
        return (content, client, me!.Id);
    }

    private static async Task<QuizResponse> CreateQuizAsync(HttpClient client, CreateQuizRequest request)
    {
        var response = await client.PostAsJsonAsync(QuizzesUri, request, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<QuizResponse>(Ct))!;
    }

    private async Task<int> CountBookmarksAsync(Guid userId, Guid questionId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpoStudyDbContext>();
        return await db.QuestionBookmarks.CountAsync(b => b.UserId == userId && b.QuestionId == questionId, Ct);
    }

    private async Task<List<QuestionReport>> FindReportsAsync(Guid userId, Guid questionId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpoStudyDbContext>();
        return await db.QuestionReports.AsNoTracking().Where(r => r.UserId == userId && r.QuestionId == questionId).ToListAsync(Ct);
    }

    private async Task DeactivateAsync(Guid questionId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpoStudyDbContext>();
        var question = await db.Questions.SingleAsync(q => q.Id == questionId, Ct);
        question.Deactivate();
        await db.SaveChangesAsync(Ct);
    }
}