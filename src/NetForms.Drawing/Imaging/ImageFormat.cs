using SkiaSharp;

namespace System.Drawing.Imaging;

public sealed class ImageFormat
{
    private ImageFormat(string name, SKEncodedImageFormat sk, int quality = 100)
    {
        Name = name;
        SkFormat = sk;
        Quality = quality;
    }

    internal string Name { get; }
    internal SKEncodedImageFormat SkFormat { get; }
    internal int Quality { get; }

    public static ImageFormat Png { get; } = new("Png", SKEncodedImageFormat.Png);
    public static ImageFormat Jpeg { get; } = new("Jpeg", SKEncodedImageFormat.Jpeg, 90);
    public static ImageFormat Bmp { get; } = new("Bmp", SKEncodedImageFormat.Bmp);
    public static ImageFormat Gif { get; } = new("Gif", SKEncodedImageFormat.Gif);
    public static ImageFormat Webp { get; } = new("Webp", SKEncodedImageFormat.Webp, 90);
    public static ImageFormat MemoryBmp => Bmp;

    public override string ToString() => Name;
}

public enum PixelFormat
{
    Indexed = 0x00010000,
    Gdi = 0x00020000,
    Alpha = 0x00040000,
    PAlpha = 0x00080000,
    Extended = 0x00100000,
    Canonical = 0x00200000,
    Undefined = 0,
    DontCare = 0,
    Format1bppIndexed = 1 | (1 << 8) | Indexed | Gdi,
    Format4bppIndexed = 2 | (4 << 8) | Indexed | Gdi,
    Format8bppIndexed = 3 | (8 << 8) | Indexed | Gdi,
    Format16bppGrayScale = 4 | (16 << 8) | Extended,
    Format16bppRgb555 = 5 | (16 << 8) | Gdi,
    Format16bppRgb565 = 6 | (16 << 8) | Gdi,
    Format16bppArgb1555 = 7 | (16 << 8) | Alpha | Gdi,
    Format24bppRgb = 8 | (24 << 8) | Gdi,
    Format32bppRgb = 9 | (32 << 8) | Gdi,
    Format32bppArgb = 10 | (32 << 8) | Alpha | Gdi | Canonical,
    Format32bppPArgb = 11 | (32 << 8) | Alpha | PAlpha | Gdi,
    Format48bppRgb = 12 | (48 << 8) | Extended,
    Format64bppArgb = 13 | (64 << 8) | Alpha | Canonical | Extended,
    Format64bppPArgb = 14 | (64 << 8) | Alpha | PAlpha | Extended,
    Max = 15,
}

public enum ImageLockMode
{
    ReadOnly = 0x0001,
    WriteOnly = 0x0002,
    ReadWrite = ReadOnly | WriteOnly,
    UserInputBuffer = 0x0004,
}

/// <summary>The pixels of a locked bitmap (<see cref="Bitmap.LockBits(Rectangle, ImageLockMode, PixelFormat)"/>).</summary>
public sealed class BitmapData
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int Stride { get; set; }
    public PixelFormat PixelFormat { get; set; }
    public IntPtr Scan0 { get; set; }
    public int Reserved { get; set; }

    internal Rectangle LockedRect { get; set; }
    internal ImageLockMode LockMode { get; set; }
    internal bool OwnsBuffer { get; set; }
}
