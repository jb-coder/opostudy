using Microsoft.Maui.Handlers;

namespace OpoStudy.Mobile;

/// <summary>
/// MaterialButton reserves 6 dp of inset above and below, so a button with a 48 dp minimum height is drawn and reported at about 36 dp
/// (touch target below the 48 dp of the design system). Removing the insets makes the minimum height the real size.
/// </summary>
internal static class ButtonInsets
{
    public static void Register() =>
        ButtonHandler.Mapper.AppendToMapping("ZeroInsets", (handler, _) =>
        {
            handler.PlatformView.InsetTop = 0;
            handler.PlatformView.InsetBottom = 0;
        });
}
