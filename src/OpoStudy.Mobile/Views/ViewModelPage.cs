using OpoStudy.Mobile.Core.ViewModels;

namespace OpoStudy.Mobile.Views;

/// <summary>Base page: binds its ViewModel and forwards the appearing event to it.</summary>
public abstract class ViewModelPage : ContentPage
{
    protected ViewModelPage(object viewModel)
    {
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is IPageLifecycleAware lifecycleAware)
        {
            await lifecycleAware.OnAppearingAsync();
        }
    }
}