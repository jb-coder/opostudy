namespace OpoStudy.Mobile.Core.Offline;

/// <summary>Sends the pending answers and keeps <see cref="OfflineState"/> up to date. It never throws (except for the caller's cancellation).</summary>
public interface IOfflineSynchronizer
{
    /// <summary>Sends everything pending. Returns how many answers reached the server.</summary>
    Task<int> SyncAsync(CancellationToken cancellationToken = default);
}
