using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms.VisualStyles;

namespace System.Windows.Forms;

/// <summary>
/// Draws a radio button glyph (and its text) the way <see cref="RadioButton"/> does in NetForms' theme, for
/// owner-drawn and custom controls. The members are WinForms'.
/// </summary>
public static class RadioButtonRenderer
{
    internal const int GlyphSize = 13;

    public static bool RenderMatchingApplicationState { get; set; } = true;

    public static bool IsBackgroundPartiallyTransparent(RadioButtonState state) => true;

    public static void DrawParentBackground(Graphics g, Rectangle bounds, Control childControl) =>
        GroupBoxRenderer.DrawParentBackground(g, bounds, childControl);

    public static void DrawRadioButton(Graphics g, Point glyphLocation, RadioButtonState state) =>
        DrawRadioButton(g, glyphLocation, Rectangle.Empty, null, null, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine, null, Rectangle.Empty, false, state);

    public static void DrawRadioButton(Graphics g, Point glyphLocation, Rectangle textBounds, string? radioButtonText, Font? font, bool focused, RadioButtonState state) =>
        DrawRadioButton(g, glyphLocation, textBounds, radioButtonText, font, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine, null, Rectangle.Empty, focused, state);

    public static void DrawRadioButton(Graphics g, Point glyphLocation, Rectangle textBounds, string? radioButtonText, Font? font, TextFormatFlags flags, bool focused, RadioButtonState state) =>
        DrawRadioButton(g, glyphLocation, textBounds, radioButtonText, font, flags, null, Rectangle.Empty, focused, state);

    public static void DrawRadioButton(Graphics g, Point glyphLocation, Rectangle textBounds, string? radioButtonText, Font? font, Image image, Rectangle imageBounds, bool focused, RadioButtonState state) =>
        DrawRadioButton(g, glyphLocation, textBounds, radioButtonText, font, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine, image, imageBounds, focused, state);

    public static void DrawRadioButton(Graphics g, Point glyphLocation, Rectangle textBounds, string? radioButtonText, Font? font, TextFormatFlags flags, Image? image, Rectangle imageBounds, bool focused, RadioButtonState state)
    {
        ArgumentNullException.ThrowIfNull(g);
        int s = (int)state;
        bool isChecked = s >= 5;
        int kind = (s - 1) % 4; // normal, hot, pressed, disabled
        bool enabled = kind != 3, hot = kind == 1, pressed = kind == 2;
        Color fill = !enabled ? Theme.ButtonFaceDisabled : pressed ? Theme.CheckFillPressed : hot ? Theme.ButtonFaceHot : Theme.CheckFill;
        Color border = !enabled ? Theme.ButtonBorderDisabled : hot || pressed ? Theme.CheckBorderHot : Theme.CheckBorder;
        var box = new Rectangle(glyphLocation, new Size(GlyphSize, GlyphSize));
        var mode = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var b = new SolidBrush(fill)) g.FillEllipse(b, box.X, box.Y, box.Width - 1, box.Height - 1);
        using (var p = new Pen(border)) g.DrawEllipse(p, box.X, box.Y, box.Width - 1, box.Height - 1);
        if (isChecked)
        {
            using var dot = new SolidBrush(enabled ? Theme.CheckMark : Theme.DisabledText);
            g.FillEllipse(dot, box.X + 3.5f, box.Y + 3.5f, box.Width - 8, box.Height - 8);
        }
        g.SmoothingMode = mode;
        if (image != null) g.DrawImage(image, imageBounds);
        if (!string.IsNullOrEmpty(radioButtonText))
        {
            TextRenderer.DrawText(g, radioButtonText, font ?? Control.DefaultFont, textBounds, enabled ? SystemColors.ControlText : Theme.DisabledText, flags);
        }
        if (focused) ControlPaint.DrawFocusRectangle(g, textBounds);
    }

    public static Size GetGlyphSize(Graphics g, RadioButtonState state)
    {
        ArgumentNullException.ThrowIfNull(g);
        return new Size(GlyphSize, GlyphSize);
    }
}
