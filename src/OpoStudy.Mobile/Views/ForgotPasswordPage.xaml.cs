using Microsoft.Extensions.Logging;

using OpoStudy.Mobile.Core.ViewModels;

namespace OpoStudy.Mobile.Views;

public partial class ForgotPasswordPage : ViewModelPage
{
    public ForgotPasswordPage(ForgotPasswordViewModel viewModel, ILogger<ForgotPasswordPage> logger) : base(viewModel, logger)
    {
        InitializeComponent();
    }
}