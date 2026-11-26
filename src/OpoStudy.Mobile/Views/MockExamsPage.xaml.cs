using Microsoft.Extensions.Logging;

using OpoStudy.Mobile.Core.ViewModels;

namespace OpoStudy.Mobile.Views;

public partial class MockExamsPage : ViewModelPage
{
    public MockExamsPage(MockExamsViewModel viewModel, ILogger<MockExamsPage> logger) : base(viewModel, logger)
    {
        InitializeComponent();
    }
}