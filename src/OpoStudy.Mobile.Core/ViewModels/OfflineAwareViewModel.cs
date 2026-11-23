using OpoStudy.Mobile.Core.Offline;

namespace OpoStudy.Mobile.Core.ViewModels;

/// <summary>Base of the screens that show the offline banner. It follows <see cref="OfflineState"/> while the page is visible.</summary>
public abstract class OfflineAwareViewModel : ViewModelBase
{
    protected OfflineAwareViewModel(OfflineState offlineState)
    {
        ArgumentNullException.ThrowIfNull(offlineState);
        OfflineState = offlineState;
    }

    protected OfflineState OfflineState { get; }

    public virtual bool ShowOfflineBanner => OfflineState.IsOffline || OfflineState.SyncingAnswers > 0;

    public virtual string OfflineBannerText => OfflineState.IsOffline
        ? OfflineState.BannerText
        : OfflineState.SyncingAnswers == 1 ? "Sincronizando 1 respuesta…" : $"Sincronizando {OfflineState.SyncingAnswers} respuestas…";

    /// <summary>Call from <c>OnAppearingAsync</c>; the subscription ends in <see cref="OnDisappearing"/>.</summary>
    protected void ListenToOfflineState()
    {
        OfflineState.Changed -= OnOfflineStateChanged;
        OfflineState.Changed += OnOfflineStateChanged;
        RaiseOfflineChanged();
    }

    public override void OnDisappearing()
    {
        OfflineState.Changed -= OnOfflineStateChanged;
        base.OnDisappearing();
    }

    /// <summary>Notifies the banner bindings; screens with more state tied to being offline override it.</summary>
    protected virtual void RaiseOfflineChanged()
    {
        OnPropertyChanged(nameof(ShowOfflineBanner));
        OnPropertyChanged(nameof(OfflineBannerText));
    }

    private void OnOfflineStateChanged(object? sender, EventArgs e) => RaiseOfflineChanged();
}
