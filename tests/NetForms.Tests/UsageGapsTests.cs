using System.Drawing.Imaging;
using Xunit;

namespace NetForms.Tests;

/// <summary>
/// The API the corpus of real projects still missed (docs/api/usage.md): image formats with their GDI+ GUIDs and
/// encoders, ImageList icons, TabPages by key, OpenFileDialog.SafeFileName, a Font constructor, LinkLabel.OverrideCursor.
/// </summary>
public class UsageGapsTests
{
    private static Bitmap Sample()
    {
        var bmp = new Bitmap(40, 30);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.White);
        g.FillRectangle(Brushes.Red, 0, 0, 20, 15);
        g.FillRectangle(Brushes.Blue, 20, 15, 20, 15);
        return bmp;
    }

    [Fact]
    public void ImageFormatsHaveTheGdiPlusGuids()
    {
        Assert.Equal(new Guid("b96b3caf-0728-11d3-9d7b-0000f81ef32e"), ImageFormat.Png.Guid);
        Assert.Equal(new Guid("b96b3cb5-0728-11d3-9d7b-0000f81ef32e"), ImageFormat.Icon.Guid);
        Assert.Equal(ImageFormat.Tiff, new ImageFormat(ImageFormat.Tiff.Guid));
        Assert.Equal("Tiff", new ImageFormat(ImageFormat.Tiff.Guid).ToString());
        Assert.NotEqual(ImageFormat.Bmp, ImageFormat.MemoryBmp);
        Assert.Equal("MemoryBMP", ImageFormat.MemoryBmp.ToString());
        Assert.StartsWith("[ImageFormat:", new ImageFormat(Guid.Empty).ToString());
    }

    [Theory]
    [InlineData("bmp")]
    [InlineData("gif")]
    public void BmpAndGifAreWrittenAndReadBack(string kind)
    {
        using var bmp = Sample();
        var format = kind == "bmp" ? ImageFormat.Bmp : ImageFormat.Gif;
        using var stream = new MemoryStream();
        bmp.Save(stream, format);
        stream.Position = 0;
        using var back = new Bitmap(stream);
        Assert.Equal(bmp.Size, back.Size);
        Assert.Equal(Color.Red.ToArgb(), back.GetPixel(5, 5).ToArgb());
        Assert.Equal(Color.Blue.ToArgb(), back.GetPixel(30, 25).ToArgb());
        Assert.Equal(Color.White.ToArgb(), back.GetPixel(30, 5).ToArgb());
    }

    [Fact]
    public void TiffIconAndMetafilesAreWrittenAsGdiPlusDoes()
    {
        using var bmp = Sample();
        using var tiff = new MemoryStream();
        bmp.Save(tiff, ImageFormat.Tiff);
        var t = tiff.ToArray();
        Assert.Equal((byte)'I', t[0]);
        Assert.Equal(42, BitConverter.ToInt16(t, 2));
        Assert.Equal(40 * 30 * 4, t.Length - BitConverter.ToInt32(t, 8 + 2 + 5 * 12 + 8)); // the strip ends the file

        // GDI+ has no encoder for these: it writes PNG.
        foreach (var format in new[] { ImageFormat.Icon, ImageFormat.Wmf, ImageFormat.Emf })
        {
            using var s = new MemoryStream();
            bmp.Save(s, format);
            Assert.Equal(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G' }, s.ToArray().Take(4).ToArray());
        }
    }

    [Fact]
    public void ImageListTakesIconsByKeyAndRemovesImages()
    {
        using var list = new ImageList();
        list.Images.Add("info", SystemIcons.Information);
        list.Images.Add(SystemIcons.Information);
        using var extra = new Bitmap(16, 16);
        int index = list.Images.Add(extra, Color.Magenta);
        Assert.Equal(2, index);
        Assert.Equal("info", list.Images.Keys[0]);
        Assert.Equal(0, list.Images.IndexOfKey("info"));
        var first = list.Images[2];
        list.Images.Remove(first);
        Assert.Equal(2, list.Images.Count);
    }

    [Fact]
    public void TabPagesAreAddedAndInsertedByKey()
    {
        var tabs = new TabControl();
        tabs.TabPages.Add("orders", "Orders", 0);
        tabs.TabPages.Add("stock", "Stock", "box");
        tabs.TabPages.Insert(0, "home", "Home");
        tabs.TabPages.Insert(1, "news", "News", 3);
        Assert.Equal(new[] { "home", "news", "orders", "stock" }, tabs.TabPages.Cast<TabPage>().Select(p => p.Name).ToArray());
        Assert.Equal("box", tabs.TabPages["stock"]!.ImageKey);
        Assert.Equal(3, tabs.TabPages["news"]!.ImageIndex);
        tabs.TabPages.Remove(tabs.TabPages["news"]!);
        Assert.Equal(3, tabs.TabPages.Count);
    }

    [Fact]
    public void OpenFileDialogSafeFileNameIsTheNameWithoutTheFolder()
    {
        var dialog = new OpenFileDialog { FileName = Path.Combine("data", "orders", "march.csv") };
        Assert.Equal("march.csv", dialog.SafeFileName);
        Assert.Equal(new[] { "march.csv" }, dialog.SafeFileNames);
        Assert.True(dialog.SelectReadOnly);
    }

    [Fact]
    public void FontTakesACharSetWithAFamily()
    {
        using var font = new Font(FontFamily.GenericSansSerif, 10f, FontStyle.Bold, GraphicsUnit.Point, 204);
        Assert.Equal((byte)204, font.GdiCharSet);
        Assert.True(font.Bold);
        using var vertical = new Font(FontFamily.GenericSansSerif, 10f, FontStyle.Regular, GraphicsUnit.Point, 1, true);
        Assert.True(vertical.GdiVerticalFont);
    }

    private sealed class BusyLinkLabel : LinkLabel
    {
        public void Busy(bool on) => OverrideCursor = on ? Cursors.WaitCursor : null;

        public Link? At(int x, int y) => PointInLink(x, y);
    }

    [Fact]
    public void LinkLabelOverrideCursorWinsOverItsCursor()
    {
        var platform = TestPlatform.Install();
        var link = new BusyLinkLabel { Text = "Open the report", Location = new Point(10, 10), AutoSize = true };
        using var form = new Form { ClientSize = new Size(300, 100) };
        form.Controls.Add(link);
        form.Show();
        var window = platform.Windows.Last();
        window.Host.MouseMove(new Point(15, 15), NetForms.Platform.MouseButton.None, NetForms.Platform.InputModifiers.None);
        Assert.Equal(Cursors.Hand.Name, window.CursorName);
        link.Busy(true);
        Assert.Equal(Cursors.WaitCursor.Name, window.CursorName);
        link.Busy(false);
        Assert.Equal(Cursors.Hand.Name, window.CursorName);
        Assert.NotNull(link.At(5, 5));
        Assert.Null(link.At(290, 5));
    }
}
