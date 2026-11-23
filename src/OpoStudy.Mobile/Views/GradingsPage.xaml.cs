using Microsoft.Extensions.Logging;

using OpoStudy.Mobile.Core.ViewModels;

namespace OpoStudy.Mobile.Views;

public partial class GradingsPage : ViewModelPage
{
    public GradingsPage(GradingsViewModel viewModel, ILogger<GradingsPage> logger) : base(viewModel, logger)
    {
        InitializeComponent();
    }
}