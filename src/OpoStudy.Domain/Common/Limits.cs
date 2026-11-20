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

    public const int MinOptions = 2;
    public const int MaxOptions = 6;
    public const int MaxQuestionsPerQuiz = 100;

    public const int TimeZone = 64;
    public const int MinDailyGoal = 5;
    public const int MaxDailyGoal = 100;
    public const int DefaultDailyGoal = 10;
    public const string DefaultTimeZone = "Europe/Madrid";
    public const int StreakHistoryDays = 7;
}