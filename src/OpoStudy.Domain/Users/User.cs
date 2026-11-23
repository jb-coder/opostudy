using OpoStudy.Domain.Common;

namespace OpoStudy.Domain.Users;

public sealed class User
{
    private User()
    {
        FirebaseUid = string.Empty;
        TimeZone = Limits.DefaultTimeZone;
        Streak = new Streak();
    }

    public Guid Id { get; private set; }

    /// <summary>User id in Firebase Auth. It is personal data: never write it to logs.</summary>
    public string FirebaseUid { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? ActiveExamId { get; private set; }

    /// <summary>Questions to answer per day.</summary>
    public int DailyGoal { get; private set; } = Limits.DefaultDailyGoal;

    /// <summary>IANA time zone id that defines the user's day.</summary>
    public string TimeZone { get; private set; }

    public Streak Streak { get; private set; }

    /// <summary>Total experience points. It never decreases.</summary>
    public int Xp { get; private set; }

    /// <summary>XP spent on purchases (streak freezes). <see cref="Xp"/> and the level are not affected.</summary>
    public int XpSpent { get; private set; }

    /// <summary>XP available to spend.</summary>
    public int SpendableXp => Xp - XpSpent;

    public int Level => XpRules.LevelFor(Xp);

    public static User Create(string firebaseUid, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        FirebaseUid = Guard.RequiredText(firebaseUid, Limits.FirebaseUid, "user.invalid_firebase_uid"),
        CreatedAt = now.ToUniversalTime(),
    };

    public void GainXp(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        Xp = checked(Xp + amount);
    }

    /// <summary>Buys a streak freeze for <see cref="XpRules.StreakFreezePrice"/> XP.</summary>
    public void BuyStreakFreeze()
    {
        if (Streak.Freezes >= Limits.MaxStreakFreezes)
        {
            throw new DomainRuleException("streak.freezes_full", $"Ya tienes el máximo de {Limits.MaxStreakFreezes} protectores.");
        }

        if (SpendableXp < XpRules.StreakFreezePrice)
        {
            throw new DomainRuleException("xp.insufficient", $"Necesitas {XpRules.StreakFreezePrice} XP disponibles para comprar un protector.");
        }

        XpSpent = checked(XpSpent + XpRules.StreakFreezePrice);
        Streak.AddFreeze();
    }

    public void ChooseExam(Guid examId) => ActiveExamId = examId;

    public void SetDailyGoal(int dailyGoal)
    {
        if (dailyGoal is < Limits.MinDailyGoal or > Limits.MaxDailyGoal)
        {
            throw new DomainRuleException(
                "user.invalid_daily_goal",
                $"El objetivo diario debe estar entre {Limits.MinDailyGoal} y {Limits.MaxDailyGoal} preguntas.");
        }

        DailyGoal = dailyGoal;
    }

    public void SetTimeZone(string timeZone)
    {
        if (!IsValidTimeZone(timeZone))
        {
            throw new DomainRuleException("user.invalid_time_zone", "La zona horaria no es válida.");
        }

        TimeZone = timeZone;
    }

    /// <summary>Whether <paramref name="timeZone"/> is an existing IANA id of at most <see cref="Limits.TimeZone"/> characters.</summary>
    public static bool IsValidTimeZone(string? timeZone) =>
        !string.IsNullOrWhiteSpace(timeZone)
        && timeZone.Length <= Limits.TimeZone
        && TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out _);

    /// <summary>The user's local date at <paramref name="now"/>.</summary>
    public DateOnly TodayAt(DateTimeOffset now) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(TimeZone)).DateTime);
}