using Android.App;
using Android.Content.PM;
using Android.Content.Res;
using Android.OS;

using AndroidX.Core.View;

using Google.Android.Material.AppBar;

namespace OpoStudy.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        ApplySystemBars();
    }

    protected override void OnResume()
    {
        base.OnResume();
        ApplySystemBars();
    }

    public override void OnConfigurationChanged(Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        ApplySystemBars();
    }

    /// <summary>Status bar background = Background token of the current theme; icons contrast with it.</summary>
    private void ApplySystemBars()
    {
        if (Window is not { } window)
        {
            return;
        }

        var isNight = (Resources?.Configuration?.UiMode & UiMode.NightMask) == UiMode.NightYes;
        var background = new Android.Graphics.Color(AndroidX.Core.Content.ContextCompat.GetColor(this, Resource.Color.appBackground));

#pragma warning disable CA1422 // Ignored on API 35+ (edge-to-edge), still needed on older versions.
        window.SetStatusBarColor(background);
        window.SetNavigationBarColor(background);
#pragma warning restore CA1422

        var controller = WindowCompat.GetInsetsController(window, window.DecorView);
        if (controller is not null)
        {
            controller.AppearanceLightStatusBars = !isNight;
            controller.AppearanceLightNavigationBars = !isNight;
        }

        // The Shell app bar (and the status bar area it pads) keeps the colour resolved when it was inflated:
        // repaint it so it follows a theme switch while the app is open.
        window.DecorView.SetBackgroundColor(background);
        if (FindAppBar(window.DecorView) is { } appBar)
        {
            appBar.SetBackgroundColor(background);
        }
    }

    private static AppBarLayout? FindAppBar(Android.Views.View? view)
    {
        if (view is AppBarLayout appBar)
        {
            return appBar;
        }

        if (view is Android.Views.ViewGroup group)
        {
            for (var i = 0; i < group.ChildCount; i++)
            {
                if (FindAppBar(group.GetChildAt(i)) is { } found)
                {
                    return found;
                }
            }
        }

        return null;
    }
}
