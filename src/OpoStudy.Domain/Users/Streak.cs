using OpoStudy.Domain.Common;

namespace OpoStudy.Domain.Users;

/// <summary>Streak of consecutive days meeting the daily goal. Owned by <see cref="User"/> (complex type).</summary>
public sealed class Streak
{
    /// <summary>Best streak ever reached.</summary>
    public int Longest { get; private set; }

    /// <summary>Last day the daily goal was met, in the user's time zone.</summary>
    public DateOnly? LastMetOn { get; private set; }

    /// <summary>Length of the streak that ends on <see cref="LastMetOn"/>.</summary>
    public int LastLength { get; private set; }

    /// <summary>Streak freezes in stock (0 to <see cref="Limits.MaxStreakFreezes"/>).</summary>
    public int Freezes { get; private set; }

    /// <summary>Last day covered by a streak freeze.</summary>
    public DateOnly? ProtectedThrough { get; private set; }

    /// <summary>Last day that kept the streak alive: the goal was met or a freeze covered it.</summary>
    private DateOnly? KeptThrough =>
        (LastMetOn, ProtectedThrough) switch
        {
            ({ } met, { } prot) => met >= prot ? met : prot,
            ({ } met, null) => met,
            (null, { } prot) => prot,
            _ => null,
        };

    /// <summary>Registers that the goal was met on <paramref name="day"/>. A day already kept is ignored.</summary>
    public void RegisterGoalMet(DateOnly day)
    {
        if (KeptThrough is { } kept)
        {
            if (day <= kept)
            {
                return;
            }

            LastLength = LastLength > 0 && day == kept.AddDays(1) ? LastLength + 1 : 1;
        }
        else
        {
            LastLength = 1;
        }

        LastMetOn = day;
        Longest = Math.Max(Longest, LastLength);
    }

    /// <summary>Current streak as seen on <paramref name="today"/>: alive only if yesterday or today kept it.</summary>
    public int CurrentAt(DateOnly today) =>
        KeptThrough is { } kept && kept >= today.AddDays(-1) ? LastLength : 0;

    /// <summary>Adds a freeze to the stock.</summary>
    public void AddFreeze()
    {
        if (Freezes >= Limits.MaxStreakFreezes)
        {
            throw new DomainRuleException("streak.freezes_full", $"Ya tienes el máximo de {Limits.MaxStreakFreezes} protectores.");
        }

        Freezes++;
    }

    /// <summary>
    /// Settles the closed days (up to yesterday) not kept since the streak was last kept. If there are enough freezes
    /// they are spent and those days are returned; otherwise the streak is lost and no freeze is spent.
    /// </summary>
    public IReadOnlyList<DateOnly> Settle(DateOnly today)
    {
        if (LastLength == 0 || LastMetOn is null || KeptThrough is not { } kept)
        {
            return [];
        }

        var yesterday = today.AddDays(-1);
        var missed = yesterday.DayNumber - kept.DayNumber;
        if (missed <= 0)
        {
            return [];
        }

        if (missed > Freezes)
        {
            LastLength = 0;
            return [];
        }

        var days = Enumerable.Range(1, missed).Select(kept.AddDays).ToList();
        Freezes -= missed;
        ProtectedThrough = yesterday;
        return days;
    }
}
