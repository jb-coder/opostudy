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
            try
            {
                await lifecycleAware.OnAppearingAsync();
            }
            catch (Exception ex)
            {
                // async void must never throw: a failing ViewModel must not crash the app.
                System.Diagnostics.Debug.WriteLine($"OnAppearingAsync failed: {ex}");
            }
        }
    }
}
