namespace OpoStudy.Mobile.Core.ViewModels;

/// <summary>Implemented by ViewModels that react to their page appearing.</summary>
public interface IPageLifecycleAware
{
    Task OnAppearingAsync();

    /// <summary>Called when the page goes away; cancels the work started while it was shown.</summary>
    void OnDisappearing();
}
