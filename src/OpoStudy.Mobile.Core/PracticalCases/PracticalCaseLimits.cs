namespace OpoStudy.Mobile.Core.PracticalCases;

/// <summary>Limits of the answers, mirroring the server (which has the final say).</summary>
public static class PracticalCaseLimits
{
    /// <summary>Maximum characters of the answer to a part.</summary>
    public const int AnswerMaxLength = 8000;

    /// <summary>From this length (90 %) the counter warns that the limit is near.</summary>
    public const int NearLimitLength = 7200;

    /// <summary>Delay of the draft autosave after the last change.</summary>
    public static readonly TimeSpan DraftSaveDelay = TimeSpan.FromMilliseconds(1500);
}