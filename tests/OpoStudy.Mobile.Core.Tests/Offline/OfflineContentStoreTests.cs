using System.Net;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using OpoStudy.Contracts.Offline;
using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.Offline;

using Refit;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests.Offline;

public sealed class OfflineContentStoreTests : IDisposable
{
    private static readonly Guid ExamId = OfflineTestData.ExamId;

    private readonly TempDatabase _temp = new();
    private readonly IOpoStudyApi _api = Substitute.For<IOpoStudyApi>();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero));

    public void Dispose() => _temp.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private OfflineContentStore CreateSut() => new(_api, _temp.Database, _time, NullLogger<OfflineContentStore>.Instance);

    private static ApiResponse<OfflineContentResponse> Ok(OfflineContentResponse content) =>
        OfflineTestData.ContentResponse(HttpStatusCode.OK, content);

    private static ApiResponse<OfflineContentResponse> NotModified() =>
        OfflineTestData.ContentResponse(HttpStatusCode.NotModified, null);

    [Fact]
    public async Task RefreshAsync_FirstDownload_SavesTheContentWithoutIfNoneMatch()
    {
        var content = OfflineTestData.Content(version: "abc");
        _api.GetOfflineContentAsync(ExamId, null, Arg.Any<CancellationToken>()).Returns(Ok(content));
        var sut = CreateSut();

        await sut.RefreshAsync(ExamId, Ct);

        // A new instance proves it was persisted, not just cached in memory.
        var stored = await CreateSut().GetAsync(ExamId, Ct);
        stored.ShouldNotBeNull();
        stored.Version.ShouldBe("abc");
        stored.Topics.Count.ShouldBe(2);
        stored.Topics[0].Questions[0].Options.Single(o => o.IsCorrect).Order.ShouldBe(2);
    }

    [Fact]
    public async Task RefreshAsync_StoredVersion_SendsItAsIfNoneMatch()
    {
        _api.GetOfflineContentAsync(ExamId, Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Ok(OfflineTestData.Content(version: "abc")), NotModified());
        var sut = CreateSut();
        await sut.RefreshAsync(ExamId, Ct);

        await sut.RefreshAsync(ExamId, Ct);

        await _api.Received(1).GetOfflineContentAsync(ExamId, "\"abc\"", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_NotModified_KeepsTheStoredContent()
    {
        _api.GetOfflineContentAsync(ExamId, Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Ok(OfflineTestData.Content(version: "abc")), NotModified());
        var sut = CreateSut();
        await sut.RefreshAsync(ExamId, Ct);

        await sut.RefreshAsync(ExamId, Ct);

        (await CreateSut().GetAsync(ExamId, Ct))!.Version.ShouldBe("abc");
    }

    [Fact]
    public async Task RefreshAsync_NewVersion_ReplacesTheStoredContent()
    {
        _api.GetOfflineContentAsync(ExamId, Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Ok(OfflineTestData.Content(version: "v1")), Ok(OfflineTestData.Content(topics: 1, version: "v2")));
        var sut = CreateSut();
        await sut.RefreshAsync(ExamId, Ct);

        await sut.RefreshAsync(ExamId, Ct);

        var stored = await sut.GetAsync(ExamId, Ct);
        stored!.Version.ShouldBe("v2");
        stored.Topics.Count.ShouldBe(1);
        (await CreateSut().GetAsync(ExamId, Ct))!.Version.ShouldBe("v2");
    }

    [Fact]
    public async Task RefreshAsync_NoConnection_DoesNotThrowAndKeepsTheStoredContent()
    {
        _api.GetOfflineContentAsync(ExamId, Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(
                _ => Task.FromResult<IApiResponse<OfflineContentResponse>>(Ok(OfflineTestData.Content(version: "v1"))),
                _ => throw new HttpRequestException("offline"));
        var sut = CreateSut();
        await sut.RefreshAsync(ExamId, Ct);

        await Should.NotThrowAsync(() => sut.RefreshAsync(ExamId, Ct));

        (await sut.GetAsync(ExamId, Ct))!.Version.ShouldBe("v1");
    }

    [Fact]
    public async Task RefreshAsync_ServerError_DoesNotThrowAndStoresNothing()
    {
        var failed = OfflineTestData.ContentResponse(HttpStatusCode.InternalServerError, null);
        _api.GetOfflineContentAsync(ExamId, null, Arg.Any<CancellationToken>()).Returns(failed);
        var sut = CreateSut();

        await sut.RefreshAsync(ExamId, Ct);

        (await sut.GetAsync(ExamId, Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task RefreshAsync_CancelledByCaller_Throws()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        _api.GetOfflineContentAsync(ExamId, null, Arg.Any<CancellationToken>()).Throws(new OperationCanceledException(cts.Token));
        var sut = CreateSut();

        await Should.ThrowAsync<OperationCanceledException>(() => sut.RefreshAsync(ExamId, cts.Token));
    }

    [Fact]
    public async Task GetAsync_NeverDownloaded_ReturnsNull() =>
        (await CreateSut().GetAsync(ExamId, Ct)).ShouldBeNull();

    [Fact]
    public async Task GetAsync_SecondCall_ReturnsTheSameInstanceFromMemory()
    {
        _api.GetOfflineContentAsync(ExamId, null, Arg.Any<CancellationToken>()).Returns(Ok(OfflineTestData.Content()));
        await CreateSut().RefreshAsync(ExamId, Ct);
        var sut = CreateSut();

        var first = await sut.GetAsync(ExamId, Ct);
        var second = await sut.GetAsync(ExamId, Ct);

        first.ShouldNotBeNull();
        second.ShouldBeSameAs(first);
    }

    [Fact]
    public async Task ListExamsAsync_DownloadedExams_ReturnsThemOrderedByName()
    {
        var otherId = Guid.Parse("11111111-0000-0000-0000-000000000002");
        var other = OfflineTestData.Content(examId: otherId) with
        {
            Exam = OfflineTestData.Content(examId: otherId).Exam with { Name = "Administrativo" },
        };
        _api.GetOfflineContentAsync(ExamId, null, Arg.Any<CancellationToken>()).Returns(Ok(OfflineTestData.Content()));
        _api.GetOfflineContentAsync(otherId, null, Arg.Any<CancellationToken>()).Returns(Ok(other));
        var sut = CreateSut();
        await sut.RefreshAsync(ExamId, Ct);
        await sut.RefreshAsync(otherId, Ct);

        var exams = await CreateSut().ListExamsAsync(Ct);

        exams.Select(e => e.Name).ShouldBe(["Administrativo", "Programador C1"]);
    }

    [Fact]
    public async Task ListExamsAsync_NothingDownloaded_ReturnsEmpty() =>
        (await CreateSut().ListExamsAsync(Ct)).ShouldBeEmpty();
}