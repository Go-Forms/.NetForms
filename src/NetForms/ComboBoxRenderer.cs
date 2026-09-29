using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms.VisualStyles;

namespace System.Windows.Forms;

/// <summary>
/// Draws the parts of a combo box (its edit area and its drop-down button) the way <see cref="ComboBox"/> does in
/// NetForms' theme. The members are WinForms'; <see cref="IsSupported"/> is always true.
/// </summary>
public static class ComboBoxRenderer
{
    public static bool IsSupported => true;

    public static void DrawTextBox(Graphics g, Rectangle bounds, ComboBoxState state) =>
        DrawTextBox(g, bounds, null, null, Rectangle.Inflate(bounds, -3, -3), TextFormatFlags.TextBoxControl, state);

    public static void DrawTextBox(Graphics g, Rectangle bounds, string? comboBoxText, Font? font, ComboBoxState state) =>
        DrawTextBox(g, bounds, comboBoxText, font, Rectangle.Inflate(bounds, -3, -3), TextFormatFlags.TextBoxControl, state);

    public static void DrawTextBox(Graphics g, Rectangle bounds, string? comboBoxText, Font? font, Rectangle textBounds, ComboBoxState state) =>
        DrawTextBox(g, bounds, comboBoxText, font, textBounds, TextFormatFlags.TextBoxControl, state);

    public static void DrawTextBox(Graphics g, Rectangle bounds, string? comboBoxText, Font? font, TextFormatFlags flags, ComboBoxState state) =>
        DrawTextBox(g, bounds, comboBoxText, font, Rectangle.Inflate(bounds, -3, -3), flags, state);

    public static void DrawTextBox(Graphics g, Rectangle bounds, string? comboBoxText, Font? font, Rectangle textBounds, TextFormatFlags flags, ComboBoxState state)
    {
        ArgumentNullException.ThrowIfNull(g);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        bool enabled = state != ComboBoxState.Disabled;
        using (var b = new SolidBrush(enabled ? SystemColors.Window : SystemColors.Control)) g.FillRectangle(b, bounds);
        Color border = state switch
        {
            ComboBoxState.Disabled => Theme.ButtonBorderDisabled,
            ComboBoxState.Hot or ComboBoxState.Pressed => Theme.WindowBorderFocused,
            _ => Theme.WindowBorder,
        };
        using (var p = new Pen(border)) g.DrawRectangle(p, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
        if (!string.IsNullOrEmpty(comboBoxText))
        {
            TextRenderer.DrawText(g, comboBoxText, font ?? Control.DefaultFont, textBounds, enabled ? SystemColors.WindowText : Theme.DisabledText, flags);
        }
    }

    public static void DrawDropDownButton(Graphics g, Rectangle bounds, ComboBoxState state)
    {
        ArgumentNullException.ThrowIfNull(g);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        bool enabled = state != ComboBoxState.Disabled;
        Color face = state switch
        {
            ComboBoxState.Pressed => Theme.ButtonFacePressed,
            ComboBoxState.Hot => Theme.ButtonFaceHot,
            _ => Color.Empty,
        };
        if (!face.IsEmpty)
        {
            using var b = new SolidBrush(face);
            g.FillRectangle(b, bounds);
        }
        int cx = bounds.X + bounds.Width / 2, cy = bounds.Y + bounds.Height / 2;
        using var pen = new Pen(enabled ? Theme.ScrollArrow : Theme.ScrollThumbDisabled, 1.5f);
        var mode = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.DrawLines(pen, new[] { new PointF(cx - 4, cy - 2), new PointF(cx, cy + 2), new PointF(cx + 4, cy - 2) });
        g.SmoothingMode = mode;
    }
}
