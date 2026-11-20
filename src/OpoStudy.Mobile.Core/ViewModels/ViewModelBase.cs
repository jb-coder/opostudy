using CommunityToolkit.Mvvm.ComponentModel;

namespace OpoStudy.Mobile.Core.ViewModels;

/// <summary>Base class with the busy and error state shared by every screen.</summary>
public abstract partial class ViewModelBase : ObservableObject, IPageLifecycleAware
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    public bool IsNotBusy => !IsBusy;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    private CancellationTokenSource? _pageCancellation;

    /// <summary>Cancelled when the page disappears; a new one is used the next time it appears.</summary>
    protected CancellationToken PageToken => (_pageCancellation ??= new CancellationTokenSource()).Token;

    public virtual Task OnAppearingAsync() => Task.CompletedTask;

    public virtual void OnDisappearing()
    {
        var cancellation = _pageCancellation;
        _pageCancellation = null;
        if (cancellation is not null)
        {
            cancellation.Cancel();
            cancellation.Dispose();
        }
    }

    /// <summary>Called whenever <see cref="IsBusy"/> changes, e.g. to refresh command availability.</summary>
    protected virtual void OnBusyChanged()
    {
    }

    /// <summary>Called whenever <see cref="ErrorMessage"/> changes.</summary>
    protected virtual void OnErrorMessageChanged()
    {
    }

    partial void OnIsBusyChanged(bool value) => OnBusyChanged();

    partial void OnErrorMessageChanged(string? value) => OnErrorMessageChanged();
}
