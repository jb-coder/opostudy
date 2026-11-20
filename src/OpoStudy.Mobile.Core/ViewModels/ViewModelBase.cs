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

    public virtual Task OnAppearingAsync() => Task.CompletedTask;

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
