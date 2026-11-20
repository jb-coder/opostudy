namespace OpoStudy.Mobile.Core.Offline;

/// <summary>
/// Whether the device has an Internet connection (implemented in the app on top of the platform connectivity).
/// It only triggers synchronization and drives the banner: the decision to go offline is made by a failed API call.
/// </summary>
public interface INetworkStatus
{
    bool HasInternet { get; }

    /// <summary>Raised when the connectivity changes; <see cref="HasInternet"/> already has the new value.</summary>
    event EventHandler? Changed;
}