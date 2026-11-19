using Microsoft.Extensions.Logging;

using OpoStudy.Mobile.Core.ViewModels;

namespace OpoStudy.Mobile.Views;

public partial class RegisterPage : ViewModelPage
{
    public RegisterPage(RegisterViewModel viewModel, ILogger<RegisterPage> logger) : base(viewModel, logger)
    {
        InitializeComponent();
    }
}