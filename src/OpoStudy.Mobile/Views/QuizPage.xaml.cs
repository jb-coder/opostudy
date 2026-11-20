using Microsoft.Extensions.Logging;

using OpoStudy.Mobile.Core.ViewModels;

namespace OpoStudy.Mobile.Views;

public partial class QuizPage : ViewModelPage
{
    public QuizPage(QuizViewModel viewModel, ILogger<QuizPage> logger) : base(viewModel, logger)
    {
        InitializeComponent();
    }
}
