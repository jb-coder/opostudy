using Microsoft.Extensions.Logging;

using OpoStudy.Mobile.Core.ViewModels;

namespace OpoStudy.Mobile.Views;

public partial class PracticalCasePage : ViewModelPage
{
    public PracticalCasePage(PracticalCaseViewModel viewModel, ILogger<PracticalCasePage> logger) : base(viewModel, logger)
    {
        InitializeComponent();
    }
}