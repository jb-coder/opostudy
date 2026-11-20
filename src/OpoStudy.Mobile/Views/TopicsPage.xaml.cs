using Microsoft.Extensions.Logging;

using OpoStudy.Mobile.Core.ViewModels;

namespace OpoStudy.Mobile.Views;

public partial class TopicsPage : ViewModelPage
{
    public TopicsPage(TopicsViewModel viewModel, ILogger<TopicsPage> logger) : base(viewModel, logger)
    {
        InitializeComponent();
    }
}
