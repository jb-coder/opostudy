using Microsoft.Extensions.Logging;

using OpoStudy.Mobile.Core.ViewModels;

namespace OpoStudy.Mobile.Views;

public partial class PracticalCasesPage : ViewModelPage
{
    public PracticalCasesPage(PracticalCasesViewModel viewModel, ILogger<PracticalCasesPage> logger) : base(viewModel, logger)
    {
        InitializeComponent();
    }
}