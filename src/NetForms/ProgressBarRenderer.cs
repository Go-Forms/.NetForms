using System.Drawing;

namespace System.Windows.Forms;

/// <summary>
/// Draws the track and the fill of a progress bar the way <see cref="ProgressBar"/> does in NetForms' theme. The
/// members are WinForms'; <see cref="IsSupported"/> is always true. The theme's bar is one continuous fill, so the
/// "chunks" are drawn as that fill, with <see cref="ChunkSpaceThickness"/> zero.
/// </summary>
public static class ProgressBarRenderer
{
    public static bool IsSupported => true;

    public static void DrawHorizontalBar(Graphics g, Rectangle bounds) => DrawTrack(g, bounds);

    public static void DrawVerticalBar(Graphics g, Rectangle bounds) => DrawTrack(g, bounds);

    public static void DrawHorizontalChunks(Graphics g, Rectangle bounds) => DrawFill(g, bounds);

    public static void DrawVerticalChunks(Graphics g, Rectangle bounds) => DrawFill(g, bounds);

    /// <summary>The width of a chunk: the theme's fill is continuous, so any width tiles it.</summary>
    public static int ChunkThickness => 8;

    public static int ChunkSpaceThickness => 0;

    private static void DrawTrack(Graphics g, Rectangle bounds)
    {
        ArgumentNullException.ThrowIfNull(g);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        using (var track = new SolidBrush(Theme.ProgressTrack)) g.FillRectangle(track, bounds);
        using var border = new Pen(Theme.ProgressTrackBorder);
        g.DrawRectangle(border, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
    }

    private static void DrawFill(Graphics g, Rectangle bounds)
    {
        ArgumentNullException.ThrowIfNull(g);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        using var fill = new SolidBrush(Theme.ProgressFill);
        g.FillRectangle(fill, bounds);
    }
}
