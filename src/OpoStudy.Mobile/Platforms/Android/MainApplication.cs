using Android.App;
using Android.Runtime;

namespace OpoStudy.Mobile;

[Application]
public class MainApplication : MauiApplication
{
	public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
	}

	public override void OnCreate()
	{
		base.OnCreate();
		global::OpoStudy.Mobile.Platforms.Android.Reminders.ReminderNotifications.EnsureChannel(this);
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
