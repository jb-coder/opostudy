using Android.Content.Res;

using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

namespace OpoStudy.Mobile;

/// <summary>
/// MAUI has no property for the track colour of a <see cref="ProgressBar"/> on Android, and its default is almost invisible in the dark theme.
/// The track takes the design system <c>Outline</c> colour; it is applied again whenever the progress colour (which follows the theme) changes.
/// </summary>
internal static class ProgressBarTrack
{
    public static void Register() =>
        ProgressBarHandler.Mapper.AppendToMapping(nameof(Microsoft.Maui.IProgress.ProgressColor), Apply);

    private static void Apply(IProgressBarHandler handler, Microsoft.Maui.IProgress progress)
    {
        var dark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var key = dark ? "OutlineDark" : "OutlineLight";
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color)
        {
            handler.PlatformView.ProgressBackgroundTintList = ColorStateList.ValueOf(color.ToPlatform());
        }
    }
}
