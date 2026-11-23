using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using OpoStudy.Contracts.Gradings;
using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.Navigation;
using OpoStudy.Mobile.Core.PracticalCases;
using OpoStudy.Mobile.Core.Services;
using OpoStudy.Mobile.Core.ViewModels;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests.PracticalCases;

/// <summary>The result of a completed grading (spec 0021).</summary>
public sealed class GradingResultTests
{
    private readonly IOpoStudyApi _api = Substitute.For<IOpoStudyApi>();

    private readonly INavigationService _navigation = Substitute.For<INavigationService>();

    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 23, 9, 0, 0, TimeSpan.Zero));

    private static GradingResponse Completed(int? xp = 74, bool goal = true, IReadOnlyList<GradingAnswerResponse>? answers = null) =>
        new(
            PracticalCaseTestData.GradingId,
            PracticalCaseTestData.CaseId,
            "Supuesto 1",
            "Completed",
            1,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            25,
            18.5m,
            "Buen trabajo",
            null,
            answers ??
            [
                new GradingAnswerResponse(1, "Enunciado B", string.Empty, 10, 0m, "Falta todo", "Modelo B", [new CriterionScoreResponse("Uno", 10, 0m)]),
                new GradingAnswerResponse(
                    0,
                    "Enunciado A",
                    "Mi respuesta",
                    15,
                    8.5m,
                    "Bien",
                    "Modelo A",
                    [new CriterionScoreResponse("Completo", 5, 5m), new CriterionScoreResponse("Parcial", 5, 2.5m), new CriterionScoreResponse("Nada", 5, 0m)]),
            ],
            xp,
            goal);

    private GradingViewModel CreateSut(GradingResponse grading)
    {
        _api.GetGradingAsync(PracticalCaseTestData.GradingId, Arg.Any<CancellationToken>()).Returns(grading);
        var sut = new GradingViewModel(_api, _navigation, _dialogs, new FakeDraftStore(), _time, NullLogger<GradingViewModel>.Instance);
        sut.ApplyQuery(new Dictionary<string, object> { [QueryKeys.GradingId] = PracticalCaseTestData.GradingId });
        return sut;
    }

    [Fact]
    public async Task OnAppearingAsync_Completed_BuildsPartsInOrderWithTextsAndCollapsedBlocks()
    {
        var sut = CreateSut(Completed());

        await sut.OnAppearingAsync();

        sut.Parts.Select(p => p.Order).ShouldBe([0, 1]);
        sut.Parts[0].Title.ShouldBe("Apartado 1 · 8,5 / 15 puntos");
        sut.Parts[0].Feedback.ShouldBe("Bien");
        sut.Parts[0].Criteria.Select(c => c.PointsText).ShouldBe(["5 / 5", "2,5 / 5", "0 / 5"]);
        sut.Parts[0].Criteria.Select(c => c.MarkText).ShouldBe(["Completo", "Parcial", "Sin puntos"]);
        sut.Parts[0].Blocks.Select(b => b.Title).ShouldBe(["Enunciado", "Tu respuesta", "Respuesta modelo"]);
        sut.Parts[0].Blocks.ShouldAllBe(b => !b.IsExpanded);
        sut.Parts[1].Answer.Content.ShouldBe("(En blanco)");
        sut.FeedbackText.ShouldBe("Buen trabajo");
        sut.ScoreText.ShouldBe("18,5 / 25 puntos");
    }

    [Fact]
    public async Task ToggleCommand_Block_ExpandsAndCollapses()
    {
        var sut = CreateSut(Completed());
        await sut.OnAppearingAsync();
        var block = sut.Parts[0].Model!;

        block.ToggleCommand.Execute(null);
        block.IsExpanded.ShouldBeTrue();
        block.StateText.ShouldBe("Respuesta modelo, desplegado");
        block.Indicator.ShouldBe("▾");
        sut.Parts[0].Statement.IsExpanded.ShouldBeFalse();

        block.ToggleCommand.Execute(null);
        block.IsExpanded.ShouldBeFalse();
        block.StateText.ShouldBe("Respuesta modelo, plegado");
    }

    [Fact]
    public async Task OnAppearingAsync_CompletedWithXpAndGoal_ShowsThem()
    {
        var sut = CreateSut(Completed(74, true));

        await sut.OnAppearingAsync();

        sut.XpText.ShouldBe("+74 XP");
        sut.HasXp.ShouldBeTrue();
        sut.MetDailyGoal.ShouldBeTrue();
    }

    [Fact]
    public async Task OnAppearingAsync_CompletedWithoutXpOrGoal_HidesThem()
    {
        var sut = CreateSut(Completed(null, false));

        await sut.OnAppearingAsync();

        sut.XpText.ShouldBeNull();
        sut.HasXp.ShouldBeFalse();
        sut.MetDailyGoal.ShouldBeFalse();
    }

    [Fact]
    public async Task OnAppearingAsync_CompletedWithoutCriteriaOrModel_HidesThoseBlocks()
    {
        var sut = CreateSut(Completed(answers: [new GradingAnswerResponse(0, "E", "R", 10, 5m, null, null, null)]));

        await sut.OnAppearingAsync();

        sut.Parts[0].HasCriteria.ShouldBeFalse();
        sut.Parts[0].HasModel.ShouldBeFalse();
        sut.Parts[0].HasFeedback.ShouldBeFalse();
        sut.Parts[0].Blocks.Count.ShouldBe(2);
    }

    [Fact]
    public async Task RepeatCaseCommand_NavigatesToTheEditorWithTheCaseId()
    {
        var sut = CreateSut(Completed());
        await sut.OnAppearingAsync();

        await sut.RepeatCaseCommand.ExecuteAsync(null);

        await _navigation.Received(1).GoToAsync(
            Routes.PracticalCase,
            Arg.Is<IReadOnlyDictionary<string, object>>(p => (Guid)p[QueryKeys.PracticalCaseId] == PracticalCaseTestData.CaseId));
    }

    [Theory]
    [InlineData(74, "Has sacado 18,5 de 25 puntos. +74 XP.")]
    [InlineData(null, "Has sacado 18,5 de 25 puntos.")]
    [InlineData(0, "Has sacado 18,5 de 25 puntos.")]
    public void ScoreAlert_WithXp_AppendsItOnlyWhenThereIsSome(int? xp, string expected) =>
        PracticalCaseTexts.ScoreAlert(18.5m, 25, xp).ShouldBe(expected);

    [Fact]
    public async Task Polling_CompletedAfterInProgress_AlertIncludesTheXp()
    {
        var inProgress = PracticalCaseTestData.Grading("Processing", 1);
        _api.GetGradingAsync(PracticalCaseTestData.GradingId, Arg.Any<CancellationToken>()).Returns(inProgress, Completed());
        var sut = new GradingViewModel(_api, _navigation, _dialogs, new FakeDraftStore(), _time, NullLogger<GradingViewModel>.Instance);
        sut.ApplyQuery(new Dictionary<string, object> { [QueryKeys.GradingId] = PracticalCaseTestData.GradingId });

        await sut.OnAppearingAsync();
        _time.Advance(TimeSpan.FromSeconds(3));
        await PracticalCaseTestData.EventuallyAsync(() => sut.IsCompleted);
        await PracticalCaseTestData.EventuallyAsync(() => _dialogs.ReceivedCalls().Any());

        await _dialogs.Received(1).AlertAsync("Corrección lista", "Has sacado 18,5 de 25 puntos. +74 XP.");
        sut.OnDisappearing();
    }

    [Fact]
    public void Texts_NewFormats_UseSpanishNumbersAndDates()
    {
        PracticalCaseTexts.Ratio(18.5m, 25).ShouldBe("18,5 / 25");
        PracticalCaseTexts.Xp(74).ShouldBe("+74 XP");
        var when = new DateTimeOffset(2026, 11, 3, 18, 5, 0, TimeSpan.Zero);
        PracticalCaseTexts.HistoryDate(when).ShouldBe(when.ToLocalTime().ToString("d MMM yyyy, HH:mm", PracticalCaseTexts.Culture));
    }
}