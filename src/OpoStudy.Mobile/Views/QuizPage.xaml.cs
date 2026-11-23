using Microsoft.Extensions.Logging;

using OpoStudy.Mobile.Core.ViewModels;

namespace OpoStudy.Mobile.Views;

public partial class QuizPage : ViewModelPage
{
    public QuizPage(QuizViewModel viewModel, ILogger<QuizPage> logger) : base(viewModel, logger)
    {
        InitializeComponent();

        // Set in code: a XAML binding on BackButtonBehavior.Command does not reach the ViewModel
        // when the page's BindingContext is assigned before InitializeComponent (as ViewModelPage does),
        // so the toolbar back arrow would leave without confirmation.
        Shell.SetBackButtonBehavior(this, new BackButtonBehavior { Command = viewModel.BackCommand });
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