using Microsoft.Extensions.Logging;

using OpoStudy.Mobile.Core.ViewModels;

namespace OpoStudy.Mobile.Views;

public partial class ExamsPage : ViewModelPage
{
    public ExamsPage(ExamsViewModel viewModel, ILogger<ExamsPage> logger) : base(viewModel, logger)
    {
        InitializeComponent();
    }
}
