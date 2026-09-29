using System.Drawing;
using System.Windows.Forms.VisualStyles;

namespace System.Windows.Forms;

/// <summary>
/// Draws a check box glyph (and its text) the way <see cref="CheckBox"/> does in NetForms' theme, for owner-drawn and
/// custom controls. The members are WinForms'.
/// </summary>
public static class CheckBoxRenderer
{
    internal const int GlyphSize = 13;

    public static bool RenderMatchingApplicationState { get; set; } = true;

    public static bool IsBackgroundPartiallyTransparent(CheckBoxState state) => false;

    public static void DrawParentBackground(Graphics g, Rectangle bounds, Control childControl) =>
        GroupBoxRenderer.DrawParentBackground(g, bounds, childControl);

    public static void DrawCheckBox(Graphics g, Point glyphLocation, CheckBoxState state) =>
        DrawCheckBox(g, glyphLocation, Rectangle.Empty, null, null, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine, null, Rectangle.Empty, false, state);

    public static void DrawCheckBox(Graphics g, Point glyphLocation, Rectangle textBounds, string? checkBoxText, Font? font, bool focused, CheckBoxState state) =>
        DrawCheckBox(g, glyphLocation, textBounds, checkBoxText, font, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine, null, Rectangle.Empty, focused, state);

    public static void DrawCheckBox(Graphics g, Point glyphLocation, Rectangle textBounds, string? checkBoxText, Font? font, TextFormatFlags flags, bool focused, CheckBoxState state) =>
        DrawCheckBox(g, glyphLocation, textBounds, checkBoxText, font, flags, null, Rectangle.Empty, focused, state);

    public static void DrawCheckBox(Graphics g, Point glyphLocation, Rectangle textBounds, string? checkBoxText, Font? font, Image image, Rectangle imageBounds, bool focused, CheckBoxState state) =>
        DrawCheckBox(g, glyphLocation, textBounds, checkBoxText, font, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine, image, imageBounds, focused, state);

    public static void DrawCheckBox(Graphics g, Point glyphLocation, Rectangle textBounds, string? checkBoxText, Font? font, TextFormatFlags flags, Image? image, Rectangle imageBounds, bool focused, CheckBoxState state)
    {
        ArgumentNullException.ThrowIfNull(g);
        int s = (int)state;
        var checkState = s >= 9 ? CheckState.Indeterminate : s >= 5 ? CheckState.Checked : CheckState.Unchecked;
        int kind = (s - 1) % 4; // normal, hot, pressed, disabled
        bool enabled = kind != 3;
        CheckBox.PaintBox(g, new Rectangle(glyphLocation, new Size(GlyphSize, GlyphSize)), checkState, enabled, kind == 1, kind == 2, false, SystemColors.ControlText);
        if (image != null) g.DrawImage(image, imageBounds);
        if (!string.IsNullOrEmpty(checkBoxText))
        {
            TextRenderer.DrawText(g, checkBoxText, font ?? Control.DefaultFont, textBounds, enabled ? SystemColors.ControlText : Theme.DisabledText, flags);
        }
        if (focused) ControlPaint.DrawFocusRectangle(g, textBounds);
    }

    public static Size GetGlyphSize(Graphics g, CheckBoxState state)
    {
        ArgumentNullException.ThrowIfNull(g);
        return new Size(GlyphSize, GlyphSize);
    }
}
