using System;
using System.IO;
using SkiaSharp;

namespace System.Drawing;

public sealed partial class Bitmap : Image
{
    public Bitmap(int width, int height) : this(width, height, Imaging.PixelFormat.Format32bppArgb) { }

    public Bitmap(int width, int height, Imaging.PixelFormat format)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        SkBitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        SkBitmap.Erase(SKColors.Transparent);
    }

    public Bitmap(Image original) : this(original, original.Size) { }

    public Bitmap(Image original, Size newSize) : this(original, newSize.Width, newSize.Height) { }

    public Bitmap(Image original, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(original);
        var src = original.SkBitmap ?? throw new ObjectDisposedException(nameof(original));
        SkBitmap = src.Resize(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
            ?? throw new ArgumentException("Cannot resize image.");
    }

    public Bitmap(string filename)
    {
        ArgumentNullException.ThrowIfNull(filename);
        SkBitmap = SKBitmap.Decode(WindowsPath.ForReading(filename)) ?? throw new ArgumentException($"Cannot decode image '{filename}'.");
    }

    public Bitmap(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        SkBitmap = SKBitmap.Decode(stream) ?? throw new ArgumentException("Cannot decode image stream.");
    }

    internal Bitmap(SKBitmap bitmap) => SkBitmap = bitmap;

    /// <summary>The backing Skia bitmap (BGRA8888, premultiplied). Owned by this Bitmap.</summary>
    internal SKBitmap Skia => SkBitmap ?? throw new ObjectDisposedException(nameof(Bitmap));

    public Color GetPixel(int x, int y)
    {
        if ((uint)x >= (uint)Width) throw new ArgumentOutOfRangeException(nameof(x));
        if ((uint)y >= (uint)Height) throw new ArgumentOutOfRangeException(nameof(y));
        return SkiaConvert.ToColor(Skia.GetPixel(x, y));
    }

    public void SetPixel(int x, int y, Color color)
    {
        if ((uint)x >= (uint)Width) throw new ArgumentOutOfRangeException(nameof(x));
        if ((uint)y >= (uint)Height) throw new ArgumentOutOfRangeException(nameof(y));
        Skia.SetPixel(x, y, SkiaConvert.ToSK(color));
    }

    public void MakeTransparent() => MakeTransparent(GetPixel(0, Height - 1));

    public void MakeTransparent(Color transparentColor)
    {
        var target = SkiaConvert.ToSK(transparentColor);
        var bmp = Skia;
        for (int y = 0; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
                if (bmp.GetPixel(x, y) == target) bmp.SetPixel(x, y, SKColors.Transparent);
    }

    public override object Clone() => new Bitmap(Skia.Copy());

    public Bitmap Clone(Rectangle rect, Imaging.PixelFormat format)
    {
        var dst = new SKBitmap(new SKImageInfo(rect.Width, rect.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
        if (!Skia.ExtractSubset(dst, new SKRectI(rect.Left, rect.Top, rect.Right, rect.Bottom)))
            throw new ArgumentException("Rectangle is outside the bitmap.", nameof(rect));
        return new Bitmap(dst.Copy());
    }
}
