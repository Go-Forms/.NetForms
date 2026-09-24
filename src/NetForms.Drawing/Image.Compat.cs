using System;
using System.Drawing.Imaging;
using System.IO;
using SkiaSharp;

namespace System.Drawing;

/// <summary>Image's remaining everyday surface (Ф6.К): RotateFlip, thumbnails, the pixel format helpers.</summary>
public abstract partial class Image
{
    public delegate bool GetThumbnailImageAbort();

    /// <summary>NetForms keeps every image as 32-bit ARGB (decoded formats are converted on load).</summary>
    public PixelFormat PixelFormat => PixelFormat.Format32bppArgb;

    public ImageFormat RawFormat { get; internal set; } = ImageFormat.MemoryBmp;

    public SizeF PhysicalDimension => new(Width, Height);

    public int Flags => 0x00000002; // ImageFlagsHasAlpha

    public RectangleF GetBounds(ref GraphicsUnit pageUnit)
    {
        pageUnit = GraphicsUnit.Pixel;
        return new RectangleF(0, 0, Width, Height);
    }

    public static Image FromFile(string filename, bool useEmbeddedColorManagement) => FromFile(filename);

    public static Image FromStream(Stream stream, bool useEmbeddedColorManagement) => FromStream(stream);

    public static Image FromStream(Stream stream, bool useEmbeddedColorManagement, bool validateImageData) => FromStream(stream);

    public static int GetPixelFormatSize(PixelFormat pixfmt) => ((int)pixfmt >> 8) & 0xFF;

    public static bool IsAlphaPixelFormat(PixelFormat pixfmt) => (pixfmt & PixelFormat.Alpha) != 0;

    public static bool IsExtendedPixelFormat(PixelFormat pixfmt) => (pixfmt & PixelFormat.Extended) != 0;

    public static bool IsCanonicalPixelFormat(PixelFormat pixfmt) => (pixfmt & PixelFormat.Canonical) != 0;

    public Image GetThumbnailImage(int thumbWidth, int thumbHeight, GetThumbnailImageAbort? callback, IntPtr callbackData)
    {
        if (thumbWidth == 0 && thumbHeight == 0) { thumbWidth = 120; thumbHeight = 120; }
        else if (thumbWidth == 0) thumbWidth = Math.Max(1, Width * thumbHeight / Math.Max(1, Height));
        else if (thumbHeight == 0) thumbHeight = Math.Max(1, Height * thumbWidth / Math.Max(1, Width));
        return new Bitmap(this, thumbWidth, thumbHeight);
    }

    /// <summary>Rotates by a multiple of 90° and/or flips, in place (the size swaps for 90° and 270°).</summary>
    public void RotateFlip(RotateFlipType rotateFlipType)
    {
        var src = SkBitmap ?? throw new ObjectDisposedException(nameof(Image));
        int value = (int)rotateFlipType;
        int turns = value & 3;
        bool flipX = (value & 4) != 0;
        int w = src.Width, h = src.Height;
        int nw = turns % 2 == 0 ? w : h, nh = turns % 2 == 0 ? h : w;
        var dst = new SKBitmap(new SKImageInfo(nw, nh, SKColorType.Bgra8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(dst))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.Translate(nw / 2f, nh / 2f);
            canvas.RotateDegrees(90 * turns);
            if (flipX) canvas.Scale(-1, 1);
            canvas.Translate(-w / 2f, -h / 2f);
            canvas.DrawBitmap(src, 0, 0);
        }
        SkBitmap = dst;
        src.Dispose();
    }
}
