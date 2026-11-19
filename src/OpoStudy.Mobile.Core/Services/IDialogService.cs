namespace OpoStudy.Mobile.Core.Services;

/// <summary>Platform-independent dialogs.</summary>
public interface IDialogService
{
    Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);

    Task AlertAsync(string title, string message);
}