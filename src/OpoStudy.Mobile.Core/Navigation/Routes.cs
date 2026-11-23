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
    public const string Exams = "exams";
    public const string Topics = "topics";
    public const string Quiz = "quiz";
    public const string PracticalCases = "practical-cases";
    public const string PracticalCase = "practical-case";
    public const string Grading = "grading";

    /// <summary>Relative route that pops the current page.</summary>
    public const string Back = "..";

    /// <summary>Absolute route (replaces the navigation stack) to a top-level page.</summary>
    public static string Root(string route) => $"//{route}";
}