namespace OpoStudy.Contracts.Progress;

/// <summary>The user's level and XP.</summary>
/// <param name="Level">Current level (starts at 1).</param>
/// <param name="TotalXp">Total XP earned.</param>
/// <param name="XpIntoLevel">XP earned since reaching the current level.</param>
/// <param name="XpForNextLevel">XP the current level needs to reach the next one.</param>
/// <param name="XpToday">XP earned today in the user's time zone, bonus included.</param>
/// <param name="SpendableXp">XP available to spend (total XP minus spent XP).</param>
public sealed record LevelResponse(int Level, int TotalXp, int XpIntoLevel, int XpForNextLevel, int XpToday, int SpendableXp);
