using System.Drawing;
using System.Drawing.Drawing2D;

namespace System.Windows.Forms;

[Flags]
public enum ButtonState
{
    Checked = 0x0400,
    Flat = 0x4000,
    Inactive = 0x0100,
    Normal = 0,
    Pushed = 0x0200,
    All = Flat | Checked | Pushed | Inactive,
}

public enum ScrollButton
{
    Down = 1,
    Left = 2,
    Right = 3,
    Up = 0,
    Min = 0,
    Max = 3,
}

[Flags]
public enum Border3DSide
{
    Left = 0x0001,
    Top = 0x0002,
    Right = 0x0004,
    Bottom = 0x0008,
    Middle = 0x0800,
    All = Left | Top | Right | Bottom | Middle,
}

public enum CaptionButton
{
    Close = 0,
    Help = 4,
    Maximize = 2,
    Minimize = 1,
    Restore = 3,
}

public enum MenuGlyph
{
    Arrow = 0,
    Checkmark = 1,
    Bullet = 2,
    Min = 0,
    Max = 2,
}

public enum FrameStyle
{
    Dashed = 0,
    Thick = 1,
}

/// <summary>
/// The owner-draw kit of WinForms (Ф6.К): buttons, check and radio boxes, scroll and combo buttons, 3D
/// borders, grips, glyphs - drawn in NetForms' theme (the control looks the same as the real one of
/// NetForms), with the signatures of WinForms. The reversible-on-screen methods (DrawReversibleFrame…)
/// draw nothing: there is no screen DC to XOR into (decision 118).
/// </summary>
public static partial class ControlPaint
{
    public static Color ContrastControlDark => SystemColors.ControlDark;

    public static void DrawButton(Graphics graphics, Rectangle rectangle, ButtonState state)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        bool pushed = (state & (ButtonState.Pushed | ButtonState.Checked)) != 0;
        bool inactive = (state & ButtonState.Inactive) != 0;
        var face = inactive ? Theme.ButtonFaceDisabled : pushed ? Theme.ButtonFacePressed : Theme.ButtonFace;
        var border = inactive ? Theme.ButtonBorderDisabled : pushed ? Theme.ButtonBorderPressed : Theme.ButtonBorder;
        using (var b = new SolidBrush(face)) graphics.FillRectangle(b, rectangle);
        using (var p = new Pen(border)) graphics.DrawRectangle(p, rectangle.X, rectangle.Y, rectangle.Width - 1, rectangle.Height - 1);
    }

    public static void DrawButton(Graphics graphics, int x, int y, int width, int height, ButtonState state) => DrawButton(graphics, new Rectangle(x, y, width, height), state);

    public static void DrawCheckBox(Graphics graphics, Rectangle rectangle, ButtonState state)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        var box = CenterSquare(rectangle);
        CheckBox.PaintBox(graphics, box, (state & ButtonState.Checked) != 0 ? CheckState.Checked : CheckState.Unchecked,
            (state & ButtonState.Inactive) == 0, false, (state & ButtonState.Pushed) != 0, (state & ButtonState.Flat) != 0, SystemColors.ControlText);
    }

    public static void DrawCheckBox(Graphics graphics, int x, int y, int width, int height, ButtonState state) => DrawCheckBox(graphics, new Rectangle(x, y, width, height), state);

    public static void DrawMixedCheckBox(Graphics graphics, Rectangle rectangle, ButtonState state)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        CheckBox.PaintBox(graphics, CenterSquare(rectangle), CheckState.Indeterminate, (state & ButtonState.Inactive) == 0, false, (state & ButtonState.Pushed) != 0, (state & ButtonState.Flat) != 0, SystemColors.ControlText);
    }

    public static void DrawMixedCheckBox(Graphics graphics, int x, int y, int width, int height, ButtonState state) => DrawMixedCheckBox(graphics, new Rectangle(x, y, width, height), state);

    public static void DrawRadioButton(Graphics graphics, Rectangle rectangle, ButtonState state)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        var box = CenterSquare(rectangle);
        bool inactive = (state & ButtonState.Inactive) != 0;
        var old = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var fill = new SolidBrush(inactive ? Theme.ButtonFaceDisabled : (state & ButtonState.Pushed) != 0 ? Theme.CheckFillPressed : Theme.CheckFill))
            graphics.FillEllipse(fill, box.X, box.Y, box.Width - 1, box.Height - 1);
        using (var pen = new Pen(inactive ? Theme.ButtonBorderDisabled : Theme.CheckBorder))
            graphics.DrawEllipse(pen, box.X, box.Y, box.Width - 1, box.Height - 1);
        if ((state & ButtonState.Checked) != 0)
        {
            int d = Math.Max(2, box.Width / 2 - 1);
            using var dot = new SolidBrush(inactive ? Theme.DisabledText : Theme.CheckMark);
            graphics.FillEllipse(dot, box.X + (box.Width - d) / 2f - 0.5f, box.Y + (box.Height - d) / 2f - 0.5f, d, d);
        }
        graphics.SmoothingMode = old;
    }

    public static void DrawRadioButton(Graphics graphics, int x, int y, int width, int height, ButtonState state) => DrawRadioButton(graphics, new Rectangle(x, y, width, height), state);

    private static Rectangle CenterSquare(Rectangle r)
    {
        int s = Math.Min(r.Width, r.Height);
        return new Rectangle(r.X + (r.Width - s) / 2, r.Y + (r.Height - s) / 2, s, s);
    }

    public static void DrawScrollButton(Graphics graphics, Rectangle rectangle, ScrollButton button, ButtonState state)
    {
        DrawButton(graphics, rectangle, state);
        DrawArrow(graphics, rectangle, button, (state & ButtonState.Inactive) == 0);
    }

    public static void DrawScrollButton(Graphics graphics, int x, int y, int width, int height, ScrollButton button, ButtonState state) =>
        DrawScrollButton(graphics, new Rectangle(x, y, width, height), button, state);

    public static void DrawComboButton(Graphics graphics, Rectangle rectangle, ButtonState state) => DrawScrollButton(graphics, rectangle, ScrollButton.Down, state);

    public static void DrawComboButton(Graphics graphics, int x, int y, int width, int height, ButtonState state) => DrawComboButton(graphics, new Rectangle(x, y, width, height), state);

    private static void DrawArrow(Graphics g, Rectangle r, ScrollButton direction, bool enabled)
    {
        int cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, s = Math.Max(2, Math.Min(r.Width, r.Height) / 4);
        Point[] points = direction switch
        {
            ScrollButton.Up => new[] { new Point(cx - s, cy + s / 2), new Point(cx + s, cy + s / 2), new Point(cx, cy - s / 2 - 1) },
            ScrollButton.Down => new[] { new Point(cx - s, cy - s / 2), new Point(cx + s, cy - s / 2), new Point(cx, cy + s / 2 + 1) },
            ScrollButton.Left => new[] { new Point(cx + s / 2, cy - s), new Point(cx + s / 2, cy + s), new Point(cx - s / 2 - 1, cy) },
            _ => new[] { new Point(cx - s / 2, cy - s), new Point(cx - s / 2, cy + s), new Point(cx + s / 2 + 1, cy) },
        };
        using var brush = new SolidBrush(enabled ? SystemColors.ControlText : SystemColors.GrayText);
        g.FillPolygon(brush, points);
    }

    public static void DrawCaptionButton(Graphics graphics, Rectangle rectangle, CaptionButton button, ButtonState state)
    {
        DrawButton(graphics, rectangle, state);
        var r = Rectangle.Inflate(rectangle, -rectangle.Width / 4, -rectangle.Height / 4);
        using var pen = new Pen((state & ButtonState.Inactive) != 0 ? SystemColors.GrayText : SystemColors.ControlText);
        switch (button)
        {
            case CaptionButton.Close:
                graphics.DrawLine(pen, r.Left, r.Top, r.Right, r.Bottom);
                graphics.DrawLine(pen, r.Right, r.Top, r.Left, r.Bottom);
                break;
            case CaptionButton.Minimize:
                graphics.DrawLine(pen, r.Left, r.Bottom, r.Right, r.Bottom);
                break;
            case CaptionButton.Maximize:
                graphics.DrawRectangle(pen, r);
                break;
            case CaptionButton.Restore:
                graphics.DrawRectangle(pen, r.X, r.Y + 2, r.Width - 2, r.Height - 2);
                graphics.DrawLine(pen, r.X + 2, r.Y, r.Right, r.Y);
                graphics.DrawLine(pen, r.Right, r.Y, r.Right, r.Bottom - 2);
                break;
            case CaptionButton.Help:
                TextRenderer.DrawText(graphics, "?", Control.DefaultFont, rectangle, pen.Color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                break;
        }
    }

    public static void DrawCaptionButton(Graphics graphics, int x, int y, int width, int height, CaptionButton button, ButtonState state) =>
        DrawCaptionButton(graphics, new Rectangle(x, y, width, height), button, state);

    public static void DrawMenuGlyph(Graphics graphics, Rectangle rectangle, MenuGlyph glyph) => DrawMenuGlyph(graphics, rectangle, glyph, SystemColors.ControlText, Color.Transparent);

    public static void DrawMenuGlyph(Graphics graphics, Rectangle rectangle, MenuGlyph glyph, Color foreColor, Color backColor)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        if (backColor.A != 0)
        {
            using var back = new SolidBrush(backColor);
            graphics.FillRectangle(back, rectangle);
        }
        using var brush = new SolidBrush(foreColor);
        var old = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        switch (glyph)
        {
            case MenuGlyph.Arrow:
                DrawArrow(graphics, rectangle, ScrollButton.Right, true);
                break;
            case MenuGlyph.Checkmark:
                using (var pen = new Pen(foreColor, 2f))
                {
                    var r = Rectangle.Inflate(rectangle, -rectangle.Width / 5, -rectangle.Height / 4);
                    graphics.DrawLines(pen, new[] { new PointF(r.Left, r.Top + r.Height * 0.55f), new PointF(r.Left + r.Width * 0.35f, r.Bottom), new PointF(r.Right, r.Top) });
                }
                break;
            case MenuGlyph.Bullet:
                int d = Math.Max(3, Math.Min(rectangle.Width, rectangle.Height) / 3);
                graphics.FillEllipse(brush, rectangle.X + (rectangle.Width - d) / 2f, rectangle.Y + (rectangle.Height - d) / 2f, d, d);
                break;
        }
        graphics.SmoothingMode = old;
    }

    public static void DrawMenuGlyph(Graphics graphics, int x, int y, int width, int height, MenuGlyph glyph) => DrawMenuGlyph(graphics, new Rectangle(x, y, width, height), glyph);

    public static void DrawMenuGlyph(Graphics graphics, int x, int y, int width, int height, MenuGlyph glyph, Color foreColor, Color backColor) =>
        DrawMenuGlyph(graphics, new Rectangle(x, y, width, height), glyph, foreColor, backColor);

    // --- borders --------------------------------------------------------------------------------------------

    public static void DrawBorder3D(Graphics graphics, Rectangle rectangle) => DrawBorder3D(graphics, rectangle, Border3DStyle.Etched, Border3DSide.All);

    public static void DrawBorder3D(Graphics graphics, Rectangle rectangle, Border3DStyle style) => DrawBorder3D(graphics, rectangle, style, Border3DSide.All);

    public static void DrawBorder3D(Graphics graphics, int x, int y, int width, int height) => DrawBorder3D(graphics, new Rectangle(x, y, width, height));

    public static void DrawBorder3D(Graphics graphics, int x, int y, int width, int height, Border3DStyle style) => DrawBorder3D(graphics, new Rectangle(x, y, width, height), style);

    public static void DrawBorder3D(Graphics graphics, int x, int y, int width, int height, Border3DStyle style, Border3DSide sides) =>
        DrawBorder3D(graphics, new Rectangle(x, y, width, height), style, sides);

    /// <summary>The Win32 DrawEdge look: outer and inner edges, raised (light top-left) or sunken (dark top-left).</summary>
    public static void DrawBorder3D(Graphics graphics, Rectangle rectangle, Border3DStyle style, Border3DSide sides)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        var r = rectangle;
        if ((style & Border3DStyle.Adjust) != 0) r.Inflate(-2, -2);
        if ((sides & Border3DSide.Middle) != 0)
        {
            using var face = new SolidBrush(SystemColors.Control);
            graphics.FillRectangle(face, r);
        }
        var light = SystemColors.ControlLightLight;
        var lightInner = SystemColors.ControlLight;
        var dark = SystemColors.ControlDarkDark;
        var darkInner = SystemColors.ControlDark;
        int s = (int)style & ~(int)Border3DStyle.Adjust;
        if (style == Border3DStyle.Flat)
        {
            Edge(graphics, r, sides, darkInner, darkInner);
            return;
        }
        // Low bits: outer edge (1 raised, 2 sunken); next bits: inner edge (4 raised, 8 sunken).
        if ((s & 1) != 0) Edge(graphics, r, sides, lightInner, dark);
        else if ((s & 2) != 0) Edge(graphics, r, sides, darkInner, light);
        var inner = Rectangle.Inflate(r, -1, -1);
        if ((s & 4) != 0) Edge(graphics, inner, sides, light, darkInner);
        else if ((s & 8) != 0) Edge(graphics, inner, sides, dark, lightInner);
    }

    private static void Edge(Graphics g, Rectangle r, Border3DSide sides, Color topLeft, Color bottomRight)
    {
        if (r.Width <= 0 || r.Height <= 0) return;
        using var tl = new Pen(topLeft);
        using var br = new Pen(bottomRight);
        if ((sides & Border3DSide.Top) != 0) g.DrawLine(tl, r.Left, r.Top, r.Right - 1, r.Top);
        if ((sides & Border3DSide.Left) != 0) g.DrawLine(tl, r.Left, r.Top, r.Left, r.Bottom - 1);
        if ((sides & Border3DSide.Bottom) != 0) g.DrawLine(br, r.Left, r.Bottom - 1, r.Right - 1, r.Bottom - 1);
        if ((sides & Border3DSide.Right) != 0) g.DrawLine(br, r.Right - 1, r.Top, r.Right - 1, r.Bottom - 1);
    }

    public static void DrawBorder(Graphics graphics, Rectangle bounds,
        Color leftColor, int leftWidth, ButtonBorderStyle leftStyle,
        Color topColor, int topWidth, ButtonBorderStyle topStyle,
        Color rightColor, int rightWidth, ButtonBorderStyle rightStyle,
        Color bottomColor, int bottomWidth, ButtonBorderStyle bottomStyle)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        void Side(Color color, int width, ButtonBorderStyle style, Func<int, (Point, Point)> line)
        {
            if (style == ButtonBorderStyle.None || width <= 0) return;
            using var pen = new Pen(color) { DashStyle = style == ButtonBorderStyle.Dotted ? DashStyle.Dot : style == ButtonBorderStyle.Dashed ? DashStyle.Dash : DashStyle.Solid };
            for (int i = 0; i < width; i++)
            {
                var (a, b) = line(i);
                if (style is ButtonBorderStyle.Inset or ButtonBorderStyle.Outset) pen.Color = i == 0 ? Dark(color) : color;
                g2(graphics, pen, a, b);
            }
        }
        static void g2(Graphics g, Pen pen, Point a, Point b) => g.DrawLine(pen, a, b);
        var r = bounds;
        Side(leftColor, leftWidth, leftStyle, i => (new Point(r.Left + i, r.Top), new Point(r.Left + i, r.Bottom - 1)));
        Side(topColor, topWidth, topStyle, i => (new Point(r.Left, r.Top + i), new Point(r.Right - 1, r.Top + i)));
        Side(rightColor, rightWidth, rightStyle, i => (new Point(r.Right - 1 - i, r.Top), new Point(r.Right - 1 - i, r.Bottom - 1)));
        Side(bottomColor, bottomWidth, bottomStyle, i => (new Point(r.Left, r.Bottom - 1 - i), new Point(r.Right - 1, r.Bottom - 1 - i)));
    }

    public static void DrawVisualStyleBorder(Graphics graphics, Rectangle bounds)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        using var pen = new Pen(Theme.ButtonBorder);
        graphics.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
    }

    public static void DrawLockedFrame(Graphics graphics, Rectangle rectangle, bool primary)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        using var pen = new Pen(primary ? Color.White : Color.Black);
        graphics.DrawRectangle(pen, rectangle.X, rectangle.Y, rectangle.Width - 1, rectangle.Height - 1);
        rectangle.Inflate(-1, -1);
        pen.Color = primary ? Color.Black : Color.White;
        graphics.DrawRectangle(pen, rectangle.X, rectangle.Y, rectangle.Width - 1, rectangle.Height - 1);
    }

    public static void DrawSelectionFrame(Graphics graphics, bool active, Rectangle outsideRect, Rectangle insideRect, Color backColor)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        using var brush = active ? (Brush)new HatchBrush(HatchStyle.Percent50, SystemColors.ControlDarkDark, backColor) : new SolidBrush(SystemColors.ControlDark);
        var region = new Region(outsideRect);
        region.Exclude(insideRect);
        graphics.FillRegion(brush, region);
    }

    public static void DrawGrabHandle(Graphics graphics, Rectangle rectangle, bool primary, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        using var fill = new SolidBrush(primary ? (enabled ? SystemColors.Window : SystemColors.Control) : (enabled ? SystemColors.ControlText : SystemColors.Control));
        using var border = new Pen(primary ? SystemColors.ControlText : SystemColors.Window);
        graphics.FillRectangle(fill, rectangle);
        graphics.DrawRectangle(border, rectangle.X, rectangle.Y, rectangle.Width - 1, rectangle.Height - 1);
    }

    public static void DrawContainerGrabHandle(Graphics graphics, Rectangle bounds)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        graphics.FillRectangle(Brushes.White, bounds);
        graphics.DrawRectangle(Pens.Black, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
        int cx = bounds.X + bounds.Width / 2, cy = bounds.Y + bounds.Height / 2;
        graphics.DrawLine(Pens.Black, cx, bounds.Y + 2, cx, bounds.Bottom - 3);
        graphics.DrawLine(Pens.Black, bounds.X + 2, cy, bounds.Right - 3, cy);
    }

    public static void DrawGrid(Graphics graphics, Rectangle area, Size pixelsBetweenDots, Color backColor)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        if (pixelsBetweenDots.Width <= 0 || pixelsBetweenDots.Height <= 0) throw new ArgumentOutOfRangeException(nameof(pixelsBetweenDots));
        var dot = backColor.GetBrightness() < 0.5 ? Color.White : Color.Black;
        using var brush = new SolidBrush(dot);
        for (int y = area.Top; y < area.Bottom; y += pixelsBetweenDots.Height)
            for (int x = area.Left; x < area.Right; x += pixelsBetweenDots.Width)
                graphics.FillRectangle(brush, x, y, 1, 1);
    }

    public static void DrawSizeGrip(Graphics graphics, Color backColor, Rectangle bounds)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        using var dark = new SolidBrush(Dark(backColor));
        using var light = new SolidBrush(LightLight(backColor));
        for (int row = 0; row < 3; row++)
            for (int col = 0; col <= row; col++)
            {
                // The staircase of dots in the corner: three on the bottom row, two above, one at the top.
                int x = bounds.Right - 4 - (row - col) * 4;
                int y = bounds.Bottom - 4 - col * 4;
                graphics.FillRectangle(light, x + 1, y + 1, 2, 2);
                graphics.FillRectangle(dark, x, y, 2, 2);
            }
    }

    public static void DrawSizeGrip(Graphics graphics, Color backColor, int x, int y, int width, int height) => DrawSizeGrip(graphics, backColor, new Rectangle(x, y, width, height));

    public static void DrawStringDisabled(Graphics graphics, string? s, Font? font, Color color, RectangleF layoutRectangle, StringFormat? format)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        if (string.IsNullOrEmpty(s) || font == null) return;
        using var light = new SolidBrush(LightLight(color));
        using var dark = new SolidBrush(Dark(color));
        var shifted = layoutRectangle;
        shifted.Offset(1, 1);
        graphics.DrawString(s, font, light, shifted, format);
        graphics.DrawString(s, font, dark, layoutRectangle, format);
    }

    public static void DrawStringDisabled(IDeviceContext dc, string? s, Font? font, Color color, Rectangle layoutRectangle, TextFormatFlags format)
    {
        ArgumentNullException.ThrowIfNull(dc);
        if (string.IsNullOrEmpty(s) || font == null) return;
        var shifted = layoutRectangle;
        shifted.Offset(1, 1);
        TextRenderer.DrawText(dc, s, font, shifted, LightLight(color), format);
        TextRenderer.DrawText(dc, s, font, layoutRectangle, Dark(color), format);
    }

    /// <summary>XOR drawing on the screen, outside any window: there is no screen DC in NetForms (decision 118), so nothing is drawn.</summary>
    public static void DrawReversibleFrame(Rectangle rectangle, Color backColor, FrameStyle style) { }

    public static void DrawReversibleLine(Point start, Point end, Color backColor) { }

    public static void FillReversibleRectangle(Rectangle rectangle, Color backColor) { }
}
