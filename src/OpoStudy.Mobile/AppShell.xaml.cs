using OpoStudy.Mobile.Core.Navigation;

#if DEBUG
using OpoStudy.Mobile.Views;
#endif

namespace OpoStudy.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

#if DEBUG
		Routing.RegisterRoute(Routes.StyleGuide, typeof(StyleGuidePage));
#endif
    }
}