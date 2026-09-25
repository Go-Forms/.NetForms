using System.Drawing;
using System.Windows.Forms.VisualStyles;

namespace System.Windows.Forms;

/// <summary>
/// Draws a group box frame and caption the way <see cref="GroupBox"/> does, for owner-drawn controls. The frame runs
/// through the caption's vertical middle and is interrupted around it (the GroupBox geometry of this theme).
/// </summary>
public static class GroupBoxRenderer
{
    internal const int CaptionX = 8;
    internal const int CaptionGap = 5;

    /// <summary>Visual styles are always on in NetForms' theme; the property exists for code that sets it.</summary>
    public static bool RenderMatchingApplicationState { get; set; } = true;

    public static bool IsBackgroundPartiallyTransparent(GroupBoxState state) => true;

    /// <summary>Paint what the control's parent shows behind <paramref name="bounds"/> (its background colour here).</summary>
    public static void DrawParentBackground(Graphics g, Rectangle bounds, Control childControl)
    {
        ArgumentNullException.ThrowIfNull(g);
        ArgumentNullException.ThrowIfNull(childControl);
        var color = childControl.Parent?.BackColor ?? childControl.BackColor;
        using var brush = new SolidBrush(color);
        g.FillRectangle(brush, bounds);
    }

    public static void DrawGroupBox(Graphics g, Rectangle bounds, GroupBoxState state) =>
        DrawGroupBox(g, bounds, null, null, TextFormatFlags.Top | TextFormatFlags.Left, state);

    public static void DrawGroupBox(Graphics g, Rectangle bounds, string? groupBoxText, Font? font, GroupBoxState state) =>
        DrawGroupBox(g, bounds, groupBoxText, font, TextFormatFlags.Top | TextFormatFlags.Left, state);

    public static void DrawGroupBox(Graphics g, Rectangle bounds, string? groupBoxText, Font? font, Color textColor, GroupBoxState state) =>
        DrawGroupBox(g, bounds, groupBoxText, font, textColor, TextFormatFlags.Top | TextFormatFlags.Left, state);

    public static void DrawGroupBox(Graphics g, Rectangle bounds, string? groupBoxText, Font? font, TextFormatFlags flags, GroupBoxState state) =>
        DrawGroupBox(g, bounds, groupBoxText, font, state == GroupBoxState.Disabled ? Theme.DisabledText : SystemColors.ControlText, flags, state);

    public static void DrawGroupBox(Graphics g, Rectangle bounds, string? groupBoxText, Font? font, Color textColor, TextFormatFlags flags, GroupBoxState state)
    {
        ArgumentNullException.ThrowIfNull(g);
        DrawFrame(g, bounds, groupBoxText, font ?? Control.DefaultFont, textColor, Theme.GroupBoxBorder, flags);
    }

    /// <summary>The frame and caption; GroupBox paints through this too.</summary>
    internal static void DrawFrame(Graphics g, Rectangle bounds, string? text, Font font, Color textColor, Color lineColor, TextFormatFlags flags)
    {
        int w = bounds.Width, h = bounds.Height;
        if (w <= 0 || h <= 0) return;
        int x0 = bounds.X, y0 = bounds.Y;
        bool hasText = !string.IsNullOrEmpty(text);
        var textSize = hasText ? TextRenderer.MeasureText(text, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.SingleLine) : Size.Empty;
        int captionHeight = font.Height;
        int top = y0 + captionHeight / 2;

        using var pen = new Pen(lineColor);
        int gapStart = hasText ? Math.Max(0, CaptionX - CaptionGap) : w;
        int gapEnd = hasText ? Math.Min(w, CaptionX + textSize.Width + CaptionGap) : w;
        if ((flags & TextFormatFlags.Right) != 0 && hasText)
        {
            // A right-aligned caption (RightToLeft): the gap sits at the other end.
            gapStart = Math.Max(0, w - CaptionX - textSize.Width - CaptionGap);
            gapEnd = Math.Min(w, w - CaptionX + CaptionGap);
        }

        // Top line, interrupted under the caption.
        if (gapStart > 0) g.DrawLine(pen, x0, top, x0 + gapStart - 1, top);
        if (gapEnd < w) g.DrawLine(pen, x0 + gapEnd, top, x0 + w - 1, top);
        g.DrawLine(pen, x0, top, x0, y0 + h - 1);
        g.DrawLine(pen, x0 + w - 1, top, x0 + w - 1, y0 + h - 1);
        g.DrawLine(pen, x0, y0 + h - 1, x0 + w - 1, y0 + h - 1);

        if (hasText)
        {
            var textRect = (flags & TextFormatFlags.Right) != 0
                ? new Rectangle(x0, y0, Math.Max(0, w - CaptionX), captionHeight)
                : new Rectangle(x0 + CaptionX, y0, Math.Max(0, w - CaptionX), captionHeight);
            TextRenderer.DrawText(g, text, font, textRect, textColor, (flags & TextFormatFlags.Right) | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        }
    }
}
