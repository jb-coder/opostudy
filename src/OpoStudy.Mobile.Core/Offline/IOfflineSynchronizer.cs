namespace OpoStudy.Mobile.Core.Offline;

/// <summary>Sends the pending answers and keeps <see cref="OfflineState"/> up to date. It never throws (except for the caller's cancellation).</summary>
public interface IOfflineSynchronizer
{
    /// <summary>Raised after the queue is flushed because the connection came back, so screens can reload what the server computed.</summary>
    event EventHandler? ReconnectSynced;

    /// <summary>Sends everything pending. Returns how many answers reached the server.</summary>
    Task<int> SyncAsync(CancellationToken cancellationToken = default);
}
