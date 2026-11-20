using System.Text.Json;

using OpoStudy.Contracts.Quizzes;
using OpoStudy.Mobile.Core.Api;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests.Offline;

public sealed class OfflineJsonContextTests
{
    [Fact]
    public void OfflineContentResponse_RoundTrip_KeepsEverythingIncludingTheCorrectOption()
    {
        var content = OfflineTestData.Content(topics: 1, questionsPerTopic: 2);

        var json = JsonSerializer.Serialize(content, ApiJsonContext.Default.OfflineContentResponse);
        var back = JsonSerializer.Deserialize(json, ApiJsonContext.Default.OfflineContentResponse);

        json.ShouldContain("\"isCorrect\":true");
        json.ShouldContain("\"examId\"");
        back.ShouldNotBeNull();
        back.Version.ShouldBe(content.Version);
        back.Exam.ShouldBe(content.Exam);
        back.Topics[0].Questions[1].LegalReference.ShouldBe(content.Topics[0].Questions[1].LegalReference);
        back.Topics[0].Questions[1].Options.Select(o => (o.Id, o.IsCorrect)).ShouldBe(content.Topics[0].Questions[1].Options.Select(o => (o.Id, o.IsCorrect)));
    }

    [Fact]
    public void SyncQuizRequest_RoundTrip_UsesCamelCaseAndKeepsTimes()
    {
        var at = new DateTimeOffset(2026, 3, 1, 7, 30, 0, TimeSpan.Zero);
        var request = new SyncQuizRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            "Random",
            at,
            [Guid.NewGuid(), Guid.NewGuid()],
            [new SyncAnswerRequest(Guid.NewGuid(), Guid.NewGuid(), at.AddMinutes(1))]);

        var json = JsonSerializer.Serialize(request, ApiJsonContext.Default.SyncQuizRequest);
        var back = JsonSerializer.Deserialize(json, ApiJsonContext.Default.SyncQuizRequest);

        json.ShouldContain("\"questionIds\"");
        json.ShouldContain("\"answeredAt\"");
        back.ShouldNotBeNull();
        back.Id.ShouldBe(request.Id);
        back.TopicId.ShouldBeNull();
        back.StartedAt.ShouldBe(at);
        back.QuestionIds.ShouldBe(request.QuestionIds);
        back.Answers.ShouldBe(request.Answers);
    }

    [Fact]
    public void SyncQuizResponse_Deserialize_ReadsTheServerPayload()
    {
        var quizId = Guid.NewGuid();
        var json = $$"""{"quizId":"{{quizId}}","acceptedCount":2,"duplicateCount":1,"rejectedCount":0,"xpEarned":15,"quizFinished":true}""";

        var response = JsonSerializer.Deserialize(json, ApiJsonContext.Default.SyncQuizResponse);

        response.ShouldBe(new SyncQuizResponse(quizId, 2, 1, 0, 15, true));
    }
}