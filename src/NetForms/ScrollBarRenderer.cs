using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms.VisualStyles;

namespace System.Windows.Forms;

/// <summary>
/// Draws the parts of a scroll bar (arrows, thumb, track, size box) the way NetForms' scroll bars look. The members
/// are WinForms'; <see cref="IsSupported"/> is always true. The theme's thumb has no grip, so the grips draw nothing.
/// </summary>
public static class ScrollBarRenderer
{
    public static bool IsSupported => true;

    public static void DrawArrowButton(Graphics g, Rectangle bounds, ScrollBarArrowButtonState state)
    {
        ArgumentNullException.ThrowIfNull(g);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        int s = (int)state;
        // 1-16: up, down, left, right x normal, hot, pressed, disabled; 17-20: the hover states of each direction.
        int direction = s <= 16 ? (s - 1) / 4 : s - 17;
        int kind = s <= 16 ? (s - 1) % 4 : 1;
        bool enabled = kind != 3;
        if (kind is 1 or 2)
        {
            using var b = new SolidBrush(kind == 2 ? Theme.ScrollThumbPressed : Theme.ScrollThumbHot);
            g.FillRectangle(b, bounds);
        }
        else
        {
            using var b = new SolidBrush(Theme.ScrollTrack);
            g.FillRectangle(b, bounds);
        }
        int cx = bounds.X + bounds.Width / 2, cy = bounds.Y + bounds.Height / 2;
        const int a = 3;
        PointF[] pts = direction switch
        {
            0 => [new(cx - a, cy + a / 2f + 1), new(cx, cy - a / 2f), new(cx + a, cy + a / 2f + 1)],
            1 => [new(cx - a, cy - a / 2f), new(cx, cy + a / 2f + 1), new(cx + a, cy - a / 2f)],
            2 => [new(cx + a / 2f + 1, cy - a), new(cx - a / 2f, cy), new(cx + a / 2f + 1, cy + a)],
            _ => [new(cx - a / 2f, cy - a), new(cx + a / 2f + 1, cy), new(cx - a / 2f, cy + a)],
        };
        var mode = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var brush = new SolidBrush(enabled ? Theme.ScrollArrow : Theme.ScrollThumbDisabled)) g.FillPolygon(brush, pts);
        g.SmoothingMode = mode;
    }

    public static void DrawHorizontalThumb(Graphics g, Rectangle bounds, ScrollBarState state) => DrawThumb(g, Rectangle.Inflate(bounds, 0, -1), state);

    public static void DrawVerticalThumb(Graphics g, Rectangle bounds, ScrollBarState state) => DrawThumb(g, Rectangle.Inflate(bounds, -1, 0), state);

    public static void DrawHorizontalThumbGrip(Graphics g, Rectangle bounds, ScrollBarState state) => ArgumentNullException.ThrowIfNull(g);

    public static void DrawVerticalThumbGrip(Graphics g, Rectangle bounds, ScrollBarState state) => ArgumentNullException.ThrowIfNull(g);

    public static void DrawRightHorizontalTrack(Graphics g, Rectangle bounds, ScrollBarState state) => DrawTrack(g, bounds, state);

    public static void DrawLeftHorizontalTrack(Graphics g, Rectangle bounds, ScrollBarState state) => DrawTrack(g, bounds, state);

    public static void DrawUpperVerticalTrack(Graphics g, Rectangle bounds, ScrollBarState state) => DrawTrack(g, bounds, state);

    public static void DrawLowerVerticalTrack(Graphics g, Rectangle bounds, ScrollBarState state) => DrawTrack(g, bounds, state);

    /// <summary>The size grip in the corner between two scroll bars: dots, as the status strip's grip.</summary>
    public static void DrawSizeBox(Graphics g, Rectangle bounds, ScrollBarSizeBoxState state)
    {
        ArgumentNullException.ThrowIfNull(g);
        using (var track = new SolidBrush(Theme.ScrollTrack)) g.FillRectangle(track, bounds);
        using var dot = new SolidBrush(Theme.SizingGrip);
        bool left = state == ScrollBarSizeBoxState.LeftAlign;
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col <= row; col++)
            {
                int x = left ? bounds.Left + 2 + col * 4 : bounds.Right - 4 - col * 4;
                int y = bounds.Bottom - 4 - (2 - row) * 4;
                g.FillRectangle(dot, x, y, 2, 2);
            }
        }
    }

    public static Size GetThumbGripSize(Graphics g, ScrollBarState state)
    {
        ArgumentNullException.ThrowIfNull(g);
        return new Size(8, 8);
    }

    public static Size GetSizeBoxSize(Graphics g, ScrollBarState state)
    {
        ArgumentNullException.ThrowIfNull(g);
        return new Size(SystemInformation.VerticalScrollBarWidth, SystemInformation.HorizontalScrollBarHeight);
    }

    private static void DrawThumb(Graphics g, Rectangle bounds, ScrollBarState state)
    {
        ArgumentNullException.ThrowIfNull(g);
        var color = state switch
        {
            ScrollBarState.Disabled => Theme.ScrollThumbDisabled,
            ScrollBarState.Pressed => Theme.ScrollThumbPressed,
            ScrollBarState.Hot => Theme.ScrollThumbHot,
            _ => Theme.ScrollThumb,
        };
        using var b = new SolidBrush(color);
        g.FillRectangle(b, bounds);
    }

    private static void DrawTrack(Graphics g, Rectangle bounds, ScrollBarState state)
    {
        ArgumentNullException.ThrowIfNull(g);
        using var b = new SolidBrush(state == ScrollBarState.Pressed ? Theme.ScrollThumbDisabled : Theme.ScrollTrack);
        g.FillRectangle(b, bounds);
    }
}
