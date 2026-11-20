using Microsoft.Extensions.Logging;

using OpoStudy.Mobile.Core.ViewModels;

namespace OpoStudy.Mobile.Views;

public partial class QuizPage : ViewModelPage
{
    public QuizPage(QuizViewModel viewModel, ILogger<QuizPage> logger) : base(viewModel, logger)
    {
        InitializeComponent();
    }

    /// <summary>Android back button: same confirmation as the toolbar back button.</summary>
    protected override bool OnBackButtonPressed()
    {
        if (BindingContext is QuizViewModel { BackCommand: { } back })
        {
            back.Execute(null);
        }

        return true;
    }
}
