using OpoStudy.Mobile.Core.Auth;
using OpoStudy.Mobile.Core.Navigation;
using OpoStudy.Mobile.Views;

namespace OpoStudy.Mobile;

public partial class AppShell : Shell
{
    public AppShell(SessionExpiredHandler sessionExpiredHandler)
    {
        InitializeComponent();

        Routing.RegisterRoute(Routes.Register, typeof(RegisterPage));
        Routing.RegisterRoute(Routes.ForgotPassword, typeof(ForgotPasswordPage));
        Routing.RegisterRoute(Routes.Exams, typeof(ExamsPage));
        Routing.RegisterRoute(Routes.Topics, typeof(TopicsPage));
        Routing.RegisterRoute(Routes.Quiz, typeof(QuizPage));
#if DEBUG
        Routing.RegisterRoute(Routes.StyleGuide, typeof(StyleGuidePage));
#endif

        sessionExpiredHandler.Start();
    }
}