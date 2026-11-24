namespace OpoStudy.Domain.Common;

/// <summary>Maximum text lengths and counts. The domain validates them and persistence uses them for column sizes.</summary>
public static class Limits
{
    public const int Slug = 100;
    public const int Name = 300;
    public const int Group = 20;
    public const int Statement = 2000;
    public const int OptionText = 1000;
    public const int Explanation = 4000;
    public const int LegalReference = 300;
    public const int Key = 150;
    public const int FirebaseUid = 128;

    public const int CaseStatement = 8000;
    public const int ModelAnswer = 8000;
    public const int CriterionDescription = 500;
    public const int MinCaseParts = 1;
    public const int MaxCaseParts = 10;
    public const int MinCriteriaPerPart = 1;
    public const int MaxCriteriaPerPart = 10;
    public const int MinCriterionPoints = 1;
    public const int MaxCriterionPoints = 10;
    public const int MinEstimatedMinutes = 5;
    public const int MaxEstimatedMinutes = 240;

    public const int PartAnswer = 8000;
    public const int GradingFeedback = 4000;
    public const int Corrector = 100;
    public const int FailureCode = 100;

    public const int MinOptions = 2;
    public const int MaxOptions = 6;
    public const int MaxQuestionsPerQuiz = 100;

    public const int TimeZone = 64;
    public const int MinDailyGoal = 5;
    public const int MaxDailyGoal = 100;
    public const int DefaultDailyGoal = 10;
    public const string DefaultTimeZone = "Europe/Madrid";
    public const int StreakHistoryDays = 7;
    public const int MaxStreakFreezes = 2;

    /// <summary>Longest date range (both ends included) of the activity statistics.</summary>
    public const int MaxActivityRangeDays = 92;

    /// <summary>Gradings listed as recent scores in the statistics.</summary>
    public const int RecentScoresCount = 10;

    /// <summary>Oldest time of an answer the server accepts when syncing a quiz taken offline.</summary>
    public static readonly TimeSpan MaxOfflineAge = TimeSpan.FromDays(7);
}