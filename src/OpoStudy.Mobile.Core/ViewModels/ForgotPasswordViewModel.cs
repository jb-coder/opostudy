using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using OpoStudy.Mobile.Core.Api;
using OpoStudy.Mobile.Core.Auth;
using OpoStudy.Mobile.Core.Navigation;

namespace OpoStudy.Mobile.Core.ViewModels;

/// <summary>Sends the password reset email. The confirmation is the same whether the account exists or not.</summary>
public sealed partial class ForgotPasswordViewModel(IAuthService auth) : ViewModelBase, IQueryReceiver
{
    public const string Confirmation = "Si existe una cuenta con ese correo, te hemos enviado un enlace para restablecer la contraseña.";

    [ObservableProperty]
    public partial string Email { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotSent))]
    public partial bool IsSent { get; set; }

    public bool IsNotSent => !IsSent;

    public string ConfirmationMessage { get; } = Confirmation;

    public void ApplyQuery(IReadOnlyDictionary<string, object> query)
    {
        if (query.TryGetValue(QueryKeys.Email, out var value) && value is string email)
        {
            Email = email;
        }
    }

    protected override void OnBusyChanged() => SendCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task SendAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        if (!InputValidation.IsValidEmail(Email))
        {
            ErrorMessage = InputValidation.InvalidEmailMessage;
            return;
        }

        IsBusy = true;
        try
        {
            await auth.SendPasswordResetAsync(Email.Trim(), cancellationToken);
            IsSent = true;
        }
        catch (FirebaseAuthException ex) when (ex.Code == "EMAIL_NOT_FOUND")
        {
            // Same answer as for existing accounts: never reveal which emails are registered.
            IsSent = true;
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