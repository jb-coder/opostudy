using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using OpoStudy.Contracts.Progress;
using OpoStudy.Mobile.Core.Reminders;

using Shouldly;

namespace OpoStudy.Mobile.Core.Tests;

public sealed class ReminderServiceTests
{
    private static readonly TimeOnly Nine = new(21, 0);

    private readonly FakeReminderStore _store = new();

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 20, 10, 0, 0, TimeSpan.Zero));

    private IReminderScheduler _scheduler = ReminderTestSupport.SupportedScheduler();

    public ReminderServiceTests() => _time.SetLocalTimeZone(TimeZoneInfo.Utc);

    private ReminderService CreateSut() => new(_store, _scheduler, _time);

    [Fact]
    public async Task EnableAsync_PermissionGranted_SavesAndSchedules()
    {
        var sut = CreateSut();

        var enabled = await sut.EnableAsync(Nine);

        enabled.ShouldBeTrue();
        _store.Settings.ShouldBe(new ReminderSettings(true, Nine));
        _scheduler.Received(1).Schedule(new DateTimeOffset(2026, 11, 20, 21, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task EnableAsync_PermissionDenied_DoesNotSaveNorSchedule()
    {
        _scheduler = ReminderTestSupport.SupportedScheduler(permissionGranted: false);
        var sut = CreateSut();

        var enabled = await sut.EnableAsync(Nine);

        enabled.ShouldBeFalse();
        _store.Settings.ShouldBe(ReminderSettings.Default);
        _scheduler.DidNotReceive().Schedule(Arg.Any<DateTimeOffset>());
    }

    [Fact]
    public async Task EnableAsync_Unsupported_ReturnsFalseWithoutAskingPermission()
    {
        _scheduler = new NullReminderScheduler();
        var sut = CreateSut();

        (await sut.EnableAsync(Nine)).ShouldBeFalse();
        _store.Settings.Enabled.ShouldBeFalse();
    }

    [Fact]
    public void Disable_Enabled_SavesDisabledAndCancels()
    {
        _store.Settings = new ReminderSettings(true, Nine);
        var sut = CreateSut();

        sut.Disable();

        _store.Settings.ShouldBe(new ReminderSettings(false, Nine));
        _scheduler.Received(1).Cancel();
    }

    [Fact]
    public void ChangeTime_Enabled_SavesAndReschedules()
    {
        _store.Settings = new ReminderSettings(true, ReminderSettings.DefaultTime);
        var sut = CreateSut();

        sut.ChangeTime(Nine);

        _store.Settings.Time.ShouldBe(Nine);
        _scheduler.Received(1).Schedule(new DateTimeOffset(2026, 11, 20, 21, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void ChangeTime_Disabled_SavesWithoutScheduling()
    {
        var sut = CreateSut();

        sut.ChangeTime(Nine);

        _store.Settings.ShouldBe(new ReminderSettings(false, Nine));
        _scheduler.DidNotReceive().Schedule(Arg.Any<DateTimeOffset>());
    }

    [Fact]
    public void EnsureScheduled_Enabled_Schedules()
    {
        _store.Settings = new ReminderSettings(true, ReminderSettings.DefaultTime);
        var sut = CreateSut();

        sut.EnsureScheduled();

        _scheduler.Received(1).Schedule(new DateTimeOffset(2026, 11, 20, 20, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void EnsureScheduled_Disabled_DoesNothing()
    {
        var sut = CreateSut();

        sut.EnsureScheduled();

        _scheduler.DidNotReceive().Schedule(Arg.Any<DateTimeOffset>());
    }

    [Fact]
    public void OnSignedOut_Always_CancelsClearsSnapshotAndKeepsSettings()
    {
        _store.Settings = new ReminderSettings(true, Nine);
        _store.Snapshot = new ReminderSnapshot(new DateOnly(2026, 11, 20), 3, 10, false, 2);
        var sut = CreateSut();

        sut.OnSignedOut();

        _scheduler.Received(1).Cancel();
        _store.Snapshot.ShouldBeNull();
        _store.Settings.ShouldBe(new ReminderSettings(true, Nine));
    }

    [Fact]
    public void OnSignedOut_ThenEnsureScheduled_DoesNotSchedule()
    {
        _store.Settings = new ReminderSettings(true, Nine);
        var sut = CreateSut();

        sut.OnSignedOut();
        sut.EnsureScheduled();

        _store.Suspended.ShouldBeTrue();
        _store.Settings.ShouldBe(new ReminderSettings(true, Nine));
        _scheduler.DidNotReceive().Schedule(Arg.Any<DateTimeOffset>());
    }

    [Fact]
    public void OnSignedOut_ThenAlarmFired_ReturnsNullAndDoesNotSchedule()
    {
        _store.Settings = new ReminderSettings(true, Nine);
        var sut = CreateSut();

        sut.OnSignedOut();
        var message = sut.OnAlarmFired(_time.GetUtcNow());

        message.ShouldBeNull();
        _scheduler.DidNotReceive().Schedule(Arg.Any<DateTimeOffset>());
    }

    [Fact]
    public void OnSignedIn_Enabled_LiftsSuspensionAndSchedules()
    {
        _store.Settings = new ReminderSettings(true, Nine);
        _store.Suspended = true;
        var sut = CreateSut();

        sut.OnSignedIn();

        _store.Suspended.ShouldBeFalse();
        _scheduler.Received(1).Schedule(new DateTimeOffset(2026, 11, 20, 21, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void OnSignedIn_Disabled_DoesNotSchedule()
    {
        _store.Suspended = true;
        var sut = CreateSut();

        sut.OnSignedIn();

        _store.Suspended.ShouldBeFalse();
        _scheduler.DidNotReceive().Schedule(Arg.Any<DateTimeOffset>());
    }

    [Fact]
    public void OnAlarmFired_Disabled_ReturnsNullAndDoesNotSchedule()
    {
        var sut = CreateSut();

        sut.OnAlarmFired(_time.GetUtcNow()).ShouldBeNull();

        _store.LastFiredOn.ShouldBeNull();
        _scheduler.DidNotReceive().Schedule(Arg.Any<DateTimeOffset>());
    }

    [Fact]
    public void OnAlarmFired_GoalMet_ReturnsNullAndSchedulesTomorrow()
    {
        _store.Settings = new ReminderSettings(true, Nine);
        _store.Snapshot = new ReminderSnapshot(new DateOnly(2026, 11, 20), 10, 10, true, 3);
        var sut = CreateSut();

        sut.OnAlarmFired(_time.GetUtcNow()).ShouldBeNull();

        _store.LastFiredOn.ShouldBe(new DateOnly(2026, 11, 20));
        _scheduler.Received(1).Schedule(new DateTimeOffset(2026, 11, 21, 21, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void OnAlarmFired_GoalNotMet_ReturnsMessageSavesDayAndSchedulesTomorrow()
    {
        _time.SetUtcNow(new DateTimeOffset(2026, 11, 20, 21, 5, 0, TimeSpan.Zero));
        _store.Settings = new ReminderSettings(true, Nine);
        _store.Snapshot = new ReminderSnapshot(new DateOnly(2026, 11, 20), 7, 10, false, 3);
        var sut = CreateSut();

        var message = sut.OnAlarmFired(_time.GetUtcNow());

        message.ShouldNotBeNull();
        _store.LastFiredOn.ShouldBe(new DateOnly(2026, 11, 20));
        _scheduler.Received(1).Schedule(new DateTimeOffset(2026, 11, 21, 21, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void UpdateFromStreak_Always_SavesTodaySnapshot()
    {
        var sut = CreateSut();

        sut.UpdateFromStreak(new StreakResponse(10, new DateOnly(2026, 11, 20), 4, false, 3, 8, [], 1, 2, 200, 0));

        _store.Snapshot.ShouldBe(new ReminderSnapshot(new DateOnly(2026, 11, 20), 4, 10, false, 3));
    }

    [Fact]
    public void UpdateFromProgress_PreviousSnapshotMetToday_KeepsGoalMet()
    {
        _store.Snapshot = new ReminderSnapshot(new DateOnly(2026, 11, 20), 10, 10, true, 3);
        var sut = CreateSut();

        sut.UpdateFromProgress(new DailyProgressResponse(11, 10, false, 3));

        _store.Snapshot.ShouldNotBeNull().GoalMetToday.ShouldBeTrue();
    }

    [Fact]
    public void UpdateFromProgress_PreviousSnapshotMetYesterday_ResetsGoalMet()
    {
        _store.Snapshot = new ReminderSnapshot(new DateOnly(2026, 11, 19), 10, 10, true, 3);
        var sut = CreateSut();

        sut.UpdateFromProgress(new DailyProgressResponse(1, 10, false, 3));

        _store.Snapshot.ShouldBe(new ReminderSnapshot(new DateOnly(2026, 11, 20), 1, 10, false, 3));
    }

    [Fact]
    public void UpdateFromProgress_GoalMetNow_MarksGoalMet()
    {
        var sut = CreateSut();

        sut.UpdateFromProgress(new DailyProgressResponse(10, 10, true, 4));

        _store.Snapshot.ShouldNotBeNull().GoalMetToday.ShouldBeTrue();
    }

    [Fact]
    public void UpdateFromProgress_AnsweredReachesGoalWithoutFlag_MarksGoalMet()
    {
        var sut = CreateSut();

        sut.UpdateFromProgress(new DailyProgressResponse(12, 10, false, 4));

        _store.Snapshot.ShouldNotBeNull().GoalMetToday.ShouldBeTrue();
    }
}

public sealed class PreferencesReminderStoreTests
{
    private readonly FakePreferencesStore _preferences = new();

    private PreferencesReminderStore CreateSut() => new(_preferences);

    [Fact]
    public void GetSettings_NothingSaved_ReturnsDefaults() =>
        CreateSut().GetSettings().ShouldBe(new ReminderSettings(false, new TimeOnly(20, 0)));

    [Fact]
    public void SaveSettings_ThenGet_RoundTrips()
    {
        var sut = CreateSut();

        sut.SaveSettings(new ReminderSettings(true, new TimeOnly(22, 0)));

        sut.GetSettings().ShouldBe(new ReminderSettings(true, new TimeOnly(22, 0)));
        _preferences.Values["reminder.time"].ShouldBe("22:00");
    }

    [Fact]
    public void SaveSnapshot_ThenGet_RoundTrips()
    {
        var sut = CreateSut();
        var snapshot = new ReminderSnapshot(new DateOnly(2026, 11, 20), 3, 10, false, 5);

        sut.SaveSnapshot(snapshot);

        sut.GetSnapshot().ShouldBe(snapshot);
    }

    [Fact]
    public void GetSnapshot_CorruptJson_ReturnsNull()
    {
        _preferences.Values["reminder.snapshot"] = "{not json";

        CreateSut().GetSnapshot().ShouldBeNull();
    }

    [Fact]
    public void ClearSnapshot_Saved_RemovesIt()
    {
        var sut = CreateSut();
        sut.SaveSnapshot(new ReminderSnapshot(new DateOnly(2026, 11, 20), 3, 10, false, 5));

        sut.ClearSnapshot();

        sut.GetSnapshot().ShouldBeNull();
    }
}