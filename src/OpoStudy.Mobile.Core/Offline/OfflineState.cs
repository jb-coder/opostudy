namespace OpoStudy.Mobile.Core.Offline;

/// <summary>
/// Shared "offline" state of the app, so every screen agrees: it is offline while the device has no Internet or the
/// last API call failed for connectivity, and it stays so until an API call succeeds again.
/// </summary>
public sealed class OfflineState : IDisposable
{
    public const string BannerText = "Sin conexión. Tus respuestas se guardan y se sincronizarán al recuperarla.";

    private readonly INetworkStatus _network;

    private bool _lastCallFailed;

    private int _syncingAnswers;

    public OfflineState(INetworkStatus network)
    {
        ArgumentNullException.ThrowIfNull(network);
        _network = network;
        _network.Changed += OnNetworkChanged;
    }

    /// <summary>Raised when <see cref="IsOffline"/> or <see cref="SyncingAnswers"/> may have changed.</summary>
    public event EventHandler? Changed;

    public bool HasInternet => _network.HasInternet;

    public bool IsOffline => !_network.HasInternet || _lastCallFailed;

    /// <summary>Answers being sent right now, or 0.</summary>
    public int SyncingAnswers => _syncingAnswers;

    /// <summary>An API call failed because the server could not be reached.</summary>
    public void ReportConnectivityFailure() => Set(failed: true, syncing: _syncingAnswers);

    /// <summary>An API call succeeded.</summary>
    public void ReportSuccess() => Set(failed: false, syncing: _syncingAnswers);

    public void BeginSync(int answers) => Set(_lastCallFailed, answers);

    public void EndSync() => Set(_lastCallFailed, 0);

    public void Dispose() => _network.Changed -= OnNetworkChanged;

    private void Set(bool failed, int syncing)
    {
        var changed = failed != _lastCallFailed || syncing != _syncingAnswers;
        _lastCallFailed = failed;
        _syncingAnswers = syncing;
        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnNetworkChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);
}
