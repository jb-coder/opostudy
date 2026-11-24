using CommunityToolkit.Mvvm.ComponentModel;

using OpoStudy.Contracts.Stats;
using OpoStudy.Mobile.Core.Stats;

namespace OpoStudy.Mobile.Core.ViewModels;

/// <summary>A topic with answers, as shown on the statistics screen. The practice message belongs to the topic card.</summary>
/// <param name="topic">The topic counts.</param>
/// <param name="zone">Time zone the last practice date is shown in.</param>
public sealed partial class TopicStatItem(TopicStatsResponse topic, TimeZoneInfo zone) : ObservableObject
{
    public TopicStatsResponse Topic { get; } = topic;

    public Guid Id => Topic.TopicId;

    /// <summary>The content title as is: it already starts with "Tema N.".</summary>
    public string Title => Topic.Title;

    public string? Section => string.IsNullOrWhiteSpace(Topic.Section) ? null : Topic.Section;

    public bool HasSection => Section is not null;

    /// <summary>Share of correct answers (0 to 1) for the progress bar.</summary>
    public double Progress => StatsTexts.Fraction(Topic.CorrectCount, Topic.AnsweredCount);

    public string ScoreText => StatsTexts.TopicScore(Topic.CorrectCount, Topic.AnsweredCount);

    public string LastPracticeText => StatsTexts.LastPractice(Topic.LastAnsweredAt, zone);

    /// <summary>Whether the topic has active questions to practice.</summary>
    public bool HasQuestions => Topic.AvailableQuestionCount > 0;

    public string AccessibilityText => $"{Title}. {ScoreText}. {LastPracticeText}";

    public string PracticeDescription => StatsTexts.PracticeTopicDescription(Title);

    /// <summary>Why the quiz of this topic could not be started (no connection, or the error).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string? Message { get; set; }

    public bool HasMessage => !string.IsNullOrEmpty(Message);
}

/// <summary>One bar of the practical case chart.</summary>
/// <param name="Score">The grading.</param>
/// <param name="Zone">Time zone the day under the bar is shown in.</param>
public sealed record ScoreBarItem(CaseScoreResponse Score, TimeZoneInfo Zone)
{
    /// <summary>Bar height as a fraction (0 to 1) of the chart height.</summary>
    public double Fraction => (double)Math.Clamp(Score.Percent / 100m, 0m, 1m);

    public string PercentText => StatsTexts.WholePercent(Score.Percent);

    public string DateText => StatsTexts.DayMonth(StatsTexts.LocalDate(Score.SubmittedAt, Zone));

    public string AccessibilityText => $"{Score.CaseTitle}, {DateText}: {PercentText}";
}

/// <summary>The best grading of a practical case.</summary>
/// <param name="Best">The best score of the case.</param>
public sealed record BestScoreItem(CaseBestScoreResponse Best)
{
    public Guid GradingId => Best.BestGradingId;

    public string Title => Best.CaseTitle;

    public string? Section => string.IsNullOrWhiteSpace(Best.Section) ? null : Best.Section;

    public bool HasSection => Section is not null;

    public string ScoreText => StatsTexts.BestScore(Best);

    public string AttemptsText => StatsTexts.Attempts(Best.GradingCount);

    public string AccessibilityText => $"{Title}. {ScoreText}. {AttemptsText}";
}