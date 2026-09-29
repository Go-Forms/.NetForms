using SkiaSharp;

namespace System.Drawing.Imaging;

/// <summary>
/// An image file format, identified by its GDI+ <see cref="Guid"/> as in System.Drawing. Saving: PNG, JPEG and WEBP are
/// Skia's encoders; BMP, TIFF and GIF are NetForms' own (<see cref="ImageEncoders"/>); a format GDI+ has no encoder for
/// (Icon, EMF, WMF, Exif, HEIF) is saved as PNG, as GDI+ does.
/// </summary>
public sealed class ImageFormat
{
    private static ImageFormat Known(string name, string guid, SKEncodedImageFormat sk, int quality = 100, bool own = false) =>
        new(new Guid(guid + "-0728-11d3-9d7b-0000f81ef32e"), name, sk, quality, own);

    private ImageFormat(Guid guid, string? name, SKEncodedImageFormat sk, int quality, bool own)
    {
        Guid = guid;
        Name = name;
        SkFormat = sk;
        Quality = quality;
        OwnEncoder = own;
    }

    /// <summary>A format by its GDI+ GUID; one of the known ones when the GUID is theirs.</summary>
    public ImageFormat(Guid guid)
        : this(guid, null, SKEncodedImageFormat.Png, 100, false)
    {
        foreach (var known in s_all)
        {
            if (known.Guid != guid) continue;
            Name = known.Name;
            SkFormat = known.SkFormat;
            Quality = known.Quality;
            OwnEncoder = known.OwnEncoder;
        }
    }

    public Guid Guid { get; }

    internal string? Name { get; }
    internal SKEncodedImageFormat SkFormat { get; }
    internal int Quality { get; }

    /// <summary>Encoded by <see cref="ImageEncoders"/> (Skia has no encoder for it).</summary>
    internal bool OwnEncoder { get; }

    public static ImageFormat MemoryBmp { get; } = Known("MemoryBMP", "b96b3caa", SKEncodedImageFormat.Bmp, own: true);
    public static ImageFormat Bmp { get; } = Known("Bmp", "b96b3cab", SKEncodedImageFormat.Bmp, own: true);
    public static ImageFormat Emf { get; } = Known("Emf", "b96b3cac", SKEncodedImageFormat.Png);
    public static ImageFormat Wmf { get; } = Known("Wmf", "b96b3cad", SKEncodedImageFormat.Png);
    public static ImageFormat Jpeg { get; } = Known("Jpeg", "b96b3cae", SKEncodedImageFormat.Jpeg, 90);
    public static ImageFormat Png { get; } = Known("Png", "b96b3caf", SKEncodedImageFormat.Png);
    public static ImageFormat Gif { get; } = Known("Gif", "b96b3cb0", SKEncodedImageFormat.Gif, own: true);
    public static ImageFormat Tiff { get; } = Known("Tiff", "b96b3cb1", SKEncodedImageFormat.Png, own: true);
    public static ImageFormat Exif { get; } = Known("Exif", "b96b3cb2", SKEncodedImageFormat.Png);
    public static ImageFormat Icon { get; } = Known("Icon", "b96b3cb5", SKEncodedImageFormat.Png);
    public static ImageFormat Heif { get; } = Known("Heif", "b96b3cb6", SKEncodedImageFormat.Png);
    public static ImageFormat Webp { get; } = Known("Webp", "b96b3cb7", SKEncodedImageFormat.Webp, 90);

    private static readonly ImageFormat[] s_all = [MemoryBmp, Bmp, Emf, Wmf, Jpeg, Png, Gif, Tiff, Exif, Icon, Heif, Webp];

    public override bool Equals(object? o) => o is ImageFormat format && format.Guid == Guid;

    public override int GetHashCode() => Guid.GetHashCode();

    public override string ToString() => Name ?? $"[ImageFormat: {Guid}]";
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
