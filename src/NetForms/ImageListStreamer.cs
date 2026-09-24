using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.Serialization;

namespace System.Windows.Forms;

/// <summary>
/// The images of an <see cref="ImageList"/> as the designer stores them in a .resx
/// (<c>imageList1.ImageStream = (ImageListStreamer)resources.GetObject("imageList1.ImageStream")</c>).
/// </summary>
/// <remarks>
/// The data is what comctl32's ImageList_Write produces, compressed with WinForms' run-length encoding behind a
/// "MSFt" header: an ILHEAD, then the image strip as a BMP (four images per row), then the mask as a BMP when the
/// list has one. WinForms hands that stream to ImageList_Read; NetForms reads it itself (decision 123), so the same
/// resource works on Linux. Writing produces the same format (ILP_DOWNLEVEL: version 0x101, 32-bit images and a
/// mask), so a list saved by NetForms opens in Visual Studio.
/// </remarks>
[Serializable]
public sealed class ImageListStreamer : ISerializable, IDisposable
{
    private static ReadOnlySpan<byte> HeaderMagic => "MSFt"u8;

    private const ushort IlMagic = 0x4C49; // "IL"
    private const ushort IlVersion = 0x0101;
    private const int IlcMask = 0x0001;
    private const int IlcColorDdb = 0x00FE;
    private const int TileCount = 4;

    private readonly List<Bitmap> _images = new();

    internal ImageListStreamer(ImageList imageList)
    {
        ImageSize = imageList.ImageSize;
        ColorDepth = imageList.ColorDepth;
        foreach (Image image in imageList.Images) _images.Add(new Bitmap(image, ImageSize));
    }

    private ImageListStreamer(SerializationInfo info, StreamingContext context)
    {
        if (info.GetValue("Data", typeof(byte[])) is byte[] data) Deserialize(data);
    }

    internal ImageListStreamer(byte[] data) => Deserialize(data);

    internal Size ImageSize { get; private set; }

    internal ColorDepth ColorDepth { get; private set; } = ColorDepth.Depth32Bit;

    /// <summary>The images, each <see cref="ImageSize"/>, with transparency applied from the alpha channel or the mask.</summary>
    internal IReadOnlyList<Bitmap> Images => _images;

    public void GetObjectData(SerializationInfo si, StreamingContext context) => si.AddValue("Data", Serialize());

    public void Dispose()
    {
        foreach (var image in _images) image.Dispose();
        _images.Clear();
    }

    // --- reading ---------------------------------------------------------------------------

    private void Deserialize(byte[] data)
    {
        var bytes = Decompress(data);
        try
        {
            Read(bytes);
        }
        catch (Exception ex) when (ex is EndOfStreamException or ArgumentException or IndexOutOfRangeException)
        {
            throw new InvalidOperationException("Image list stream could not be loaded.", ex);
        }
    }

    /// <summary>WinForms' run-length decoding: after "MSFt", (count, value) byte pairs. Data without the header is used as is.</summary>
    internal static byte[] Decompress(byte[] input)
    {
        if (!input.AsSpan().StartsWith(HeaderMagic)) return input;
        int length = 0;
        for (int i = HeaderMagic.Length; i + 1 < input.Length; i += 2) length += input[i];
        var output = new byte[length];
        int o = 0;
        for (int i = HeaderMagic.Length; i + 1 < input.Length; i += 2)
        {
            output.AsSpan(o, input[i]).Fill(input[i + 1]);
            o += input[i];
        }
        return output;
    }

    internal static byte[] Compress(ReadOnlySpan<byte> input)
    {
        using var stream = new MemoryStream();
        stream.Write(HeaderMagic);
        int i = 0;
        while (i < input.Length)
        {
            byte value = input[i];
            int run = 1;
            while (run < 255 && i + run < input.Length && input[i + run] == value) run++;
            stream.WriteByte((byte)run);
            stream.WriteByte(value);
            i += run;
        }
        return stream.ToArray();
    }

    private void Read(byte[] bytes)
    {
        using var reader = new BinaryReader(new MemoryStream(bytes));
        if (reader.ReadUInt16() != IlMagic) throw new ArgumentException("Not an image list stream.");
        ushort version = reader.ReadUInt16();
        int count = reader.ReadUInt16();
        reader.ReadUInt16(); // cMaxImage
        reader.ReadUInt16(); // cGrow
        int cx = reader.ReadUInt16();
        int cy = reader.ReadUInt16();
        reader.ReadUInt32(); // bkcolor
        int flags = reader.ReadUInt16();
        for (int i = 0; i < 4; i++) reader.ReadInt16(); // overlay indices
        if (version != IlVersion && version != 0x0600) throw new ArgumentException($"Unknown image list stream version 0x{version:x}.");

        ImageSize = new Size(cx, cy);
        ColorDepth = (flags & IlcColorDdb) switch
        {
            0x04 => ColorDepth.Depth4Bit,
            0x08 => ColorDepth.Depth8Bit,
            0x10 => ColorDepth.Depth16Bit,
            0x18 => ColorDepth.Depth24Bit,
            0x20 => ColorDepth.Depth32Bit,
            _ => ColorDepth.Depth8Bit,
        };

        var strip = ReadDib(reader, out bool hasAlpha);
        var mask = (flags & IlcMask) != 0 ? ReadDib(reader, out _) : null;

        for (int index = 0; index < count; index++)
        {
            int sx = index % TileCount * cx, sy = index / TileCount * cy;
            // comctl32 decides per image: one with any alpha uses it, the others take transparency from the mask.
            bool imageAlpha = false;
            for (int y = 0; y < cy && hasAlpha && !imageAlpha; y++)
            {
                for (int x = 0; x < cx; x++)
                {
                    if (sx + x < strip.Width && sy + y < strip.Height && (strip.Pixels[(sy + y) * strip.Width + sx + x] & 0xFF000000) != 0)
                    {
                        imageAlpha = true;
                        break;
                    }
                }
            }
            var image = new Bitmap(cx, cy, PixelFormat.Format32bppArgb);
            for (int y = 0; y < cy; y++)
            {
                for (int x = 0; x < cx; x++)
                {
                    if (sx + x >= strip.Width || sy + y >= strip.Height) continue;
                    uint argb = strip.Pixels[(sy + y) * strip.Width + sx + x];
                    if (!imageAlpha)
                    {
                        bool transparent = mask != null && sx + x < mask.Width && sy + y < mask.Height && (mask.Pixels[(sy + y) * mask.Width + sx + x] & 0xFFFFFF) != 0;
                        argb = transparent ? 0 : argb | 0xFF000000;
                    }
                    image.SetPixel(x, y, Color.FromArgb(unchecked((int)argb)));
                }
            }
            _images.Add(image);
        }
    }

    private sealed class Dib
    {
        public int Width;
        public int Height;
        public uint[] Pixels = Array.Empty<uint>();
    }

    /// <summary>A BITMAPFILEHEADER-prefixed DIB, as the image list writes it: 1, 4, 8, 16, 24 or 32 bits, uncompressed.</summary>
    private static Dib ReadDib(BinaryReader reader, out bool hasAlpha)
    {
        hasAlpha = false;
        if (reader.ReadUInt16() != 0x4D42) throw new ArgumentException("Bitmap header expected.");
        reader.ReadUInt32(); // bfSize
        reader.ReadUInt32(); // reserved
        reader.ReadUInt32(); // bfOffBits
        int headerSize = reader.ReadInt32();
        int width = reader.ReadInt32();
        int height = reader.ReadInt32();
        reader.ReadUInt16(); // planes
        int bits = reader.ReadUInt16();
        int compression = reader.ReadInt32();
        reader.ReadInt32(); // size image
        reader.ReadInt32();
        reader.ReadInt32();
        int colorsUsed = reader.ReadInt32();
        reader.ReadInt32(); // important
        if (headerSize > 40) reader.ReadBytes(headerSize - 40);
        if (compression != 0 && compression != 3) throw new ArgumentException("Compressed bitmaps are not supported.");

        uint[] masks = { 0x00FF0000, 0x0000FF00, 0x000000FF };
        if (compression == 3) masks = new[] { reader.ReadUInt32(), reader.ReadUInt32(), reader.ReadUInt32() };

        uint[] palette = Array.Empty<uint>();
        if (bits <= 8)
        {
            // Like comctl32's reader: the palette always has 2^bits entries.
            int entries = 1 << bits;
            palette = new uint[entries];
            for (int i = 0; i < entries; i++) palette[i] = reader.ReadUInt32() & 0xFFFFFF;
        }
        _ = colorsUsed;

        bool bottomUp = height > 0;
        height = Math.Abs(height);
        int stride = (width * bits + 31) / 32 * 4;
        var dib = new Dib { Width = width, Height = height, Pixels = new uint[width * height] };
        for (int row = 0; row < height; row++)
        {
            var line = reader.ReadBytes(stride);
            int y = bottomUp ? height - 1 - row : row;
            for (int x = 0; x < width; x++)
            {
                uint px = bits switch
                {
                    1 => palette[(line[x >> 3] >> (7 - (x & 7))) & 1],
                    4 => palette[(line[x >> 1] >> ((x & 1) == 0 ? 4 : 0)) & 0xF],
                    8 => palette[line[x]],
                    16 => Rgb555(BitConverter.ToUInt16(line, x * 2)),
                    24 => (uint)(line[x * 3] | line[x * 3 + 1] << 8 | line[x * 3 + 2] << 16),
                    32 => BitConverter.ToUInt32(line, x * 4),
                    _ => throw new ArgumentException($"Unsupported bit depth {bits}."),
                };
                if (bits == 32 && (px & 0xFF000000) != 0) hasAlpha = true;
                dib.Pixels[y * width + x] = px;
            }
        }
        return dib;
    }

    private static uint Rgb555(ushort v)
    {
        uint r = (uint)((v >> 10) & 0x1F), g = (uint)((v >> 5) & 0x1F), b = (uint)(v & 0x1F);
        return (r << 3 | r >> 2) << 16 | (g << 3 | g >> 2) << 8 | (b << 3 | b >> 2);
    }

    // --- writing ---------------------------------------------------------------------------

    /// <summary>ImageList_WriteEx(ILP_DOWNLEVEL) of a 32-bit list with a mask, compressed as WinForms does.</summary>
    internal byte[] Serialize()
    {
        int cx = ImageSize.Width, cy = ImageSize.Height, count = _images.Count;
        int rows = Math.Max(1, (count + TileCount - 1) / TileCount);
        using var stream = new MemoryStream();
        using (var w = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            w.Write(IlMagic);
            w.Write(IlVersion);
            w.Write((ushort)count);
            w.Write((ushort)(rows * TileCount));
            w.Write((ushort)TileCount);
            w.Write((ushort)cx);
            w.Write((ushort)cy);
            w.Write(0xFFFFFFFF); // CLR_NONE
            w.Write((ushort)(IlcMask | 0x20));
            for (int i = 0; i < 4; i++) w.Write((short)-1);

            int width = cx * TileCount, height = cy * rows;
            WriteDib(w, width, height, 32, (x, y) => PixelAt(x, y, cx, cy) is { } c ? (uint)c.ToArgb() : 0);
            WriteDib(w, width, height, 1, (x, y) => PixelAt(x, y, cx, cy) is { A: > 0 } ? 0u : 1u);
        }
        return Compress(stream.GetBuffer().AsSpan(0, (int)stream.Length));
    }

    private Color? PixelAt(int x, int y, int cx, int cy)
    {
        int index = y / cy * TileCount + x / cx;
        if (index >= _images.Count) return null;
        return _images[index].GetPixel(x % cx, y % cy);
    }

    private static void WriteDib(BinaryWriter w, int width, int height, int bits, Func<int, int, uint> pixel)
    {
        int stride = (width * bits + 31) / 32 * 4;
        int paletteSize = bits == 1 ? 8 : 0;
        int offBits = 14 + 40 + paletteSize;
        w.Write((ushort)0x4D42);
        w.Write(offBits + stride * height);
        w.Write(0);
        w.Write(offBits);
        w.Write(40);
        w.Write(width);
        w.Write(height);
        w.Write((ushort)1);
        w.Write((ushort)bits);
        w.Write(0);
        w.Write(stride * height);
        w.Write(0);
        w.Write(0);
        w.Write(0);
        w.Write(0);
        if (bits == 1)
        {
            w.Write(0x00000000u);
            w.Write(0x00FFFFFFu);
        }
        var line = new byte[stride];
        for (int row = height - 1; row >= 0; row--)
        {
            Array.Clear(line);
            for (int x = 0; x < width; x++)
            {
                uint px = pixel(x, row);
                if (bits == 32) BitConverter.TryWriteBytes(line.AsSpan(x * 4), px);
                else if (px != 0) line[x >> 3] |= (byte)(0x80 >> (x & 7));
            }
            w.Write(line);
        }
    }
}
