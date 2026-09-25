using System;
using System.IO;
using SkiaSharp;

namespace System.Drawing;

[System.ComponentModel.TypeConverter(typeof(ImageConverter))]
public abstract partial class Image : ICloneable, IDisposable
{
    internal SKBitmap? SkBitmap { get; set; }

    /// <summary>
    /// The page as vector drawing, when the image is a print-preview page (the Metafile of WinForms'
    /// PreviewPrintController): PrintPreviewControl replays it at any zoom; everything else sees the bitmap.
    /// </summary>
    internal SKPicture? Picture { get; set; }

    public int Width => SkBitmap?.Width ?? 0;
    public int Height => SkBitmap?.Height ?? 0;
    public Size Size => new Size(Width, Height);
    public float HorizontalResolution { get; internal set; } = 96f;
    public float VerticalResolution { get; internal set; } = 96f;
    public object? Tag { get; set; }

    public static Image FromFile(string filename)
    {
        ArgumentNullException.ThrowIfNull(filename);
        // As GDI+: a missing file is FileNotFoundException here (new Bitmap(path) says ArgumentException).
        var path = WindowsPath.ForReading(filename);
        if (!File.Exists(path)) throw new FileNotFoundException(filename, filename);
        return new Bitmap(path);
    }

    public static Image FromStream(Stream stream) => new Bitmap(stream);

    public abstract object Clone();

    public void Save(string filename)
    {
        var ext = Path.GetExtension(filename).ToLowerInvariant();
        var fmt = ext switch
        {
            ".jpg" or ".jpeg" => Imaging.ImageFormat.Jpeg,
            ".bmp" => Imaging.ImageFormat.Bmp,
            ".gif" => Imaging.ImageFormat.Gif,
            ".webp" => Imaging.ImageFormat.Webp,
            _ => Imaging.ImageFormat.Png,
        };
        Save(filename, fmt);
    }

    public void Save(string filename, Imaging.ImageFormat format)
    {
        using var fs = File.Create(WindowsPath.ForWriting(filename));
        Save(fs, format);
    }

    public void Save(Stream stream, Imaging.ImageFormat format)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (SkBitmap == null) throw new ObjectDisposedException(nameof(Image));
        using var image = SKImage.FromBitmap(SkBitmap);
        using var data = image.Encode(format.SkFormat, format.Quality)
            ?? throw new NotSupportedException($"Cannot encode {format}.");
        data.SaveTo(stream);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        SkBitmap?.Dispose();
        SkBitmap = null;
        Picture?.Dispose();
        Picture = null;
    }

    ~Image() => Dispose(false);
}
