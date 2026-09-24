using System.Formats.Nrbf;
using System.Resources;
using System.Resources.Extensions;
using Xunit;

namespace NetForms.Tests;

/// <summary>
/// ImageList.ImageStream: the comctl32 image list stream the VS designer puts in a .resx, read and written by
/// NetForms itself. The fixture is a real one (Surviving-WinForms, MIT: MessageBoxForDevs/ExceptionalBox.resx,
/// four 16x16 images, 8-bit with a mask).
/// </summary>
public class ImageListStreamerTests
{
    private static byte[] FixtureNrbf() => Convert.FromBase64String(File.ReadAllText(
        Path.Combine(DesignerCodeReaderTests.RepoRoot, "tests", "Fixtures", "ImageStream", "ExceptionalBox.ImageStream.b64")));

    /// <summary>The "Data" byte array of the BinaryFormatter record, as WinForms' own NRBF reader takes it.</summary>
    private static byte[] FixtureData()
    {
        var record = (ClassRecord)NrbfDecoder.Decode(new MemoryStream(FixtureNrbf()));
        return ((SZArrayRecord<byte>)record.GetSerializationRecord("Data")!).GetArray();
    }

    private static ImageListStreamer Create(byte[] data) =>
        (ImageListStreamer)typeof(ImageListStreamer).GetConstructor(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, new[] { typeof(byte[]) })!
            .Invoke(new object[] { data });

    [Fact]
    public void ARealDesignerStreamGivesItsImages()
    {
        using var streamer = Create(FixtureData());
        using var list = new ImageList { ImageStream = streamer };
        Assert.Equal(4, list.Images.Count);
        Assert.Equal(new Size(16, 16), list.ImageSize);
        Assert.Equal(ColorDepth.Depth8Bit, list.ColorDepth);
        var images = list.Images.Cast<Bitmap>().ToList();
        Assert.All(images, b => Assert.Equal(new Size(16, 16), b.Size));
        // The 8-bit list was saved with an all-clear mask: opaque images, the "copy" icon on white.
        Assert.Equal(Color.White.ToArgb(), images[0].GetPixel(0, 0).ToArgb());
        Assert.All(images, b => Assert.Equal(255, b.GetPixel(8, 8).A));
        // Four different pictures, not one tile four times.
        Assert.Equal(4, images.Select(b => string.Join(",", Enumerable.Range(0, 256).Select(i => b.GetPixel(i % 16, i / 16).ToArgb()))).Distinct().Count());
    }

    [Fact]
    public void WithoutAlphaTheMaskMakesPixelsTransparent()
    {
        var picture = new Bitmap(16, 16);
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                picture.SetPixel(x, y, x < 8 ? Color.Red : Color.Transparent);
        using var list = new ImageList();
        list.Images.Add(picture);
        var serialize = typeof(ImageListStreamer).GetMethod("Serialize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var raw = ImageListStreamer.Decompress((byte[])serialize.Invoke(list.ImageStream, null)!);
        // Clear the alpha bytes of the 32-bit strip (ILHEAD 28 + BMP headers 54, then BGRA): a list saved without
        // alpha, as comctl32 writes 24-bit and palette lists, where only the mask knows what is transparent.
        for (int i = 28 + 54 + 3; i < 28 + 54 + 64 * 16 * 4; i += 4) raw[i] = 0;
        using var copy = new ImageList { ImageStream = Create(ImageListStreamer.Compress(raw)) };
        var image = (Bitmap)copy.Images[0];
        Assert.Equal(Color.Red.ToArgb(), image.GetPixel(2, 5).ToArgb());
        Assert.Equal(0, image.GetPixel(12, 5).A);
    }

    [Fact]
    public void WhatNetFormsWritesReadsBackTheSame()
    {
        using var original = new ImageList { ImageStream = Create(FixtureData()) };
        var written = original.ImageStream!;
        var data = (byte[])typeof(ImageListStreamer).GetMethod("Serialize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(written, null)!;
        Assert.Equal("MSFt"u8.ToArray(), data.Take(4));

        using var copy = new ImageList { ImageStream = Create(data) };
        Assert.Equal(original.Images.Count, copy.Images.Count);
        for (int i = 0; i < original.Images.Count; i++)
        {
            var a = (Bitmap)original.Images[i];
            var b = (Bitmap)copy.Images[i];
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    Assert.Equal(a.GetPixel(x, y).ToArgb(), b.GetPixel(x, y).ToArgb());
        }
        Assert.Equal(ColorDepth.Depth32Bit, copy.ColorDepth);
    }

    [Fact]
    public void AnEmptyListHasNoStreamAndNullClearsTheList()
    {
        using var list = new ImageList();
        Assert.Null(list.ImageStream);
        list.ImageStream = Create(FixtureData());
        Assert.Equal(4, list.Images.Count);
        list.ImageStream = null;
        Assert.Empty(list.Images);
    }

    /// <summary>
    /// The runtime path: the build embeds the .resx object pre-serialized, and System.Resources.Extensions reads it
    /// without BinaryFormatter - it needs the type "System.Windows.Forms.ImageListStreamer, System.Windows.Forms".
    /// </summary>
    [Fact]
    public void TheResourceReaderOfTheBuildGivesAnImageListStreamer()
    {
        using var resources = new MemoryStream();
        using (var writer = new PreserializedResourceWriter(resources))
        {
            // What MSBuild's GenerateResource does with a binary-formatted .resx entry (it only copies the bytes).
#pragma warning disable SYSLIB0011
            writer.AddBinaryFormattedResource("imageList1.ImageStream", FixtureNrbf(),
                "System.Windows.Forms.ImageListStreamer, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
#pragma warning restore SYSLIB0011
            writer.Generate();
        }
        using var reader = new DeserializingResourceReader(new MemoryStream(resources.ToArray()));
        var e = reader.GetEnumerator();
        Assert.True(e.MoveNext());
        var streamer = Assert.IsType<ImageListStreamer>(e.Value);
        using var list = new ImageList { ImageStream = streamer };
        Assert.Equal(4, list.Images.Count);
    }
}
