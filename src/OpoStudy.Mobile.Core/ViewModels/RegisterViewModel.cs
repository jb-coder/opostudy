using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.Auth;
using OpoStudy.Mobile.Core.Navigation;

namespace OpoStudy.Mobile.Core.ViewModels;

/// <summary>Account creation with email and password.</summary>
public sealed partial class RegisterViewModel(IAuthService auth, INavigationService navigation) : ViewModelBase
{
    [ObservableProperty]
    public partial string Email { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ConfirmPassword { get; set; } = string.Empty;

    protected override void OnBusyChanged() => RegisterCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task RegisterAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        if (!InputValidation.IsValidEmail(Email))
        {
            ErrorMessage = InputValidation.InvalidEmailMessage;
            return;
        }

        if (Password.Length < InputValidation.MinPasswordLength)
        {
            ErrorMessage = InputValidation.PasswordTooShortMessage;
            return;
        }

        if (Password != ConfirmPassword)
        {
            ErrorMessage = InputValidation.PasswordsDoNotMatchMessage;
            return;
        }

        IsBusy = true;
        try
        {
            await auth.SignUpWithEmailAsync(Email.Trim(), Password, cancellationToken);
            Password = string.Empty;
            ConfirmPassword = string.Empty;
            await navigation.GoToAsync(Routes.Root(Routes.Home));
        }
        catch (Exception ex)
        {
            ErrorMessage = ApiErrorMessages.From(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }
}