using OpoStudy.Mobile.Core.Auth;
using OpoStudy.Mobile.Core.Navigation;

namespace OpoStudy.Mobile.Core.ViewModels;

/// <summary>Splash screen: restores the stored session and routes to home or login.</summary>
public sealed class StartupViewModel(IAuthService auth, INavigationService navigation) : ViewModelBase
{
    public override async Task OnAppearingAsync()
    {
        await auth.RestoreSessionAsync();
        await navigation.GoToAsync(Routes.Root(auth.IsSignedIn ? Routes.Home : Routes.Login));
    }
}