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

        if (_paintException != null)
        {
            DrawPaintException(g, client);
        }
        else
        {
            using var e = new PaintEventArgs(g, clip);
            var state = g.Save();
            try
            {
                OnPaintBackground(e);
                g.Restore(state);
                state = g.Save();
                OnPaint(e);
                g.Restore(state);
            }
            catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException or AccessViolationException))
            {
                // WinForms (PaintWithErrorHandling): the control is drawn as a red cross from now on, and the
                // exception goes on to the application - except on a design surface, which must stay up
                // whatever a control of the user's does (decision 159): there the cross says what failed.
                g.Restore(state);
                _paintException = ex;
                DrawPaintException(g, client);
                if (!DesignMode) throw;
            }
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

    /// <summary>The exception the control's painting threw; it is drawn as a red cross ever after (WinForms).</summary>
    private Exception? _paintException;

    /// <summary>The red cross of a control whose painting failed; on a design surface with what failed written in it.</summary>
    private void DrawPaintException(Graphics g, Rectangle client)
    {
        DrawErrorPlate(g, client, DesignMode ? $"{GetType().Name}: {_paintException!.GetType().Name}: {_paintException.Message}" : null);
    }

    /// <summary>White, a red frame and a red cross (Control.PaintException in WinForms), and an optional text.</summary>
    internal static void DrawErrorPlate(Graphics g, Rectangle r, string? text)
    {
        if (r.Width <= 0 || r.Height <= 0) return;
        g.FillRectangle(Brushes.White, r);
        using var pen = new Pen(Color.Red, 2);
        g.DrawRectangle(pen, r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2);
        g.DrawLine(pen, r.Left, r.Top, r.Right, r.Bottom);
        g.DrawLine(pen, r.Left, r.Bottom, r.Right, r.Top);
        if (string.IsNullOrEmpty(text)) return;
        var inner = Rectangle.Inflate(r, -4, -4);
        TextRenderer.DrawText(g, text, DefaultFont, inner, Color.Black, Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
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
