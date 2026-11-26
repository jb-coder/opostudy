using System.Globalization;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using OpoStudy.Contracts.MockExams;
using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.MockExams;
using OpoStudy.Mobile.Core.Navigation;
using OpoStudy.Mobile.Core.Services;
using OpoStudy.Mobile.Core.ViewModels;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests;

public sealed class MockExamsViewModelTests
{
    private readonly IOpoStudyApi _api = Substitute.For<IOpoStudyApi>();

    private readonly INavigationService _navigation = Substitute.For<INavigationService>();

    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();

    private readonly FakePreferencesStore _preferences = new();

    private readonly OfflineDoubles _offline = new();

    private readonly Guid _examId = Guid.NewGuid();

    private static ExamExerciseResponse Exercise(int number = 1, int questionCount = 55, int available = 100, decimal penalty = 0.5m) =>
        new(number, "Primer ejercicio", questionCount, 50, 20m, penalty, 10m, available);

    private MockExamsViewModel CreateSut(params ExamExerciseResponse[] exercises)
    {
        _api.GetExercisesAsync(_examId, Arg.Any<CancellationToken>()).Returns(exercises);
        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        var sut = new MockExamsViewModel(_api, _navigation, _dialogs, _preferences, _offline.State, NullLogger<MockExamsViewModel>.Instance);
        sut.ApplyQuery(new Dictionary<string, object> { [QueryKeys.ExamId] = _examId, [QueryKeys.ExamName] = "Programador" });
        return sut;
    }

    [Theory]
    [InlineData(0.5, "Cada fallo resta la mitad de un acierto")]
    [InlineData(0, "Los fallos no restan")]
    [InlineData(0.25, "Cada fallo resta 0,25 de un acierto")]
    public void Penalty_Ratio_ReturnsText(double ratio, string expected)
    {
        MockExamTexts.Penalty((decimal)ratio).ShouldBe(expected);
    }

    [Fact]
    public async Task OnAppearingAsync_LoadsExercisesWithDefaultPassScoreAndTexts()
    {
        var sut = CreateSut(Exercise());

        await sut.OnAppearingAsync();

        var item = sut.Exercises.ShouldHaveSingleItem();
        item.PassScoreText.ShouldBe("10");
        item.Details.ShouldBe("55 preguntas · 50 minutos · sobre 20 puntos");
        item.PenaltyText.ShouldBe("Cada fallo resta la mitad de un acierto");
        item.IsAvailable.ShouldBeTrue();
        sut.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public async Task OnAppearingAsync_WithRememberedPassScore_UsesIt()
    {
        _preferences.Values[MockExamsViewModel.PassScoreStorageKey(_examId, 1)] = "12.5";
        var sut = CreateSut(Exercise());

        await sut.OnAppearingAsync();

        sut.Exercises.Single().PassScoreText.ShouldBe("12,5");
    }

    [Fact]
    public async Task OnAppearingAsync_WithoutExercises_SetsEmptyState()
    {
        var sut = CreateSut();

        await sut.OnAppearingAsync();

        sut.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public async Task OnAppearingAsync_WhenOffline_ShowsNeedsConnectionWithoutCallingApi()
    {
        _offline.Network.HasInternet.Returns(false);
        var sut = CreateSut(Exercise());

        await sut.OnAppearingAsync();

        sut.NeedsConnection.ShouldBeTrue();
        sut.Exercises.ShouldBeEmpty();
        await _api.DidNotReceive().GetExercisesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnAppearingAsync_ApiFails_ShowsError()
    {
        var sut = CreateSut();
        _api.GetExercisesAsync(_examId, Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("boom"));

        await sut.OnAppearingAsync();

        sut.HasError.ShouldBeTrue();
        sut.IsBusy.ShouldBeFalse();
    }

    [Fact]
    public async Task Start_WithNotEnoughQuestions_IsDisabledAndShowsMessage()
    {
        var sut = CreateSut(Exercise(available: 10));
        await sut.OnAppearingAsync();
        var item = sut.Exercises.Single();

        sut.StartCommand.CanExecute(item).ShouldBeFalse();
        item.AvailabilityText.ShouldBe("Aún no hay preguntas suficientes (10 de 55)");
        item.IsAvailable.ShouldBeFalse();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("21")]
    [InlineData("abc")]
    [InlineData("10,123")]
    public async Task Start_WithInvalidPassScore_ShowsErrorAndDoesNotNavigate(string text)
    {
        var sut = CreateSut(Exercise());
        await sut.OnAppearingAsync();
        var item = sut.Exercises.Single();
        item.PassScoreText = text;

        await sut.StartCommand.ExecuteAsync(item);

        item.HasPassScoreError.ShouldBeTrue();
        await _navigation.DidNotReceive().GoToAsync(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, object>?>());
    }

    [Fact]
    public async Task Start_WithCommaDecimal_NavigatesAndRemembersPassScore()
    {
        var sut = CreateSut(Exercise());
        await sut.OnAppearingAsync();
        var item = sut.Exercises.Single();
        item.PassScoreText = "12,5";

        await sut.StartCommand.ExecuteAsync(item);

        await _dialogs.Received(1).ConfirmAsync(
            Arg.Any<string>(),
            "Tendrás 50 minutos para 55 preguntas. Al acabar el tiempo se entrega solo.",
            "Empezar",
            "Cancelar");
        await _navigation.Received(1).GoToAsync(
            Routes.MockExam,
            Arg.Is<IReadOnlyDictionary<string, object>?>(p =>
                p != null
                && (Guid)p[QueryKeys.ExamId] == _examId
                && (int)p[QueryKeys.ExerciseNumber] == 1
                && (decimal)p[QueryKeys.PassScore] == 12.5m));
        _preferences.Values[MockExamsViewModel.PassScoreStorageKey(_examId, 1)].ShouldBe("12.5");
    }

    [Fact]
    public async Task Start_WhenUserCancelsConfirmation_DoesNotNavigateNorSave()
    {
        var sut = CreateSut(Exercise());
        await sut.OnAppearingAsync();
        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        await sut.StartCommand.ExecuteAsync(sut.Exercises.Single());

        await _navigation.DidNotReceive().GoToAsync(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, object>?>());
        _preferences.Values.ShouldBeEmpty();
    }

    [Fact]
    public void Number_UsesSpanishFormat()
    {
        MockExamTexts.Number(12.73m).ShouldBe("12,73");
        MockExamTexts.Number(20m).ShouldBe("20");
        MockExamTexts.StoredNumber(12.5m).ShouldBe(12.5m.ToString("0.##", CultureInfo.InvariantCulture));
    }
}