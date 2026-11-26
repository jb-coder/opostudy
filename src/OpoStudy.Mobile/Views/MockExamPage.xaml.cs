using Microsoft.Extensions.Logging;

using OpoStudy.Mobile.Core.ViewModels;

namespace OpoStudy.Mobile.Views;

public partial class MockExamPage : ViewModelPage
{
    public MockExamPage(MockExamViewModel viewModel, ILogger<MockExamPage> logger) : base(viewModel, logger)
    {
        InitializeComponent();

        // Set in code for the same reason as in QuizPage: the BindingContext is assigned before InitializeComponent.
        Shell.SetBackButtonBehavior(this, new BackButtonBehavior { Command = viewModel.ExitCommand });
    }

    /// <summary>Android back button: same confirmation as the toolbar back button.</summary>
    protected override bool OnBackButtonPressed()
    {
        if (BindingContext is MockExamViewModel { ExitCommand: { } exit })
        {
            exit.Execute(null);
        }

        return true;
    }
}