using System.Text.Json;

using NSubstitute;

using OpoStudy.Contracts.Gradings;
using OpoStudy.Contracts.PracticalCases;
using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.Auth;
using OpoStudy.Mobile.Core.PracticalCases;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests.PracticalCases;

public sealed class PracticalCaseStoreAndTextsTests
{
    private readonly FakePreferencesStore _preferences = new();

    private readonly IAuthService _auth = Substitute.For<IAuthService>();

    private PreferencesPracticalCaseDraftStore CreateSut(string? uid = "uid-1")
    {
        _auth.UserId.Returns(uid);
        return new PreferencesPracticalCaseDraftStore(_preferences, _auth);
    }

    private static PracticalCaseDraft Draft() => new(["uno", ""], new DateTimeOffset(2026, 11, 20, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Find_AfterSave_ReturnsTheDraft()
    {
        var sut = CreateSut();

        sut.Save(PracticalCaseTestData.CaseId, Draft());

        var found = sut.Find(PracticalCaseTestData.CaseId);
        found.ShouldNotBeNull();
        found.Answers.ShouldBe(["uno", ""]);
        found.SavedAt.ShouldBe(Draft().SavedAt);
    }

    [Fact]
    public void Find_OtherUser_ReturnsNull()
    {
        CreateSut("uid-1").Save(PracticalCaseTestData.CaseId, Draft());

        CreateSut("uid-2").Find(PracticalCaseTestData.CaseId).ShouldBeNull();
        _preferences.Values.Keys.ShouldAllBe(k => k.Contains("uid-1", StringComparison.Ordinal));
    }

    [Fact]
    public void Save_WithoutSession_StoresNothing()
    {
        var sut = CreateSut(uid: null);

        sut.Save(PracticalCaseTestData.CaseId, Draft());

        _preferences.Values.ShouldBeEmpty();
        sut.Find(PracticalCaseTestData.CaseId).ShouldBeNull();
    }

    [Fact]
    public void Find_CorruptJson_ReturnsNullAndRemovesTheKey()
    {
        var sut = CreateSut();
        sut.Save(PracticalCaseTestData.CaseId, Draft());
        var key = _preferences.Values.Keys.Single();
        _preferences.Values[key] = "{not json";

        sut.Find(PracticalCaseTestData.CaseId).ShouldBeNull();

        _preferences.Values.ShouldBeEmpty();
    }

    [Fact]
    public void Delete_RemovesTheDraft()
    {
        var sut = CreateSut();
        sut.Save(PracticalCaseTestData.CaseId, Draft());

        sut.Delete(PracticalCaseTestData.CaseId);

        sut.Find(PracticalCaseTestData.CaseId).ShouldBeNull();
    }

    [Fact]
    public void ApiJsonContext_PracticalCaseResponse_RoundTrips()
    {
        var original = PracticalCaseTestData.Case();

        var json = JsonSerializer.Serialize(original, ApiJsonContext.Default.PracticalCaseResponse);
        var back = JsonSerializer.Deserialize(json, ApiJsonContext.Default.PracticalCaseResponse);

        back.ShouldNotBeNull();
        back.Parts.Count.ShouldBe(2);
        back.Parts[1].Statement.ShouldBe("Apartado 1");
        back.Title.ShouldBe(original.Title);
    }

    [Fact]
    public void ApiJsonContext_GradingResponse_RoundTripsWithAndWithoutCriteria()
    {
        var withCriteria = new GradingResponse(
            Guid.NewGuid(), Guid.NewGuid(), "T", "Completed", 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 25, 18.5m, "ok", null,
            [
                new GradingAnswerResponse(0, "A", "t", 10, 8.5m, "f", "modelo", [new CriterionScoreResponse("c", 5, 4.5m)]),
                new GradingAnswerResponse(1, "B", "t", 15, null, null, null, null),
            ]);

        var json = JsonSerializer.Serialize(withCriteria, ApiJsonContext.Default.GradingResponse);
        var back = JsonSerializer.Deserialize(json, ApiJsonContext.Default.GradingResponse);

        back.ShouldNotBeNull();
        back.Score.ShouldBe(18.5m);
        back.Answers[0].Criteria.ShouldNotBeNull();
        back.Answers[0].Criteria![0].Awarded.ShouldBe(4.5m);
        back.Answers[1].Criteria.ShouldBeNull();
    }

    [Fact]
    public void ApiJsonContext_GradingQuotaResponse_RoundTrips()
    {
        var quota = PracticalCaseTestData.Quota();

        var json = JsonSerializer.Serialize(quota, ApiJsonContext.Default.GradingQuotaResponse);

        JsonSerializer.Deserialize(json, ApiJsonContext.Default.GradingQuotaResponse).ShouldBe(quota);
    }

    [Fact]
    public void Quota_NormalAndExhausted_FormatsTexts()
    {
        PracticalCaseTexts.Quota(PracticalCaseTestData.Quota(3, 5)).ShouldBe("Te quedan 3 de 5 correcciones este mes");
        PracticalCaseTexts.Quota(PracticalCaseTestData.Quota(0, 5)).ShouldBe("Has agotado tus 5 correcciones de este mes");
        PracticalCaseTexts.QuotaShort(PracticalCaseTestData.Quota(3, 5)).ShouldBe("3/5 este mes");
    }

    [Fact]
    public void QuotaResets_UsesSpanishMonthName()
    {
        var quota = PracticalCaseTestData.Quota();

        PracticalCaseTexts.QuotaResets(quota).ShouldBe("Se renuevan el 1 de diciembre");
    }

    [Fact]
    public void Points_UsesDecimalComma()
    {
        PracticalCaseTexts.Points(18.5m).ShouldBe("18,5");
        PracticalCaseTexts.Points(25m).ShouldBe("25");
        PracticalCaseTexts.Score(18.5m, 25).ShouldBe("18,5 / 25 puntos");
        PracticalCaseTexts.ScoreAlert(18.5m, 25).ShouldBe("Has sacado 18,5 de 25 puntos.");
    }

    [Fact]
    public void CharacterCount_NearLimit_AddsRemaining()
    {
        PracticalCaseTexts.CharacterCount(10).ShouldBe("10 / 8000");
        PracticalCaseTexts.CharacterCountNearLimit(7500).ShouldBe("7500 / 8000 · Te quedan 500");
    }
}