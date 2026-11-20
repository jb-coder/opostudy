namespace OpoStudy.Mobile.Core.Services;

/// <summary>Platform-independent dialogs.</summary>
public interface IDialogService
{
    Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);

    Task AlertAsync(string title, string message);

    /// <summary>Shows an action sheet and returns the chosen option, or <c>null</c> if it was dismissed.</summary>
    Task<string?> ChooseAsync(string title, string cancel, IReadOnlyList<string> options);
}