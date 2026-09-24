using System;
using System.Collections.Generic;
using SkiaSharp;

namespace System.Drawing;

/// <summary>
/// Line breaking, measuring and drawing of text with a <see cref="Font"/>. Shared by
/// <see cref="Graphics.DrawString(string, Font, Brush, RectangleF, StringFormat)"/> and
/// <c>TextRenderer</c>; the two differ only in the padding they add around the result.
/// </summary>
internal static class TextLayout
{
    internal readonly struct Line
    {
        public Line(string text, float width) { Text = text; Width = width; }
        public readonly string Text;
        public readonly float Width;
    }

    internal enum Overflow { None, Ellipsis }

    public static float LineHeight(Font font)
    {
        var m = font.SKFont.Metrics;
        return m.Descent - m.Ascent + m.Leading;
    }

    /// <summary>
    /// GDI's tmHeight: ascent + descent, without the external leading that <see cref="LineHeight"/>
    /// (and so <c>Font.Height</c>) includes. This is the line spacing DrawText/GetTextExtentPoint32
    /// use, and therefore what <c>TextRenderer.MeasureText</c> has to report.
    /// </summary>
    public static float CellHeight(Font font)
    {
        var m = font.SKFont.Metrics;
        return m.Descent - m.Ascent;
    }

    /// <summary>The extent GDI would report: the widest line, and one <see cref="CellHeight"/> per line.</summary>
    public static SizeF MeasureGdi(string text, Font font, float maxWidth, bool wordWrap)
    {
        var lines = Break(text, font, maxWidth, wordWrap);
        float width = 0;
        foreach (var line in lines) width = Math.Max(width, line.Width);
        return new SizeF(width, Math.Max(1, lines.Count) * CellHeight(font));
    }

    public static float MeasureWidth(Font font, string s) => s.Length == 0 ? 0 : font.SKFont.MeasureText(s);

    /// <summary>Break <paramref name="text"/> into lines. <paramref name="maxWidth"/> ≤ 0 means no wrapping.</summary>
    public static List<Line> Break(string text, Font font, float maxWidth, bool wordWrap)
    {
        var lines = new List<Line>();
        if (string.IsNullOrEmpty(text))
        {
            return lines;
        }

        var skFont = font.SKFont;
        foreach (var para in text.Replace("\r\n", "\n").Split('\n'))
        {
            if (!wordWrap || maxWidth <= 0 || para.Length == 0)
            {
                lines.Add(new Line(para, para.Length == 0 ? 0 : skFont.MeasureText(para)));
                continue;
            }

            // Greedy word wrap: words separated by spaces; a word wider than the
            // line is broken by character, like GDI+.
            int pos = 0;
            while (pos < para.Length)
            {
                int end = pos;
                int lastBreak = -1;
                float width = 0;
                while (end < para.Length)
                {
                    int next = end + 1;
                    float w = skFont.MeasureText(para.AsSpan(pos, next - pos).ToString());
                    if (w > maxWidth && next - pos > 1)
                    {
                        break;
                    }
                    width = w;
                    if (para[end] == ' ') lastBreak = next;
                    end = next;
                }

                if (end < para.Length && lastBreak > pos)
                {
                    end = lastBreak;
                }

                var lineText = para.Substring(pos, end - pos).TrimEnd(' ');
                lines.Add(new Line(lineText, lineText.Length == 0 ? 0 : skFont.MeasureText(lineText)));
                pos = end;
                while (pos < para.Length && para[pos] == ' ') pos++;
            }
        }
        return lines;
    }

    public static SizeF Measure(string text, Font font, float maxWidth, bool wordWrap)
    {
        if (string.IsNullOrEmpty(text)) return new SizeF(0, LineHeight(font));
        var lines = Break(text, font, maxWidth, wordWrap);
        float w = 0;
        foreach (var l in lines) w = Math.Max(w, l.Width);
        return new SizeF(w, lines.Count * LineHeight(font));
    }

    public static string Ellipsize(string line, Font font, float maxWidth)
    {
        const string ellipsis = "…";
        var skFont = font.SKFont;
        if (skFont.MeasureText(line) <= maxWidth) return line;
        float ew = skFont.MeasureText(ellipsis);
        int n = line.Length;
        while (n > 0 && skFont.MeasureText(line.Substring(0, n)) + ew > maxWidth) n--;
        return line.Substring(0, n) + ellipsis;
    }

    /// <summary>Draw text inside <paramref name="bounds"/> with the given alignment.</summary>
    public static void Draw(SKCanvas canvas, string text, Font font, SKColor color, RectangleF bounds,
        StringAlignment horizontal, StringAlignment vertical, bool wordWrap, bool clip, Overflow overflow)
    {
        if (string.IsNullOrEmpty(text)) return;

        var skFont = font.SKFont;
        float lineHeight = LineHeight(font);
        var lines = Break(text, font, wordWrap ? bounds.Width : 0, wordWrap);

        float totalHeight = lines.Count * lineHeight;
        float y0 = vertical switch
        {
            StringAlignment.Center => bounds.Top + (bounds.Height - totalHeight) / 2f,
            StringAlignment.Far => bounds.Bottom - totalHeight,
            _ => bounds.Top,
        };

        using var paint = new SKPaint { Color = color, IsAntialias = true };
        var metrics = skFont.Metrics;

        int saved = -1;
        if (clip)
        {
            saved = canvas.Save();
            canvas.ClipRect(SkiaConvert.ToSK(bounds));
        }

        float y = y0;
        foreach (var raw in lines)
        {
            var line = raw;
            if (overflow == Overflow.Ellipsis && line.Width > bounds.Width && bounds.Width > 0)
            {
                var s = Ellipsize(line.Text, font, bounds.Width);
                line = new Line(s, skFont.MeasureText(s));
            }

            float x = horizontal switch
            {
                StringAlignment.Center => bounds.Left + (bounds.Width - line.Width) / 2f,
                StringAlignment.Far => bounds.Right - line.Width,
                _ => bounds.Left,
            };
            float baseline = y - metrics.Ascent;
            if (line.Text.Length > 0)
            {
                if (HermeticRendering.OutlineText)
                {
                    using var outline = skFont.GetTextPath(line.Text, new SKPoint(x, baseline));
                    canvas.DrawPath(outline, paint);
                }
                else
                {
                    canvas.DrawText(line.Text, x, baseline, SKTextAlign.Left, skFont, paint);
                }
                if (font.Underline)
                {
                    float uy = baseline + (metrics.UnderlinePosition ?? 1f);
                    float th = metrics.UnderlineThickness ?? 1f;
                    canvas.DrawRect(x, uy, line.Width, Math.Max(1f, th), paint);
                }
                if (font.Strikeout)
                {
                    float sy = baseline + (metrics.StrikeoutPosition ?? -metrics.XHeight / 2f);
                    float th = metrics.StrikeoutThickness ?? 1f;
                    canvas.DrawRect(x, sy, line.Width, Math.Max(1f, th), paint);
                }
            }
            y += lineHeight;
        }

        if (saved >= 0) canvas.RestoreToCount(saved);
    }
}
