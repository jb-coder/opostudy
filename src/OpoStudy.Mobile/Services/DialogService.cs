using OpoStudy.Mobile.Core.Services;

namespace OpoStudy.Mobile.Services;

/// <summary><see cref="IDialogService"/> backed by Shell alerts.</summary>
internal sealed class DialogService : IDialogService
{
    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) =>
        MainThread.InvokeOnMainThreadAsync(() => CurrentShell().DisplayAlertAsync(title, message, accept, cancel));

    public Task AlertAsync(string title, string message) =>
        MainThread.InvokeOnMainThreadAsync(() => CurrentShell().DisplayAlertAsync(title, message, "Aceptar"));

    private static Shell CurrentShell() => Shell.Current ?? throw new InvalidOperationException("Shell is not initialized.");
}