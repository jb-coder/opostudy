namespace OpoStudy.Contracts.Progress;

/// <summary>XP earned by one answer.</summary>
/// <param name="Gained">XP earned, <paramref name="Bonus"/> included.</param>
/// <param name="Bonus">Daily-goal bonus; 0 except for the answer that meets the goal.</param>
/// <param name="TotalXp">Total XP after this answer.</param>
/// <param name="Level">Level after this answer.</param>
/// <param name="LeveledUp"><c>true</c> only for the answer that changes the level.</param>
public sealed record XpGainResponse(int Gained, int Bonus, int TotalXp, int Level, bool LeveledUp);
