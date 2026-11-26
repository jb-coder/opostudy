using OpoStudy.Domain.Common;
using OpoStudy.Domain.Questions;

using Shouldly;

namespace OpoStudy.Domain.Tests;

public sealed class QuestionBookmarkAndReportTests
{
    private static readonly Guid UserId = Guid.CreateVersion7();
    private static readonly Guid QuestionId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 10, 0, 0, TimeSpan.FromHours(1));

    [Fact]
    public void BookmarkCreate_ValidIds_StoresUtcTime()
    {
        var bookmark = QuestionBookmark.Create(UserId, QuestionId, Now);

        bookmark.UserId.ShouldBe(UserId);
        bookmark.QuestionId.ShouldBe(QuestionId);
        bookmark.CreatedAt.Offset.ShouldBe(TimeSpan.Zero);
        bookmark.CreatedAt.ShouldBe(Now);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void BookmarkCreate_EmptyId_ThrowsDomainRule(bool emptyUser, bool emptyQuestion)
    {
        var ex = Should.Throw<DomainRuleException>(() =>
            QuestionBookmark.Create(emptyUser ? Guid.Empty : UserId, emptyQuestion ? Guid.Empty : QuestionId, Now));

        ex.Code.ShouldBe("question_bookmark.invalid_id");
    }

    [Fact]
    public void ReportCreate_ValidWithComment_IsOpenAndTrimsComment()
    {
        var report = QuestionReport.Create(UserId, QuestionId, QuestionReportReason.WrongAnswer, "  Wrong article  ", Now);

        report.Status.ShouldBe(QuestionReportStatus.Open);
        report.Reason.ShouldBe(QuestionReportReason.WrongAnswer);
        report.Comment.ShouldBe("Wrong article");
        report.Id.ShouldNotBe(Guid.Empty);
        report.CreatedAt.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ReportCreate_BlankComment_StoresNull(string? comment)
    {
        var report = QuestionReport.Create(UserId, QuestionId, QuestionReportReason.Typo, comment, Now);

        report.Comment.ShouldBeNull();
    }

    [Fact]
    public void ReportCreate_CommentOfMaxLength_Succeeds()
    {
        var report = QuestionReport.Create(UserId, QuestionId, QuestionReportReason.Typo, new string('a', QuestionReport.MaxCommentLength), Now);

        report.Comment!.Length.ShouldBe(QuestionReport.MaxCommentLength);
    }

    [Fact]
    public void ReportCreate_CommentTooLong_ThrowsDomainRule()
    {
        var ex = Should.Throw<DomainRuleException>(() =>
            QuestionReport.Create(UserId, QuestionId, QuestionReportReason.Typo, new string('a', QuestionReport.MaxCommentLength + 1), Now));

        ex.Code.ShouldBe("question_report.comment_too_long");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void ReportCreate_OtherWithoutComment_ThrowsDomainRule(string? comment)
    {
        var ex = Should.Throw<DomainRuleException>(() =>
            QuestionReport.Create(UserId, QuestionId, QuestionReportReason.Other, comment, Now));

        ex.Code.ShouldBe("question_report.comment_required");
    }

    [Fact]
    public void ReportCreate_OtherWithComment_Succeeds()
    {
        var report = QuestionReport.Create(UserId, QuestionId, QuestionReportReason.Other, "Something", Now);

        report.Reason.ShouldBe(QuestionReportReason.Other);
    }

    [Fact]
    public void ReportCreate_EmptyQuestion_ThrowsDomainRule()
    {
        var ex = Should.Throw<DomainRuleException>(() =>
            QuestionReport.Create(UserId, Guid.Empty, QuestionReportReason.Typo, null, Now));

        ex.Code.ShouldBe("question_report.invalid_id");
    }
}
