using Microsoft.Extensions.Logging;

using OpoStudy.Mobile.Core.ViewModels;

namespace OpoStudy.Mobile.Views;

public partial class GradingPage : ViewModelPage
{
    public GradingPage(GradingViewModel viewModel, ILogger<GradingPage> logger) : base(viewModel, logger)
    {
        InitializeComponent();
    }
}