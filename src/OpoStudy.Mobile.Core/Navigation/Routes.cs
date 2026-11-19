namespace OpoStudy.Mobile.Core.Navigation;

/// <summary>Shell route names shared between ViewModels and the app shell.</summary>
public static class Routes
{
    public const string Startup = "startup";
    public const string Login = "login";
    public const string Register = "register";
    public const string ForgotPassword = "forgot-password";
    public const string Home = "home";
    public const string StyleGuide = "styleguide";

    /// <summary>Absolute route (replaces the navigation stack) to a top-level page.</summary>
    public static string Root(string route) => $"//{route}";
}
