using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using OpoStudy.Contracts.Gradings;
using OpoStudy.Mobile.Core.PracticalCases;

namespace OpoStudy.Mobile.Core.ViewModels;

/// <summary>A criterion of the rubric with the points awarded. The mark is text plus icon, never only colour.</summary>
public sealed record CriterionItem(string Description, string PointsText, string MarkText, string MarkIcon, bool IsComplete, bool IsNone)
{
    public bool IsPartial => !IsComplete && !IsNone;

    /// <summary>Icon and text together, e.g. "✓ Completo".</summary>
    public string MarkLabel => $"{MarkIcon} {MarkText}";

    public string AccessibilityText => $"{Description}. {PointsText}. {MarkText}";

    public static CriterionItem From(CriterionScoreResponse criterion)
    {
        ArgumentNullException.ThrowIfNull(criterion);

        var complete = criterion.Awarded == criterion.Points;
        var none = !complete && criterion.Awarded == 0;
        var (text, icon) = complete
            ? (PracticalCaseTexts.CriterionComplete, "✓")
            : none ? (PracticalCaseTexts.CriterionNone, "✕") : (PracticalCaseTexts.CriterionPartial, "◐");
        return new CriterionItem(
            criterion.Description,
            PracticalCaseTexts.Ratio(criterion.Awarded, criterion.Points),
            text,
            icon,
            complete,
            none);
    }
}

/// <summary>A collapsible block of text of a part (statement, the user's answer or the model answer). Collapsed by default.</summary>
public sealed partial class GradingBlock(string title, string content) : ObservableObject
{
    public string Title { get; } = title;

    public string Content { get; } = content;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Indicator))]
    [NotifyPropertyChangedFor(nameof(StateText))]
    public partial bool IsExpanded { get; set; }

    public string Indicator => IsExpanded ? "▾" : "▸";

    /// <summary>What a screen reader announces: the title and whether the block is open.</summary>
    public string StateText => $"{Title}, {(IsExpanded ? "desplegado" : "plegado")}";

    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;
}

/// <summary>One part of a completed grading: score, comments, criteria and the collapsible blocks.</summary>
public sealed class GradingPartItem
{
    public GradingPartItem(GradingAnswerResponse answer)
    {
        ArgumentNullException.ThrowIfNull(answer);

        Order = answer.Order;
        Title = PracticalCaseTexts.PartResult(answer.Order, answer.Score, answer.MaxPoints);
        Feedback = string.IsNullOrWhiteSpace(answer.Feedback) ? null : answer.Feedback;
        Criteria = [.. (answer.Criteria ?? []).Select(CriterionItem.From)];
        Statement = new GradingBlock(PracticalCaseTexts.StatementBlock, answer.Statement);
        Answer = new GradingBlock(
            PracticalCaseTexts.AnswerBlock,
            string.IsNullOrWhiteSpace(answer.Text) ? PracticalCaseTexts.BlankAnswer : answer.Text);
        Model = answer.ModelAnswer is null ? null : new GradingBlock(PracticalCaseTexts.ModelAnswerBlock, answer.ModelAnswer);
        Blocks = [.. new[] { Statement, Answer, Model }.OfType<GradingBlock>()];
    }

    public int Order { get; }

    public string Title { get; }

    public string? Feedback { get; }

    public bool HasFeedback => Feedback is not null;

    public IReadOnlyList<CriterionItem> Criteria { get; }

    public bool HasCriteria => Criteria.Count > 0;

    public GradingBlock Statement { get; }

    public GradingBlock Answer { get; }

    /// <summary><c>null</c> when the server did not send a model answer (it hides the block).</summary>
    public GradingBlock? Model { get; }

    public bool HasModel => Model is not null;

    /// <summary>The blocks to show, in order; the model answer is left out when there is none.</summary>
    public IReadOnlyList<GradingBlock> Blocks { get; }
}