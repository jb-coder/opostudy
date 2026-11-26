using System.Collections.ObjectModel;
using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Extensions.Logging;

using OpoStudy.Contracts.MockExams;
using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.MockExams;
using OpoStudy.Mobile.Core.Navigation;
using OpoStudy.Mobile.Core.Offline;
using OpoStudy.Mobile.Core.Reminders;
using OpoStudy.Mobile.Core.Services;

namespace OpoStudy.Mobile.Core.ViewModels;

/// <summary>An exercise as shown in the list, with the pass score the user is about to use.</summary>
public sealed partial class ExerciseItem(ExamExerciseResponse exercise, string passScoreText) : ObservableObject
{
    public ExamExerciseResponse Exercise { get; } = exercise;

    public int Number => Exercise.Number;

    public string Title => Exercise.Title;

    public string Details => MockExamTexts.Details(Exercise);

    public string PenaltyText => MockExamTexts.Penalty(Exercise.PenaltyRatio);

    public bool IsAvailable => Exercise.AvailableQuestionCount >= Exercise.QuestionCount;

    public string AvailabilityText => IsAvailable
        ? string.Empty
        : MockExamTexts.NotEnoughQuestions(Exercise.AvailableQuestionCount, Exercise.QuestionCount);

    public bool HasAvailabilityText => !IsAvailable;

    public string AccessibilityText =>
        $"{Title}. {Details}. {PenaltyText}" + (IsAvailable ? string.Empty : $". {AvailabilityText}");

    [ObservableProperty]
    public partial string PassScoreText { get; set; } = passScoreText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPassScoreError))]
    public partial string PassScoreError { get; set; } = string.Empty;

    public bool HasPassScoreError => PassScoreError.Length > 0;

    partial void OnPassScoreTextChanged(string value) => PassScoreError = string.Empty;

    /// <summary>Parses the typed pass score (comma or dot as decimal separator). Sets <see cref="PassScoreError"/> when invalid.</summary>
    public bool TryGetPassScore(out decimal passScore)
    {
        var text = PassScoreText.Trim().Replace(',', '.');
        if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out passScore))
        {
            PassScoreError = "Escribe un número, por ejemplo 10 o 12,5.";
            return false;
        }

        if (decimal.Round(passScore, 2) != passScore)
        {
            PassScoreError = "Usa como máximo dos decimales.";
            return false;
        }

        if (passScore <= 0 || passScore > Exercise.MaxScore)
        {
            PassScoreError = $"La nota debe ser mayor que 0 y no pasar de {MockExamTexts.Number(Exercise.MaxScore)}.";
            return false;
        }

        return true;
    }
}

/// <summary>Exercises of an exam: choose one and its pass score to start a mock exam.</summary>
public sealed partial class MockExamsViewModel(
    IOpoStudyApi api,
    INavigationService navigation,
    IDialogService dialogs,
    IPreferencesStore preferences,
    OfflineState offlineState,
    ILogger<MockExamsViewModel> logger) : OfflineAwareViewModel(offlineState), IQueryReceiver
{
    private const string MissingExamMessage = "No se ha podido abrir la oposición. Vuelve atrás e inténtalo de nuevo.";

    private Guid _examId;

    public ObservableCollection<ExerciseItem> Exercises { get; } = [];

    [ObservableProperty]
    public partial string ExamName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    /// <summary>There is no connection: nothing can be started.</summary>
    [ObservableProperty]
    public partial bool NeedsConnection { get; set; }

    public static string PassScoreStorageKey(Guid examId, int exerciseNumber) =>
        string.Create(CultureInfo.InvariantCulture, $"mock-exam.pass-score.{examId}.{exerciseNumber}");

    public void ApplyQuery(IReadOnlyDictionary<string, object> query)
    {
        if (QueryValues.ReadGuid(query, QueryKeys.ExamId) is { } id)
        {
            _examId = id;
        }

        if (query.TryGetValue(QueryKeys.ExamName, out var name) && name is string examName)
        {
            ExamName = examName;
        }
    }

    public override async Task OnAppearingAsync()
    {
        ListenToOfflineState();
        if (OfflineState.IsOffline)
        {
            ShowNeedsConnection();
            return;
        }

        if (RefreshCommand.CanExecute(null))
        {
            await RefreshCommand.ExecuteAsync(null);
        }
    }

    protected override void OnBusyChanged()
    {
        RefreshCommand.NotifyCanExecuteChanged();
        StartCommand.NotifyCanExecuteChanged();
    }

    private void ShowNeedsConnection()
    {
        NeedsConnection = true;
        IsEmpty = false;
        ErrorMessage = null;
        Exercises.Clear();
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        if (_examId == Guid.Empty)
        {
            ErrorMessage = MissingExamMessage;
            return;
        }

        IsBusy = true;
        try
        {
            var exercises = await api.GetExercisesAsync(_examId, cancellationToken);
            OfflineState.ReportSuccess();
            NeedsConnection = false;
            Exercises.Clear();
            foreach (var exercise in exercises)
            {
                Exercises.Add(new ExerciseItem(exercise, InitialPassScoreText(exercise)));
            }

            IsEmpty = Exercises.Count == 0;
        }
        catch (Exception ex) when (ConnectivityErrors.IsConnectivityError(ex, cancellationToken))
        {
            LogLoadFailed(ex, ex.GetType().FullName);
            OfflineState.ReportConnectivityFailure();
            ShowNeedsConnection();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogLoadFailed(ex, ex.GetType().FullName);
            IsEmpty = false;
            ErrorMessage = ApiErrorMessages.From(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>The last pass score used in the exercise, or the default one.</summary>
    private string InitialPassScoreText(ExamExerciseResponse exercise)
    {
        var stored = preferences.GetString(PassScoreStorageKey(_examId, exercise.Number));
        if (stored is not null
            && decimal.TryParse(stored, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
            && value > 0
            && value <= exercise.MaxScore)
        {
            return MockExamTexts.Number(value);
        }

        return MockExamTexts.Number(exercise.DefaultPassScore);
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync(ExerciseItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!item.IsAvailable || OfflineState.IsOffline || !item.TryGetPassScore(out var passScore))
        {
            return;
        }

        IsBusy = true;
        try
        {
            var exercise = item.Exercise;
            var confirmed = await dialogs.ConfirmAsync(
                exercise.Title,
                MockExamTexts.StartConfirmation(exercise.DurationMinutes, exercise.QuestionCount),
                "Empezar",
                "Cancelar");
            if (!confirmed)
            {
                return;
            }

            preferences.SetString(PassScoreStorageKey(_examId, exercise.Number), MockExamTexts.StoredNumber(passScore));
            await navigation.GoToAsync(
                Routes.MockExam,
                new Dictionary<string, object>
                {
                    [QueryKeys.ExamId] = _examId,
                    [QueryKeys.ExerciseNumber] = exercise.Number,
                    [QueryKeys.PassScore] = passScore,
                });
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanStart(ExerciseItem? item) => IsNotBusy && item is { IsAvailable: true };

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not load the exercises ({ExceptionType})")]
    private partial void LogLoadFailed(Exception exception, string? exceptionType);
}