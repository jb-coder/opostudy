using Microsoft.Extensions.Logging;

using OpoStudy.Mobile.Core.ViewModels;

namespace OpoStudy.Mobile.Views;

public partial class StartupPage : ViewModelPage
{
    public StartupPage(StartupViewModel viewModel, ILogger<StartupPage> logger) : base(viewModel, logger)
    {
        InitializeComponent();
    }
}