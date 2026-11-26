using OpoStudy.Mobile.Core.Services;

namespace OpoStudy.Mobile.Services;

/// <summary><see cref="IDialogService"/> backed by Shell alerts.</summary>
internal sealed class DialogService : IDialogService
{
    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) =>
        MainThread.InvokeOnMainThreadAsync(() => CurrentShell().DisplayAlertAsync(title, message, accept, cancel));

    public Task AlertAsync(string title, string message) =>
        MainThread.InvokeOnMainThreadAsync(() => CurrentShell().DisplayAlertAsync(title, message, "Aceptar"));

    public async Task<string?> ChooseAsync(string title, string cancel, IReadOnlyList<string> options)
    {
        var choice = await MainThread.InvokeOnMainThreadAsync(() => CurrentShell().DisplayActionSheetAsync(title, cancel, null, [.. options]));
        return choice == cancel ? null : choice;
    }

    public Task<string?> PromptAsync(string title, string message, string accept, string cancel, string? placeholder, int maxLength) =>
        MainThread.InvokeOnMainThreadAsync(() => CurrentShell().DisplayPromptAsync(title, message, accept, cancel, placeholder, maxLength));

    private static Shell CurrentShell() => Shell.Current ?? throw new InvalidOperationException("Shell is not initialized.");
}