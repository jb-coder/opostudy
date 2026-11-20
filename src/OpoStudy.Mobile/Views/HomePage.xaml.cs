using Microsoft.Extensions.Logging;

using OpoStudy.Mobile.Core.ViewModels;

namespace OpoStudy.Mobile.Views;

public partial class HomePage : ViewModelPage
{
    public HomePage(HomeViewModel viewModel, ILogger<HomePage> logger) : base(viewModel, logger)
    {
        InitializeComponent();
    }
}