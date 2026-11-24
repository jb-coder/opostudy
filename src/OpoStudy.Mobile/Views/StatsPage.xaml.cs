using Microsoft.Extensions.Logging;

using OpoStudy.Mobile.Core.ViewModels;

namespace OpoStudy.Mobile.Views;

public partial class StatsPage : ViewModelPage
{
    public StatsPage(StatsViewModel viewModel, ILogger<StatsPage> logger) : base(viewModel, logger)
    {
        InitializeComponent();
    }
}