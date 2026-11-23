using OpoStudy.Mobile.Core.Offline;

namespace OpoStudy.Mobile.Services;

/// <summary><see cref="INetworkStatus"/> on top of MAUI <see cref="IConnectivity"/>. <see cref="Changed"/> is raised on the main thread.</summary>
internal sealed class MauiNetworkStatus : INetworkStatus, IDisposable
{
    private readonly IConnectivity _connectivity;

    public MauiNetworkStatus(IConnectivity connectivity)
    {
        _connectivity = connectivity;
        _connectivity.ConnectivityChanged += OnConnectivityChanged;
    }

    public event EventHandler? Changed;

    public bool HasInternet => _connectivity.NetworkAccess == NetworkAccess.Internet;

    public void Dispose() => _connectivity.ConnectivityChanged -= OnConnectivityChanged;

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e) =>
        MainThread.BeginInvokeOnMainThread(() => Changed?.Invoke(this, EventArgs.Empty));
}
