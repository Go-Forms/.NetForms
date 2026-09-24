using System;
using System.Drawing;

namespace System.Windows.Forms;

public partial class Control
{
    /// <summary>
    /// Paint this control and its children onto <paramref name="g"/>, whose origin is this
    /// control's top-left and whose clip is already limited to its bounds. The order is the
    /// one WM_PAINT produces: background, <see cref="OnPaint"/>, then children back to front.
    /// </summary>
    internal void PaintTree(Graphics g, Rectangle clip)
    {
        // A control that owns a frame (an MDI child) paints it first, in its own bounds
        // coordinates, and everything below happens inside the client area.
        var origin = ClientOrigin;
        if (!origin.IsEmpty || HasNonClientArea)
        {
            var state = g.Save();
            OnPaintNonClient(g);
            g.Restore(state);

            var canvas = g.Canvas;
            int saved = canvas.Save();
            var client = ClientSizeCore;
            canvas.ClipRect(new SkiaSharp.SKRect(origin.X, origin.Y, origin.X + client.Width, origin.Y + client.Height),
                SkiaSharp.SKClipOperation.Intersect, false);
            canvas.Translate(origin.X, origin.Y);
            using (var clientGraphics = Graphics.FromCanvas(canvas))
            {
                clip.Offset(-origin.X, -origin.Y);
                PaintClientArea(clientGraphics, clip);
            }
            canvas.RestoreToCount(saved);
            return;
        }
        PaintClientArea(g, clip);
    }

    /// <summary>True when the control draws something outside its client area (an MDI child's caption).</summary>
    internal virtual bool HasNonClientArea => false;

    /// <summary>Paints the frame around the client area, in the control's own bounds coordinates.</summary>
    internal virtual void OnPaintNonClient(Graphics g) { }

    private void PaintClientArea(Graphics g, Rectangle clip)
    {
        var client = ClientRectangle;
        clip.Intersect(client);
        if (clip.IsEmpty) return;

        using (var e = new PaintEventArgs(g, clip))
        {
            var state = g.Save();
            OnPaintBackground(e);
            g.Restore(state);

            state = g.Save();
            OnPaint(e);
            g.Restore(state);
        }

        if (_controls != null)
        {
            for (int i = _controls.Count - 1; i >= 0; i--) PaintChild(g, _controls[i], clip);
        }
        // Adornments sit above every child, as the windows they stand for would.
        if (_adornments != null)
        {
            foreach (var adornment in _adornments) PaintChild(g, adornment, clip);
        }
        OnPaintOverlay(g);
    }

    private static void PaintChild(Graphics g, Control child, Rectangle clip)
    {
        if (!child._visible || child._width <= 0 || child._height <= 0) return;
        var childRect = child.Bounds;
        if (!childRect.IntersectsWith(clip)) return;

        var childClip = clip;
        childClip.Intersect(childRect);
        childClip.Offset(-child._x, -child._y);

        // Each child paints through its own Graphics whose base state is its own clip
        // and origin, so ResetClip/ResetTransform inside OnPaint cannot escape it.
        var canvas = g.Canvas;
        int saved = canvas.Save();
        canvas.ClipRect(new SkiaSharp.SKRect(childRect.Left, childRect.Top, childRect.Right, childRect.Bottom), SkiaSharp.SKClipOperation.Intersect, false);
        canvas.Translate(child._x, child._y);
        using (var childGraphics = Graphics.FromCanvas(canvas))
        {
            child.PaintTree(childGraphics, childClip);
        }
        canvas.RestoreToCount(saved);
    }

    /// <summary>Render the control (and its children) into <paramref name="bitmap"/> without a window.</summary>
    public void DrawToBitmap(Bitmap bitmap, Rectangle targetBounds)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        if (targetBounds.Width <= 0 || targetBounds.Height <= 0 || targetBounds.X < 0 || targetBounds.Y < 0)
            throw new ArgumentException("Invalid target bounds.", nameof(targetBounds));

        // Make sure pending layout has run, as showing the control would.
        if (_layoutDeferred && _layoutSuspendCount == 0) PerformLayout();

        using var canvas = new SkiaSharp.SKCanvas(bitmap.Skia);
        canvas.ClipRect(new SkiaSharp.SKRect(targetBounds.Left, targetBounds.Top, targetBounds.Right, targetBounds.Bottom), SkiaSharp.SKClipOperation.Intersect, false);
        canvas.Translate(targetBounds.X, targetBounds.Y);
        using var g = Graphics.FromCanvas(canvas);
        PaintTree(g, new Rectangle(0, 0, targetBounds.Width, targetBounds.Height));
    }
}
