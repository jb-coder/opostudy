namespace OpoStudy.Mobile.Views.Controls;

/// <summary>Banner shown while the app is offline (or syncing). The page binds <c>IsVisible</c> and <see cref="Text"/> to its ViewModel.</summary>
public partial class OfflineBanner : ContentView
{
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(OfflineBanner), string.Empty);

    public OfflineBanner()
    {
        InitializeComponent();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}
