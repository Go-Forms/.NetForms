using System.Drawing;
using System.Windows.Forms.VisualStyles;

namespace System.Windows.Forms;

/// <summary>
/// Draws a push button the way <see cref="Button"/> does in NetForms' theme, for owner-drawn and custom controls
/// (a <c>class MyButton : Control</c> that paints itself with <c>ButtonRenderer.DrawButton</c>). The members are
/// WinForms'; visual styles are always on, so the look is the theme's on both systems.
/// </summary>
public static class ButtonRenderer
{
    /// <summary>Visual styles are always on in NetForms' theme; the property exists for code that sets it.</summary>
    public static bool RenderMatchingApplicationState { get; set; } = true;

    /// <summary>The theme's button is opaque.</summary>
    public static bool IsBackgroundPartiallyTransparent(PushButtonState state) => false;

    public static void DrawParentBackground(Graphics g, Rectangle bounds, Control childControl) =>
        GroupBoxRenderer.DrawParentBackground(g, bounds, childControl);

    public static void DrawButton(Graphics g, Rectangle bounds, PushButtonState state) =>
        DrawButton(g, bounds, null, null, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine, null, Rectangle.Empty, false, state);

    public static void DrawButton(Graphics g, Rectangle bounds, bool focused, PushButtonState state) =>
        DrawButton(g, bounds, null, null, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine, null, Rectangle.Empty, focused, state);

    public static void DrawButton(Graphics g, Rectangle bounds, string? buttonText, Font? font, bool focused, PushButtonState state) =>
        DrawButton(g, bounds, buttonText, font, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine, null, Rectangle.Empty, focused, state);

    public static void DrawButton(Graphics g, Rectangle bounds, string? buttonText, Font? font, TextFormatFlags flags, bool focused, PushButtonState state) =>
        DrawButton(g, bounds, buttonText, font, flags, null, Rectangle.Empty, focused, state);

    public static void DrawButton(Graphics g, Rectangle bounds, Image image, Rectangle imageBounds, bool focused, PushButtonState state) =>
        DrawButton(g, bounds, null, null, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine, image, imageBounds, focused, state);

    public static void DrawButton(Graphics g, Rectangle bounds, string? buttonText, Font? font, Image image, Rectangle imageBounds, bool focused, PushButtonState state) =>
        DrawButton(g, bounds, buttonText, font, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine, image, imageBounds, focused, state);

    public static void DrawButton(Graphics g, Rectangle bounds, string? buttonText, Font? font, TextFormatFlags flags, Image? image, Rectangle imageBounds, bool focused, PushButtonState state)
    {
        ArgumentNullException.ThrowIfNull(g);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        Color face = state switch
        {
            PushButtonState.Disabled => Theme.ButtonFaceDisabled,
            PushButtonState.Pressed => Theme.ButtonFacePressed,
            PushButtonState.Hot => Theme.ButtonFaceHot,
            _ => Theme.ButtonFace,
        };
        Color border = state switch
        {
            PushButtonState.Disabled => Theme.ButtonBorderDisabled,
            PushButtonState.Pressed => Theme.ButtonBorderPressed,
            PushButtonState.Hot => Theme.ButtonBorderHot,
            PushButtonState.Default => Theme.ButtonBorderDefault,
            _ => Theme.ButtonBorder,
        };
        using (var b = new SolidBrush(face)) g.FillRectangle(b, bounds);
        using (var p = new Pen(border)) g.DrawRectangle(p, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
        if (state == PushButtonState.Default)
        {
            // The default button's second frame, as Button draws it for AcceptButton.
            using var p = new Pen(border);
            g.DrawRectangle(p, bounds.X + 1, bounds.Y + 1, bounds.Width - 3, bounds.Height - 3);
        }

        if (image != null) g.DrawImage(image, imageBounds);
        if (!string.IsNullOrEmpty(buttonText))
        {
            var content = Rectangle.Inflate(bounds, -3, -3);
            TextRenderer.DrawText(g, buttonText, font ?? Control.DefaultFont, content,
                state == PushButtonState.Disabled ? Theme.DisabledText : SystemColors.ControlText, flags);
        }
        if (focused)
        {
            ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(bounds, -3, -3));
        }
    }
}
