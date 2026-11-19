using Microsoft.Extensions.Logging;

using OpoStudy.Mobile.Core.ViewModels;

namespace OpoStudy.Mobile.Views;

/// <summary>Base page: binds its ViewModel and forwards the appearing event to it.</summary>
public abstract partial class ViewModelPage : ContentPage, IQueryAttributable
{
    private readonly ILogger _logger;

    protected ViewModelPage(object viewModel, ILogger logger)
    {
        BindingContext = viewModel;
        _logger = logger;
    }

    /// <summary>Forwards Shell navigation parameters to ViewModels that want them.</summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (BindingContext is IQueryReceiver receiver)
        {
            receiver.ApplyQuery(new Dictionary<string, object>(query));
        }
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
                LogAppearingFailed(_logger, ex, GetType().Name);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "OnAppearingAsync failed on {Page}")]
    private static partial void LogAppearingFailed(ILogger logger, Exception exception, string page);
}