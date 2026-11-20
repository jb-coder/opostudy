using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.Auth;
using OpoStudy.Mobile.Core.Navigation;

namespace OpoStudy.Mobile.Core.ViewModels;

/// <summary>Login with email and password or with Google.</summary>
public sealed partial class LoginViewModel(IAuthService auth, INavigationService navigation) : ViewModelBase
{
    [ObservableProperty]
    public partial string Email { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    protected override void OnBusyChanged()
    {
        SignInCommand.NotifyCanExecuteChanged();
        SignInWithGoogleCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task SignInAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        if (!InputValidation.IsValidEmail(Email))
        {
            ErrorMessage = InputValidation.InvalidEmailMessage;
            return;
        }

        if (string.IsNullOrEmpty(Password))
        {
            ErrorMessage = InputValidation.PasswordRequiredMessage;
            return;
        }

        IsBusy = true;
        try
        {
            await auth.SignInWithEmailAsync(Email.Trim(), Password, cancellationToken);
            await CompleteAsync();
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

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task SignInWithGoogleAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            if (await auth.SignInWithGoogleAsync(cancellationToken))
            {
                await CompleteAsync();
            }
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

    [RelayCommand]
    private Task GoToRegisterAsync() => navigation.GoToAsync(Routes.Register);

    [RelayCommand]
    private Task GoToForgotPasswordAsync() =>
        navigation.GoToAsync(Routes.ForgotPassword, new Dictionary<string, object> { [QueryKeys.Email] = Email.Trim() });

    private async Task CompleteAsync()
    {
        Password = string.Empty;
        await navigation.GoToAsync(Routes.Root(Routes.Home));
    }
}