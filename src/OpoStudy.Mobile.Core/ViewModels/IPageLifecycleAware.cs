namespace OpoStudy.Mobile.Core.ViewModels;

/// <summary>Implemented by ViewModels that react to their page appearing.</summary>
public interface IPageLifecycleAware
{
    Task OnAppearingAsync();
}
