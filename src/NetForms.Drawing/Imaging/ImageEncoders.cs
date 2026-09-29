using System.Collections.Generic;
using System.IO;
using SkiaSharp;

namespace System.Drawing.Imaging;

/// <summary>
/// The encoders GDI+ has and Skia does not: BMP (32 bpp with alpha, 24 bpp without), TIFF (uncompressed RGBA) and GIF
/// (8 bpp with a fixed palette, a transparent index for transparent pixels, LZW). PNG, JPEG and WEBP are Skia's.
/// </summary>
internal static class ImageEncoders
{
    public static void Encode(SKBitmap bitmap, ImageFormat format, Stream stream)
    {
        using var rgba = ToRgba(bitmap);
        var pixels = rgba.GetPixelSpan();
        int w = rgba.Width, h = rgba.Height;
        if (format.Equals(ImageFormat.Bmp) || format.Equals(ImageFormat.MemoryBmp)) WriteBmp(pixels, w, h, stream);
        else if (format.Equals(ImageFormat.Tiff)) WriteTiff(pixels, w, h, stream);
        else if (format.Equals(ImageFormat.Gif)) WriteGif(pixels, w, h, stream);
        else throw new NotSupportedException($"No encoder for {format}.");
    }

    private static SKBitmap ToRgba(SKBitmap source)
    {
        var info = new SKImageInfo(source.Width, source.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        var copy = new SKBitmap(info);
        using var canvas = new SKCanvas(copy);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawBitmap(source, 0, 0);
        return copy;
    }

    private static bool HasAlpha(ReadOnlySpan<byte> rgba)
    {
        for (int i = 3; i < rgba.Length; i += 4) if (rgba[i] != 255) return true;
        return false;
    }

    // --- BMP -------------------------------------------------------------------------------------------------

    private static void WriteBmp(ReadOnlySpan<byte> rgba, int w, int h, Stream stream)
    {
        bool alpha = HasAlpha(rgba);
        int bpp = alpha ? 32 : 24;
        int stride = (w * bpp / 8 + 3) & ~3;
        int headerSize = alpha ? 108 : 40; // BITMAPV4HEADER keeps the alpha mask
        int offset = 14 + headerSize;
        int size = offset + stride * h;
        using var bw = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true);
        bw.Write((byte)'B'); bw.Write((byte)'M'); bw.Write(size); bw.Write(0); bw.Write(offset);
        bw.Write(headerSize); bw.Write(w); bw.Write(h); bw.Write((short)1); bw.Write((short)bpp);
        bw.Write(alpha ? 3 : 0); // BI_BITFIELDS : BI_RGB
        bw.Write(stride * h); bw.Write(3780); bw.Write(3780); bw.Write(0); bw.Write(0);
        if (alpha)
        {
            bw.Write(0x00FF0000); bw.Write(0x0000FF00); bw.Write(0x000000FF); bw.Write(unchecked((int)0xFF000000));
            bw.Write(0x73524742); // LCS_sRGB
            for (int i = 0; i < 12; i++) bw.Write(0); // endpoints and gamma
        }
        var row = new byte[stride];
        for (int y = h - 1; y >= 0; y--)
        {
            Array.Clear(row);
            for (int x = 0; x < w; x++)
            {
                int s = (y * w + x) * 4, d = x * (bpp / 8);
                row[d] = rgba[s + 2]; row[d + 1] = rgba[s + 1]; row[d + 2] = rgba[s];
                if (alpha) row[d + 3] = rgba[s + 3];
            }
            bw.Write(row);
        }
    }

    // --- TIFF ------------------------------------------------------------------------------------------------

    private static void WriteTiff(ReadOnlySpan<byte> rgba, int w, int h, Stream stream)
    {
        // Little-endian baseline TIFF: one strip of uncompressed RGBA (ExtraSamples = unassociated alpha).
        using var bw = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true);
        const int entries = 11;
        int ifdOffset = 8;
        int ifdSize = 2 + entries * 12 + 4;
        int bitsOffset = ifdOffset + ifdSize;
        int resOffset = bitsOffset + 8;
        int dataOffset = resOffset + 16;
        bw.Write((byte)'I'); bw.Write((byte)'I'); bw.Write((short)42); bw.Write(ifdOffset);
        bw.Write((short)entries);
        void Entry(short tag, short type, int count, int value) { bw.Write(tag); bw.Write(type); bw.Write(count); bw.Write(value); }
        Entry(256, 4, 1, w);                  // ImageWidth
        Entry(257, 4, 1, h);                  // ImageLength
        Entry(258, 3, 4, bitsOffset);         // BitsPerSample 8,8,8,8
        Entry(259, 3, 1, 1);                  // Compression: none
        Entry(262, 3, 1, 2);                  // Photometric: RGB
        Entry(273, 4, 1, dataOffset);         // StripOffsets
        Entry(277, 3, 1, 4);                  // SamplesPerPixel
        Entry(278, 4, 1, h);                  // RowsPerStrip
        Entry(279, 4, 1, w * h * 4);          // StripByteCounts
        Entry(282, 5, 1, resOffset);          // XResolution
        Entry(338, 3, 1, 2);                  // ExtraSamples: unassociated alpha
        bw.Write(0);                          // no next IFD
        bw.Write((short)8); bw.Write((short)8); bw.Write((short)8); bw.Write((short)8);
        bw.Write(96); bw.Write(1); bw.Write(96); bw.Write(1);
        bw.Write(rgba);
    }

    // --- GIF -------------------------------------------------------------------------------------------------

    private static void WriteGif(ReadOnlySpan<byte> rgba, int w, int h, Stream stream)
    {
        // The palette: 6x7x6 levels of red, green and blue (252 colours), then black, white, grey and the transparent
        // index 255. GDI+ writes GIFs with its own fixed halftone palette too.
        var palette = new byte[256 * 3];
        int n = 0;
        for (int r = 0; r < 6; r++)
            for (int g = 0; g < 7; g++)
                for (int b = 0; b < 6; b++)
                {
                    palette[n * 3] = (byte)(r * 51); palette[n * 3 + 1] = (byte)(g * 255 / 6); palette[n * 3 + 2] = (byte)(b * 51);
                    n++;
                }
        bool alpha = HasAlpha(rgba);
        var indices = new byte[w * h];
        for (int i = 0; i < indices.Length; i++)
        {
            int s = i * 4;
            if (alpha && rgba[s + 3] < 128) { indices[i] = 255; continue; }
            int r = (rgba[s] * 5 + 127) / 255, g = (rgba[s + 1] * 6 + 127) / 255, b = (rgba[s + 2] * 5 + 127) / 255;
            indices[i] = (byte)(r * 42 + g * 6 + b);
        }

        using var bw = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true);
        bw.Write("GIF89a"u8.ToArray());
        bw.Write((short)w); bw.Write((short)h);
        bw.Write((byte)0xF7); bw.Write((byte)0); bw.Write((byte)0); // global table of 256 colours
        bw.Write(palette);
        if (alpha)
        {
            bw.Write(new byte[] { 0x21, 0xF9, 4, 0x01, 0, 0, 255, 0 }); // graphic control: transparent index 255
        }
        bw.Write((byte)0x2C); bw.Write((short)0); bw.Write((short)0); bw.Write((short)w); bw.Write((short)h); bw.Write((byte)0);
        const int minCodeSize = 8;
        bw.Write((byte)minCodeSize);
        var lzw = Lzw(indices, minCodeSize);
        for (int i = 0; i < lzw.Length; i += 255)
        {
            int len = Math.Min(255, lzw.Length - i);
            bw.Write((byte)len);
            bw.Write(lzw, i, len);
        }
        bw.Write((byte)0);
        bw.Write((byte)0x3B);
    }

    private static byte[] Lzw(byte[] data, int minCodeSize)
    {
        int clear = 1 << minCodeSize, end = clear + 1;
        var output = new MemoryStream();
        int bitBuffer = 0, bitCount = 0, codeSize = minCodeSize + 1;
        void Emit(int code)
        {
            bitBuffer |= code << bitCount;
            bitCount += codeSize;
            while (bitCount >= 8)
            {
                output.WriteByte((byte)bitBuffer);
                bitBuffer >>= 8;
                bitCount -= 8;
            }
        }

        var table = new Dictionary<int, int>();
        int next = end + 1;
        Emit(clear);
        if (data.Length == 0)
        {
            Emit(end);
            if (bitCount > 0) output.WriteByte((byte)bitBuffer);
            return output.ToArray();
        }
        int prefix = data[0];
        for (int i = 1; i < data.Length; i++)
        {
            int c = data[i];
            int key = (prefix << 8) | c;
            if (table.TryGetValue(key, out int code))
            {
                prefix = code;
                continue;
            }
            Emit(prefix);
            if (next < 4096)
            {
                table[key] = next++;
                if (next > (1 << codeSize) && codeSize < 12) codeSize++;
            }
            else
            {
                Emit(clear);
                table.Clear();
                next = end + 1;
                codeSize = minCodeSize + 1;
            }
            prefix = c;
        }
        Emit(prefix);
        Emit(end);
        if (bitCount > 0) output.WriteByte((byte)bitBuffer);
        return output.ToArray();
    }
}
