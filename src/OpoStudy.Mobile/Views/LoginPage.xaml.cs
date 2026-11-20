using Microsoft.Extensions.Logging;

using OpoStudy.Mobile.Core.ViewModels;

namespace OpoStudy.Mobile.Views;

public partial class LoginPage : ViewModelPage
{
    public LoginPage(LoginViewModel viewModel, ILogger<LoginPage> logger) : base(viewModel, logger)
    {
        InitializeComponent();
    }
}