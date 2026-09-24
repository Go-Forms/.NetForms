using System;
using System.IO;
using SkiaSharp;

namespace System.Drawing;

/// <summary>An icon: a bitmap with a nominal size, decoded from .ico/.png files or drawn by <see cref="SystemIcons"/>.</summary>
[System.ComponentModel.TypeConverter(typeof(IconConverter))]
public sealed class Icon : ICloneable, IDisposable
{
    private readonly Bitmap _bitmap;

    public Icon(string fileName) : this(fileName, new Size(32, 32)) { }

    public Icon(string fileName, int width, int height) : this(fileName, new Size(width, height)) { }

    public Icon(string fileName, Size size)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        using var stream = File.OpenRead(WindowsPath.ForReading(fileName));
        _bitmap = Decode(stream, size);
    }

    public Icon(Stream stream) : this(stream, new Size(32, 32)) { }

    public Icon(Stream stream, int width, int height) : this(stream, new Size(width, height)) { }

    public Icon(Stream stream, Size size)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _bitmap = Decode(stream, size);
    }

    public Icon(Icon original, Size size) : this(original, size.Width, size.Height) { }

    public Icon(Icon original, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(original);
        _bitmap = new Bitmap(original._bitmap, width, height);
    }

    internal Icon(Bitmap bitmap) => _bitmap = bitmap;

    internal Bitmap Bitmap => _bitmap;

    private static Bitmap Decode(Stream stream, Size size)
    {
        var decoded = SKBitmap.Decode(stream) ?? throw new ArgumentException("Not an icon or image stream.");
        if (size.Width > 0 && size.Height > 0 && (decoded.Width != size.Width || decoded.Height != size.Height))
        {
            var resized = decoded.Resize(new SKImageInfo(size.Width, size.Height, SKColorType.Bgra8888, SKAlphaType.Premul), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
            decoded.Dispose();
            decoded = resized;
        }
        return new Bitmap(decoded);
    }

    public int Width => _bitmap.Width;
    public int Height => _bitmap.Height;
    public Size Size => _bitmap.Size;
    public IntPtr Handle => IntPtr.Zero;

    public Bitmap ToBitmap() => (Bitmap)_bitmap.Clone();

    public static Icon? ExtractAssociatedIcon(string filePath) => null;

    public static Icon FromHandle(IntPtr handle) => throw new NotSupportedException("Native icon handles are not available.");

    public void Save(Stream outputStream) => _bitmap.Save(outputStream, Imaging.ImageFormat.Png);

    public object Clone() => new Icon((Bitmap)_bitmap.Clone());

    public void Dispose()
    {
        _bitmap.Dispose();
        GC.SuppressFinalize(this);
    }

    public override string ToString() => "(Icon)";
}

/// <summary>The stock icons (Information, Warning, Error, Question, ...), drawn as vector art at 32×32.</summary>
public static class SystemIcons
{
    private static Icon? s_information, s_warning, s_error, s_question, s_application, s_shield;

    public static Icon Information => s_information ??= Draw(Kind.Information);
    public static Icon Asterisk => Information;
    public static Icon Warning => s_warning ??= Draw(Kind.Warning);
    public static Icon Exclamation => Warning;
    public static Icon Error => s_error ??= Draw(Kind.Error);
    public static Icon Hand => Error;
    public static Icon Question => s_question ??= Draw(Kind.Question);
    public static Icon Application => s_application ??= Draw(Kind.Application);
    public static Icon WinLogo => Application;
    public static Icon Shield => s_shield ??= Draw(Kind.Shield);

    /// <summary>
    /// A shell stock icon. NetForms draws the dialog ones (information, warning, error, help, shield); the
    /// others come back as the application icon (decision 116), at the size asked for.
    /// </summary>
    public static Icon GetStockIcon(StockIconId stockIcon, StockIconOptions options = StockIconOptions.Default) =>
        GetStockIcon(stockIcon, (options & StockIconOptions.SmallIcon) != 0 ? 16 : 32);

    public static Icon GetStockIcon(StockIconId stockIcon, int size)
    {
        var icon = stockIcon switch
        {
            StockIconId.Info => Information,
            StockIconId.Warning => Warning,
            StockIconId.Error => Error,
            StockIconId.Help => Question,
            StockIconId.Shield => Shield,
            _ => Application,
        };
        return new Icon(icon, size, size);
    }

    private enum Kind { Information, Warning, Error, Question, Application, Shield }

    private static Icon Draw(Kind kind)
    {
        const int size = 32;
        var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias;
        using var white = new SolidBrush(Color.White);
        using var glyphFont = new Font("Segoe UI", 15f, FontStyle.Bold, GraphicsUnit.Pixel);
        var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        var box = new RectangleF(1, 1, size - 2, size - 2);

        switch (kind)
        {
            case Kind.Information:
                using (var b = new SolidBrush(Color.FromArgb(0x1E, 0x90, 0xFF))) g.FillEllipse(b, box);
                g.FillEllipse(white, 14, 7, 4, 4);
                g.FillRectangle(white, 14, 13, 4, 12);
                break;
            case Kind.Question:
                using (var b = new SolidBrush(Color.FromArgb(0x1E, 0x90, 0xFF))) g.FillEllipse(b, box);
                using (var f = new Font("Segoe UI", 20f, FontStyle.Bold, GraphicsUnit.Pixel))
                    g.DrawString("?", f, white, new RectangleF(0, 0, size, size), format);
                break;
            case Kind.Warning:
                using (var b = new SolidBrush(Color.FromArgb(0xF7, 0xB5, 0x00)))
                    g.FillPolygon(b, new[] { new PointF(16, 2), new PointF(31, 29), new PointF(1, 29) });
                using (var dark = new SolidBrush(Color.FromArgb(0x30, 0x30, 0x30)))
                {
                    g.FillRectangle(dark, 14, 10, 4, 11);
                    g.FillEllipse(dark, 14, 23, 4, 4);
                }
                break;
            case Kind.Error:
                using (var b = new SolidBrush(Color.FromArgb(0xE8, 0x11, 0x23))) g.FillEllipse(b, box);
                using (var p = new Pen(Color.White, 3.5f))
                {
                    g.DrawLine(p, 10, 10, 22, 22);
                    g.DrawLine(p, 22, 10, 10, 22);
                }
                break;
            case Kind.Shield:
                using (var b = new SolidBrush(Color.FromArgb(0x1E, 0x6F, 0xC4)))
                    g.FillClosedCurve(b, new[] { new PointF(16, 2), new PointF(30, 7), new PointF(27, 22), new PointF(16, 30), new PointF(5, 22), new PointF(2, 7) }, Drawing2D.FillMode.Alternate, 0.2f);
                using (var y = new SolidBrush(Color.FromArgb(0xF7, 0xB5, 0x00)))
                    g.FillRectangle(y, 16, 6, 11, 12);
                break;
            default:
                using (var b = new SolidBrush(Color.FromArgb(0x33, 0x99, 0xFF))) g.FillRectangle(b, 3, 3, 26, 26);
                using (var t = new SolidBrush(Color.FromArgb(0x1E, 0x6F, 0xC4))) g.FillRectangle(t, 3, 3, 26, 6);
                break;
        }
        return new Icon(bmp);
    }
}
