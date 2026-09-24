using System;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace System.Drawing;

/// <summary>
/// Raw pixel access and the remaining constructors (Ф6.К). A NetForms bitmap keeps its pixels as Skia's
/// premultiplied BGRA; <see cref="LockBits(Rectangle, ImageLockMode, PixelFormat)"/> hands out a buffer in
/// the format asked for, laid out as GDI+ lays it out (rows padded to 4 bytes, BGR(A) byte order), and
/// <see cref="UnlockBits"/> writes it back unless the lock was read-only.
/// </summary>
public sealed partial class Bitmap
{
    public Bitmap(int width, int height, Graphics g) : this(width, height) => ArgumentNullException.ThrowIfNull(g);

    public Bitmap(Stream stream, bool useIcm) : this(stream) { }

    public Bitmap(string filename, bool useIcm) : this(filename) { }

    /// <summary>A bitmap embedded in <paramref name="type"/>'s assembly, named relative to the type's namespace.</summary>
    public Bitmap(Type type, string resource) : this(OpenResource(type, resource)) { }

    /// <summary>A bitmap over the caller's pixels. GDI+ keeps using that memory; NetForms copies it once.</summary>
    public Bitmap(int width, int height, int stride, PixelFormat format, IntPtr scan0) : this(width, height)
    {
        if (scan0 == IntPtr.Zero) return;
        var data = new BitmapData { Width = width, Height = height, Stride = stride, PixelFormat = format, Scan0 = scan0, LockedRect = new Rectangle(0, 0, width, height), LockMode = ImageLockMode.WriteOnly };
        CopyIn(data);
    }

    internal static Stream OpenResource(Type type, string resource)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(resource);
        return type.Module.Assembly.GetManifestResourceStream(type, resource)
            ?? throw new ArgumentException($"Resource '{resource}' cannot be found in class '{type.FullName}'.");
    }

    public Bitmap Clone(RectangleF rect, PixelFormat format) => Clone(Rectangle.Truncate(rect), format);

    public void SetResolution(float xDpi, float yDpi)
    {
        HorizontalResolution = xDpi;
        VerticalResolution = yDpi;
    }

    public BitmapData LockBits(Rectangle rect, ImageLockMode flags, PixelFormat format) => LockBits(rect, flags, format, new BitmapData());

    public BitmapData LockBits(Rectangle rect, ImageLockMode flags, PixelFormat format, BitmapData bitmapData)
    {
        ArgumentNullException.ThrowIfNull(bitmapData);
        if (rect.X < 0 || rect.Y < 0 || rect.Width <= 0 || rect.Height <= 0 || rect.Right > Width || rect.Bottom > Height)
            throw new ArgumentException("Parameter is not valid.", nameof(rect));
        int bpp = BytesPerPixel(format);
        int stride = (rect.Width * bpp + 3) & ~3;
        bitmapData.Width = rect.Width;
        bitmapData.Height = rect.Height;
        bitmapData.PixelFormat = format;
        bitmapData.LockedRect = rect;
        bitmapData.LockMode = flags;
        if ((flags & ImageLockMode.UserInputBuffer) == 0)
        {
            bitmapData.Stride = stride;
            bitmapData.Scan0 = Marshal.AllocHGlobal(stride * rect.Height);
            bitmapData.OwnsBuffer = true;
        }
        if ((flags & ImageLockMode.ReadOnly) != 0) CopyOut(bitmapData);
        return bitmapData;
    }

    public void UnlockBits(BitmapData bitmapdata)
    {
        ArgumentNullException.ThrowIfNull(bitmapdata);
        try
        {
            if ((bitmapdata.LockMode & ImageLockMode.WriteOnly) != 0) CopyIn(bitmapdata);
        }
        finally
        {
            if (bitmapdata.OwnsBuffer && bitmapdata.Scan0 != IntPtr.Zero) Marshal.FreeHGlobal(bitmapdata.Scan0);
            bitmapdata.Scan0 = IntPtr.Zero;
            bitmapdata.OwnsBuffer = false;
        }
    }

    private static int BytesPerPixel(PixelFormat format) => format switch
    {
        PixelFormat.Format24bppRgb => 3,
        PixelFormat.Format32bppArgb or PixelFormat.Format32bppPArgb or PixelFormat.Format32bppRgb => 4,
        _ => throw new NotSupportedException($"LockBits supports 24bppRgb and the 32bpp formats; not {format}."),
    };

    /// <summary>Our premultiplied BGRA → the locked buffer's format.</summary>
    private unsafe void CopyOut(BitmapData data)
    {
        var src = Skia;
        int bpp = BytesPerPixel(data.PixelFormat);
        byte* pixels = (byte*)src.GetPixels();
        int srcStride = src.RowBytes;
        for (int y = 0; y < data.Height; y++)
        {
            byte* s = pixels + (data.LockedRect.Y + y) * srcStride + data.LockedRect.X * 4;
            byte* d = (byte*)data.Scan0 + y * data.Stride;
            for (int x = 0; x < data.Width; x++, s += 4, d += bpp)
            {
                byte b = s[0], g = s[1], r = s[2], a = s[3];
                if (data.PixelFormat != PixelFormat.Format32bppPArgb && a != 0 && a != 255)
                {
                    b = (byte)Math.Min(255, (b * 255 + a / 2) / a);
                    g = (byte)Math.Min(255, (g * 255 + a / 2) / a);
                    r = (byte)Math.Min(255, (r * 255 + a / 2) / a);
                }
                d[0] = b;
                d[1] = g;
                d[2] = r;
                if (bpp == 4) d[3] = data.PixelFormat == PixelFormat.Format32bppRgb ? (byte)255 : a;
            }
        }
    }

    /// <summary>The locked buffer → our premultiplied BGRA.</summary>
    private unsafe void CopyIn(BitmapData data)
    {
        var dst = Skia;
        int bpp = BytesPerPixel(data.PixelFormat);
        byte* pixels = (byte*)dst.GetPixels();
        int dstStride = dst.RowBytes;
        for (int y = 0; y < data.Height; y++)
        {
            byte* s = (byte*)data.Scan0 + y * data.Stride;
            byte* d = pixels + (data.LockedRect.Y + y) * dstStride + data.LockedRect.X * 4;
            for (int x = 0; x < data.Width; x++, s += bpp, d += 4)
            {
                byte b = s[0], g = s[1], r = s[2];
                byte a = bpp == 4 && data.PixelFormat != PixelFormat.Format32bppRgb ? s[3] : (byte)255;
                if (data.PixelFormat != PixelFormat.Format32bppPArgb && a != 255)
                {
                    b = (byte)((b * a + 127) / 255);
                    g = (byte)((g * a + 127) / 255);
                    r = (byte)((r * a + 127) / 255);
                }
                d[0] = b;
                d[1] = g;
                d[2] = r;
                d[3] = a;
            }
        }
        dst.NotifyPixelsChanged();
    }
}
