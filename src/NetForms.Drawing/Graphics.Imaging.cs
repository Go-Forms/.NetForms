using System;
using System.Drawing.Imaging;
using SkiaSharp;

namespace System.Drawing;

/// <summary>DrawImage with ImageAttributes (a color matrix, gamma, color key…) and icons (Ф6.К).</summary>
public sealed partial class Graphics
{
    public delegate bool DrawImageAbort(IntPtr callbackdata);

    public void DrawImage(Image image, Rectangle destRect, int srcX, int srcY, int srcWidth, int srcHeight, GraphicsUnit srcUnit, ImageAttributes? imageAttr) =>
        DrawImage(image, destRect, (float)srcX, srcY, srcWidth, srcHeight, srcUnit, imageAttr, null, IntPtr.Zero);

    public void DrawImage(Image image, Rectangle destRect, int srcX, int srcY, int srcWidth, int srcHeight, GraphicsUnit srcUnit, ImageAttributes? imageAttr, DrawImageAbort? callback) =>
        DrawImage(image, destRect, (float)srcX, srcY, srcWidth, srcHeight, srcUnit, imageAttr, callback, IntPtr.Zero);

    public void DrawImage(Image image, Rectangle destRect, int srcX, int srcY, int srcWidth, int srcHeight, GraphicsUnit srcUnit, ImageAttributes? imageAttrs, DrawImageAbort? callback, IntPtr callbackData) =>
        DrawImage(image, destRect, (float)srcX, srcY, srcWidth, srcHeight, srcUnit, imageAttrs, callback, callbackData);

    public void DrawImage(Image image, Rectangle destRect, float srcX, float srcY, float srcWidth, float srcHeight, GraphicsUnit srcUnit, ImageAttributes? imageAttrs) =>
        DrawImage(image, destRect, srcX, srcY, srcWidth, srcHeight, srcUnit, imageAttrs, null, IntPtr.Zero);

    public void DrawImage(Image image, Rectangle destRect, float srcX, float srcY, float srcWidth, float srcHeight, GraphicsUnit srcUnit, ImageAttributes? imageAttrs, DrawImageAbort? callback) =>
        DrawImage(image, destRect, srcX, srcY, srcWidth, srcHeight, srcUnit, imageAttrs, callback, IntPtr.Zero);

    public void DrawImage(Image image, Rectangle destRect, float srcX, float srcY, float srcWidth, float srcHeight, GraphicsUnit srcUnit, ImageAttributes? imageAttrs, DrawImageAbort? callback, IntPtr callbackData)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (callback?.Invoke(callbackData) == true) return; // GDI+ asks before drawing whether to abort
        DrawImageCore(image, destRect, new RectangleF(srcX, srcY, srcWidth, srcHeight), imageAttrs);
    }

    public void DrawImage(Image image, Point[] destPoints, Rectangle srcRect, GraphicsUnit srcUnit) => DrawImage(image, destPoints, srcRect, srcUnit, null);

    public void DrawImage(Image image, Point[] destPoints, Rectangle srcRect, GraphicsUnit srcUnit, ImageAttributes? imageAttr) =>
        DrawImage(image, Drawing2D.GraphicsPath.ToPointF(destPoints), srcRect, srcUnit, imageAttr);

    public void DrawImage(Image image, Point[] destPoints, Rectangle srcRect, GraphicsUnit srcUnit, ImageAttributes? imageAttr, DrawImageAbort? callback) =>
        DrawImage(image, destPoints, srcRect, srcUnit, imageAttr);

    public void DrawImage(Image image, Point[] destPoints, Rectangle srcRect, GraphicsUnit srcUnit, ImageAttributes? imageAttr, DrawImageAbort? callback, int callbackData) =>
        DrawImage(image, destPoints, srcRect, srcUnit, imageAttr);

    public void DrawImage(Image image, PointF[] destPoints, RectangleF srcRect, GraphicsUnit srcUnit) => DrawImage(image, destPoints, srcRect, srcUnit, null);

    public void DrawImage(Image image, PointF[] destPoints, RectangleF srcRect, GraphicsUnit srcUnit, ImageAttributes? imageAttr)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(destPoints);
        if (destPoints.Length != 3) throw new ArgumentException("Three points are required.", nameof(destPoints));
        using var m = new Drawing2D.Matrix(new RectangleF(0, 0, srcRect.Width, srcRect.Height), destPoints);
        Canvas.Save();
        Canvas.Concat(m.Skia);
        DrawImageCore(image, new RectangleF(0, 0, srcRect.Width, srcRect.Height), srcRect, imageAttr);
        Canvas.Restore();
    }

    public void DrawImage(Image image, PointF[] destPoints, RectangleF srcRect, GraphicsUnit srcUnit, ImageAttributes? imageAttr, DrawImageAbort? callback) =>
        DrawImage(image, destPoints, srcRect, srcUnit, imageAttr);

    public void DrawImage(Image image, PointF[] destPoints, RectangleF srcRect, GraphicsUnit srcUnit, ImageAttributes? imageAttr, DrawImageAbort? callback, int callbackData) =>
        DrawImage(image, destPoints, srcRect, srcUnit, imageAttr);

    private void DrawImageCore(Image image, RectangleF destRect, RectangleF srcRect, ImageAttributes? attributes)
    {
        var bmp = image.SkBitmap ?? throw new ObjectDisposedException(nameof(image));
        SKColorFilter? filter = null;
        var source = attributes?.Apply(bmp, out filter) ?? bmp;
        try
        {
            using var img = SKImage.FromBitmap(source);
            var sampling = InterpolationMode is Drawing2D.InterpolationMode.NearestNeighbor
                ? new SKSamplingOptions(SKFilterMode.Nearest)
                : new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
            _paint.Reset();
            _paint.ColorFilter = filter;
            Canvas.DrawImage(img, SkiaConvert.ToSK(srcRect), SkiaConvert.ToSK(destRect), sampling, _paint);
            _paint.ColorFilter = null;
        }
        finally
        {
            filter?.Dispose();
            if (!ReferenceEquals(source, bmp)) source.Dispose();
        }
    }

    public void DrawIcon(Icon icon, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(icon);
        DrawImage(icon.Bitmap, x, y, icon.Width, icon.Height);
    }

    public void DrawIcon(Icon icon, Rectangle targetRect)
    {
        ArgumentNullException.ThrowIfNull(icon);
        DrawImage(icon.Bitmap, targetRect);
    }

    public void DrawIconUnstretched(Icon icon, Rectangle targetRect)
    {
        ArgumentNullException.ThrowIfNull(icon);
        var state = Canvas.Save();
        Canvas.ClipRect(SkiaConvert.ToSK((RectangleF)targetRect));
        DrawImage(icon.Bitmap, targetRect.X, targetRect.Y, icon.Width, icon.Height);
        Canvas.RestoreToCount(state);
    }
}
