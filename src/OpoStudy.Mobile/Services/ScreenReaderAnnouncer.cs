using OpoStudy.Mobile.Core.Services;

namespace OpoStudy.Mobile.Services;

/// <summary><see cref="IScreenReaderAnnouncer"/> backed by MAUI semantic announcements.</summary>
internal sealed class ScreenReaderAnnouncer : IScreenReaderAnnouncer
{
    public void Announce(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            SemanticScreenReader.Default.Announce(message);
        }
    }
}
