using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace System.Windows.Forms;

public static partial class ControlPaint
{
    /// <summary>The dotted focus rectangle WinForms draws around the focused control.</summary>
    public static void DrawFocusRectangle(Graphics graphics, Rectangle rectangle) =>
        DrawFocusRectangle(graphics, rectangle, SystemColors.ControlText, SystemColors.Control);

    public static void DrawFocusRectangle(Graphics graphics, Rectangle rectangle, Color foreColor, Color backColor)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        if (rectangle.Width <= 1 || rectangle.Height <= 1) return;
        using var pen = new Pen(foreColor, 1f) { DashStyle = DashStyle.Dot };
        graphics.DrawRectangle(pen, rectangle.X, rectangle.Y, rectangle.Width - 1, rectangle.Height - 1);
    }

    public static void DrawBorder(Graphics graphics, Rectangle bounds, Color color, ButtonBorderStyle style)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        if (style == ButtonBorderStyle.None) return;
        using var pen = new Pen(color, 1f)
        {
            DashStyle = style switch
            {
                ButtonBorderStyle.Dotted => DashStyle.Dot,
                ButtonBorderStyle.Dashed => DashStyle.Dash,
                _ => DashStyle.Solid,
            },
        };
        graphics.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
    }

    public static Color Light(Color baseColor) => Light(baseColor, 0.5f);

    public static Color Light(Color baseColor, float percOfLightLight)
    {
        percOfLightLight = Math.Clamp(percOfLightLight, 0f, 1f);
        baseColor = HermeticRendering.Resolve(baseColor);
        return Color.FromArgb(baseColor.A,
            (int)(baseColor.R + (255 - baseColor.R) * percOfLightLight),
            (int)(baseColor.G + (255 - baseColor.G) * percOfLightLight),
            (int)(baseColor.B + (255 - baseColor.B) * percOfLightLight));
    }

    public static Color LightLight(Color baseColor) => Light(baseColor, 1f);

    public static Color Dark(Color baseColor) => Dark(baseColor, 0.5f);

    public static Color Dark(Color baseColor, float percOfDarkDark)
    {
        percOfDarkDark = Math.Clamp(percOfDarkDark, 0f, 1f);
        baseColor = HermeticRendering.Resolve(baseColor);
        return Color.FromArgb(baseColor.A,
            (int)(baseColor.R * (1 - percOfDarkDark)),
            (int)(baseColor.G * (1 - percOfDarkDark)),
            (int)(baseColor.B * (1 - percOfDarkDark)));
    }

    public static Color DarkDark(Color baseColor) => Dark(baseColor, 1f);

    /// <summary>
    /// Draws <paramref name="image"/> as a disabled control shows it: gray and faded toward the
    /// background (WinForms uses a color matrix; this is its effect, per pixel).
    /// </summary>
    public static void DrawImageDisabled(Graphics graphics, Image image, int x, int y, Color background)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(image);
        using var copy = new Bitmap(image);
        int back = (int)(background.GetBrightness() * 255);
        for (int py = 0; py < copy.Height; py++)
        {
            for (int px = 0; px < copy.Width; px++)
            {
                var c = copy.GetPixel(px, py);
                if (c.A == 0) continue;
                int luminance = (int)(c.R * 0.299f + c.G * 0.587f + c.B * 0.114f);
                int gray = Math.Clamp((luminance + back) / 2 + 24, 0, 255);
                copy.SetPixel(px, py, Color.FromArgb(c.A, gray, gray, gray));
            }
        }
        graphics.DrawImage(copy, x, y, image.Width, image.Height);
    }
}

public enum ButtonBorderStyle
{
    None = 0,
    Dotted = 1,
    Dashed = 2,
    Solid = 3,
    Inset = 4,
    Outset = 5,
}
