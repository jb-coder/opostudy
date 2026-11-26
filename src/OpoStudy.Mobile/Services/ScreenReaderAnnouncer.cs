using OpoStudy.Mobile.Core.Services;

namespace OpoStudy.Mobile.Services;

/// <summary><see cref="IScreenReaderAnnouncer"/> backed by MAUI semantic announcements.</summary>
internal sealed class ScreenReaderAnnouncer : IScreenReaderAnnouncer
{
    public void Announce(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            // Announcements must run on the UI thread; ViewModels may call this after an await on any thread.
            MainThread.BeginInvokeOnMainThread(() => SemanticScreenReader.Default.Announce(message));
        }
    }
}
