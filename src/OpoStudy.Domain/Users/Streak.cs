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

    /// <summary>Registers that the goal was met on <paramref name="day"/>. The same day or an earlier one is ignored.</summary>
    public void RegisterGoalMet(DateOnly day)
    {
        if (LastMetOn is { } last)
        {
            if (day <= last)
            {
                return;
            }

            LastLength = day == last.AddDays(1) ? LastLength + 1 : 1;
        }
        else
        {
            LastLength = 1;
        }

        LastMetOn = day;
        Longest = Math.Max(Longest, LastLength);
    }

    /// <summary>Current streak as seen on <paramref name="today"/>: alive only if the goal was met today or yesterday.</summary>
    public int CurrentAt(DateOnly today) =>
        LastMetOn is { } last && last >= today.AddDays(-1) ? LastLength : 0;
}