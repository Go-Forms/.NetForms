using System;
using System.Drawing;

namespace System.Windows.Forms;

/// <summary>
/// The GDI-style text API every control uses to measure and draw its text. This is the
/// metric reference of NetForms (see docs/PLAN.md §7): the default flags add GDI's glyph
/// overhang padding on the left and right, <see cref="TextFormatFlags.NoPadding"/> removes it.
/// </summary>
public static class TextRenderer
{
    public static Size MeasureText(string? text, Font? font) => MeasureText(text, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.Default);

    public static Size MeasureText(string? text, Font? font, Size proposedSize) => MeasureText(text, font, proposedSize, TextFormatFlags.Default);

    public static Size MeasureText(IDeviceContext dc, string? text, Font? font) => MeasureText(text, font);

    public static Size MeasureText(IDeviceContext dc, string? text, Font? font, Size proposedSize) => MeasureText(text, font, proposedSize);

    public static Size MeasureText(IDeviceContext dc, string? text, Font? font, Size proposedSize, TextFormatFlags flags) => MeasureText(text, font, proposedSize, flags);

    public static Size MeasureText(string? text, Font? font, Size proposedSize, TextFormatFlags flags)
    {
        if (string.IsNullOrEmpty(text)) return Size.Empty;
        font ??= Control.DefaultFont;

        text = PrepareText(text, flags);
        bool wrap = (flags & TextFormatFlags.WordBreak) != 0 && (flags & TextFormatFlags.SingleLine) == 0;
        var (padLeft, padRight) = Padding(font, flags);
        float maxWidth = wrap && proposedSize.Width > 0 && proposedSize.Width < int.MaxValue ? proposedSize.Width - padLeft - padRight : 0;

        // GDI measures one tmHeight per line; Font.Height additionally counts external leading.
        var size = TextLayout.MeasureGdi(text, font, maxWidth, wrap);
        int w = (int)Math.Ceiling(size.Width) + padLeft + padRight;
        int h = (int)Math.Ceiling(size.Height);
        return new Size(w, h);
    }

    public static void DrawText(IDeviceContext dc, string? text, Font? font, Point pt, Color foreColor) =>
        DrawText(dc, text, font, pt, foreColor, TextFormatFlags.Default);

    public static void DrawText(IDeviceContext dc, string? text, Font? font, Point pt, Color foreColor, TextFormatFlags flags)
    {
        if (string.IsNullOrEmpty(text)) return;
        font ??= Control.DefaultFont;
        var size = MeasureText(text, font, new Size(int.MaxValue, int.MaxValue), flags | TextFormatFlags.NoClipping);
        DrawText(dc, text, font, new Rectangle(pt, size), foreColor, flags | TextFormatFlags.NoClipping);
    }

    public static void DrawText(IDeviceContext dc, string? text, Font? font, Point pt, Color foreColor, Color backColor) =>
        DrawText(dc, text, font, pt, foreColor, backColor, TextFormatFlags.Default);

    public static void DrawText(IDeviceContext dc, string? text, Font? font, Point pt, Color foreColor, Color backColor, TextFormatFlags flags)
    {
        FillBack(dc, new Rectangle(pt, MeasureText(text, font, new Size(int.MaxValue, int.MaxValue), flags)), backColor);
        DrawText(dc, text, font, pt, foreColor, flags);
    }

    public static void DrawText(IDeviceContext dc, string? text, Font? font, Rectangle bounds, Color foreColor) =>
        DrawText(dc, text, font, bounds, foreColor, TextFormatFlags.Default);

    public static void DrawText(IDeviceContext dc, string? text, Font? font, Rectangle bounds, Color foreColor, Color backColor) =>
        DrawText(dc, text, font, bounds, foreColor, backColor, TextFormatFlags.Default);

    public static void DrawText(IDeviceContext dc, string? text, Font? font, Rectangle bounds, Color foreColor, Color backColor, TextFormatFlags flags)
    {
        FillBack(dc, bounds, backColor);
        DrawText(dc, text, font, bounds, foreColor, flags);
    }

    public static void DrawText(IDeviceContext dc, string? text, Font? font, Rectangle bounds, Color foreColor, TextFormatFlags flags)
    {
        ArgumentNullException.ThrowIfNull(dc);
        if (string.IsNullOrEmpty(text)) return;
        if (dc is not Graphics g) throw new ArgumentException("Only Graphics device contexts are supported.", nameof(dc));
        font ??= Control.DefaultFont;

        text = PrepareText(text, flags);
        var (padLeft, padRight) = Padding(font, flags);

        var horizontal = (flags & TextFormatFlags.HorizontalCenter) != 0 ? StringAlignment.Center
            : (flags & TextFormatFlags.Right) != 0 ? StringAlignment.Far
            : StringAlignment.Near;
        var vertical = (flags & TextFormatFlags.VerticalCenter) != 0 ? StringAlignment.Center
            : (flags & TextFormatFlags.Bottom) != 0 ? StringAlignment.Far
            : StringAlignment.Near;
        bool wrap = (flags & TextFormatFlags.WordBreak) != 0 && (flags & TextFormatFlags.SingleLine) == 0;
        bool clip = (flags & TextFormatFlags.NoClipping) == 0;
        var overflow = (flags & (TextFormatFlags.EndEllipsis | TextFormatFlags.WordEllipsis | TextFormatFlags.PathEllipsis)) != 0
            ? TextLayout.Overflow.Ellipsis
            : TextLayout.Overflow.None;

        var inner = new RectangleF(bounds.X + padLeft, bounds.Y, Math.Max(0, bounds.Width - padLeft - padRight), bounds.Height);
        TextLayout.Draw(g.Canvas, text, font, SkiaConvert.ToSK(foreColor),
            inner, horizontal, vertical, wrap, clip, overflow);
    }

    private static void FillBack(IDeviceContext dc, Rectangle bounds, Color backColor)
    {
        if (backColor.A == 0 || dc is not Graphics g) return;
        using var b = new SolidBrush(backColor);
        g.FillRectangle(b, bounds);
    }

    /// <summary>Strip mnemonic prefixes ("&amp;File" → "File", "&amp;&amp;" → "&amp;") unless NoPrefix asks to keep them.</summary>
    private static string PrepareText(string text, TextFormatFlags flags)
    {
        if ((flags & TextFormatFlags.NoPrefix) != 0 || text.IndexOf('&') < 0) return text;
        var sb = new System.Text.StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '&')
            {
                if (i + 1 < text.Length && text[i + 1] == '&')
                {
                    sb.Append('&');
                    i++;
                }
                continue;
            }
            sb.Append(text[i]);
        }
        return sb.ToString();
    }

    /// <summary>GDI's default glyph-overhang padding: about 1/6 em on the left and 1/4 em on the right.</summary>
    private static (int left, int right) Padding(Font font, TextFormatFlags flags)
    {
        if ((flags & TextFormatFlags.NoPadding) != 0) return (0, 0);
        float em = font.SizeInPixels;
        int left = (int)Math.Ceiling(em / 6f);
        int right = (int)Math.Ceiling(em / 4f);
        if ((flags & TextFormatFlags.LeftAndRightPadding) != 0)
        {
            left = (int)Math.Ceiling(em / 3f);
            right = (int)Math.Ceiling(em / 3f);
        }
        return (left, right);
    }
}
