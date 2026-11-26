namespace OpoStudy.Mobile.Core.Services;

/// <summary>Platform-independent dialogs.</summary>
public interface IDialogService
{
    Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);

    Task AlertAsync(string title, string message);

    /// <summary>Shows an action sheet and returns the chosen option, or <c>null</c> if it was dismissed.</summary>
    Task<string?> ChooseAsync(string title, string cancel, IReadOnlyList<string> options);

    /// <summary>Asks for a line of text and returns it, or <c>null</c> if the user cancelled. An empty answer is <c>""</c>.</summary>
    Task<string?> PromptAsync(string title, string message, string accept, string cancel, string? placeholder, int maxLength);
}