namespace OpoStudy.Mobile.Core.Services;

/// <summary>Announces dynamic messages (results, errors) to the screen reader without navigating.</summary>
public interface IScreenReaderAnnouncer
{
    void Announce(string message);
}
