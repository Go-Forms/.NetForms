using System.Drawing.Imaging;
using NetForms.Design;
using NetForms.Design.Serialization;
using Xunit;
using static NetForms.Tests.DesignerCodeReaderTests;

namespace NetForms.Tests;

/// <summary>
/// Pictures set in the designer (decision 176): a picture or an icon from a file goes into the form's .resx as VS
/// writes it - the file's bytes, read by the type's converter - and the code gets it with resources.GetObject; an
/// ImageList's pictures go into its ImageStream (a BinaryFormatter record, written without BinaryFormatter).
/// </summary>
public sealed class DesignerImageTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "netforms-images-" + Guid.NewGuid().ToString("N"));

    public DesignerImageTests()
    {
        TestPlatform.Install();
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private string CopyHelloForms()
    {
        var source = Sample("HelloForms/MainForm.Designer.cs");
        var target = Path.Combine(_dir, "MainForm.Designer.cs");
        File.Copy(source, target);
        File.Copy(DesignerCodeReader.CompanionPath(source)!, DesignerCodeReader.CompanionPath(target)!);
        return target;
    }

    /// <summary>A PNG file of <paramref name="size"/> filled with <paramref name="color"/>.</summary>
    private string Png(string name, int size, Color color)
    {
        var path = Path.Combine(_dir, name);
        using var b = new Bitmap(size, size);
        using (var g = Graphics.FromImage(b)) g.Clear(color);
        b.Save(path, ImageFormat.Png);
        return path;
    }

    /// <summary>An .ico holding one PNG image (what VS and Windows accept since Vista).</summary>
    private string Ico(string name, int size, Color color)
    {
        var png = File.ReadAllBytes(Png(name + ".png", size, color));
        using var s = new MemoryStream();
        using var w = new BinaryWriter(s);
        w.Write((short)0); w.Write((short)1); w.Write((short)1);
        w.Write((byte)size); w.Write((byte)size); w.Write((byte)0); w.Write((byte)0);
        w.Write((short)1); w.Write((short)32); w.Write(png.Length); w.Write(22);
        w.Write(png);
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, s.ToArray());
        return path;
    }

    private static DesignerOp SetImage(string id, string prop, string path) => new() { Op = "setImage", Id = id, Prop = prop, Value = path };

    [Fact]
    public void APictureFromAFileGoesToTheResxAndReadsBack()
    {
        var path = CopyHelloForms();
        var resx = Path.ChangeExtension(Path.ChangeExtension(path, null), ".resx");
        Assert.False(File.Exists(resx));
        using (var s = DesignSurface.Open(path))
        {
            s.AutoSave = true;
            var row = s.GetProperties("button1").Single(r => r.Name == "Image");
            Assert.Equal(("image", "(none)"), (row.Editor, row.Value));
            s.Apply(new[] { SetImage("button1", "Image", Png("save.png", 16, Color.Red)) });

            Assert.Contains("System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));", s.Source);
            Assert.Contains("button1.Image = (Image)resources.GetObject(\"button1.Image\");", s.Source.Replace("this.", ""));
            row = s.GetProperties("button1").Single(r => r.Name == "Image");
            Assert.Equal("(Bitmap) 16 × 16", row.Value);
            Assert.NotNull(row.Preview);
        }
        // The .resx as VS writes it, with a byte-order mark.
        var text = File.ReadAllText(resx);
        Assert.Contains("<data name=\"button1.Image\" type=\"System.Drawing.Bitmap, System.Drawing\" mimetype=\"application/x-microsoft.net.object.bytearray.base64\">", text);
        Assert.Contains("<assembly alias=\"System.Drawing\"", text);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, File.ReadAllBytes(resx)[..3]);

        // Opened again (and by NetForms at run time through the same entry), the picture is there.
        using var again = DesignSurface.Open(path);
        var image = ((Button)again.Model.Find("button1")!.Instance).Image!;
        Assert.Equal(16, image.Width);
        Assert.Equal(Color.Red.ToArgb(), ((Bitmap)image).GetPixel(8, 8).ToArgb());
    }

    [Fact]
    public void AFormsIconAndClearingAPictureTakesItOutOfTheResx()
    {
        var path = CopyHelloForms();
        using var s = DesignSurface.Open(path);
        s.AutoSave = true;
        s.Apply(new[] { SetImage("", "Icon", Ico("app.ico", 16, Color.Blue)), SetImage("label1", "Image", Png("a.png", 8, Color.Green)) });
        Assert.Contains("Icon = (Icon)resources.GetObject(\"$this.Icon\");", s.Source);
        Assert.Contains("<data name=\"$this.Icon\" type=\"System.Drawing.Icon, System.Drawing\"", s.ResxSource);
        Assert.Throws<DesignerEditException>(() => s.Apply(new[] { SetImage("", "Icon", Png("not-an-icon.png", 8, Color.Green)) }));

        s.Apply(new[] { SetImage("label1", "Image", "") });
        Assert.DoesNotContain("label1.Image", s.Source);
        Assert.DoesNotContain("label1.Image", s.ResxSource);
        Assert.Contains("$this.Icon", s.ResxSource);

        // Undo puts the .resx back with the code.
        Assert.True(s.Undo());
        Assert.Contains("label1.Image", s.ResxSource);
        Assert.Contains("label1.Image", File.ReadAllText(s.ResxPath!));
    }

    [Fact]
    public void AnImageListsPicturesAreAddedNamedAndReordered()
    {
        var path = CopyHelloForms();
        using (var s = DesignSurface.Open(path))
        {
            s.AutoSave = true;
            s.Apply(new[] { new DesignerOp { Op = "add", Type = "System.Windows.Forms.ImageList" } });
            s.Apply(new[] { new DesignerOp { Op = "setImages", Id = "imageList1", Values = new[] { "red.png", "blue.png" }, Ids = new[] { Png("red.png", 16, Color.Red), Png("blue.png", 16, Color.Blue) } } });
            var source = s.Source.Replace("this.", "");
            Assert.Contains("imageList1.ImageStream = (ImageListStreamer)resources.GetObject(\"imageList1.ImageStream\");", source);
            Assert.Contains("imageList1.Images.SetKeyName(0, \"red.png\");", source);
            Assert.Contains("imageList1.Images.SetKeyName(1, \"blue.png\");", source);
            Assert.Contains("<data name=\"imageList1.ImageStream\" mimetype=\"application/x-microsoft.net.object.binary.base64\">", s.ResxSource);
            var row = s.GetProperties("imageList1").Single(r => r.Name == "Images");
            Assert.Equal(("images", 2), (row.Editor, row.Previews!.Count));

            // Reordered, renamed, one removed.
            s.Apply(new[] { new DesignerOp { Op = "setImages", Id = "imageList1", Values = new[] { "first" }, Ids = new[] { "#1" } } });
            Assert.Contains("imageList1.Images.SetKeyName(0, \"first\");", s.Source.Replace("this.", ""));
        }
        // The stream the designer wrote is read back (through System.Resources.Extensions, as at run time).
        using var again = DesignSurface.Open(path);
        var list = (ImageList)again.Model.Find("imageList1")!.Instance;
        Assert.Single(list.Images);
        Assert.Equal("first", list.Images.Keys[0]);
        Assert.Equal(Color.Blue.ToArgb(), ((Bitmap)list.Images[0]).GetPixel(8, 8).ToArgb());
    }

    [Fact]
    public void AnUnrelatedEditLeavesAnExistingResxAsItIs()
    {
        var source = Sample("Gallery/ResourcesForm.Designer.cs");
        var target = Path.Combine(_dir, "ResourcesForm.Designer.cs");
        File.Copy(source, target);
        foreach (var f in new[] { DesignerCodeReader.CompanionPath(source)!, ResxResources.ResxPathOf(source)! })
            File.Copy(f, Path.Combine(_dir, Path.GetFileName(f)));
        var resx = ResxResources.ResxPathOf(target)!;
        var before = File.ReadAllBytes(resx);
        using var s = DesignSurface.Open(target);
        s.AutoSave = true;
        s.Apply(new[] { new DesignerOp { Op = "setProp", Id = "", Prop = "Text", Value = "Resources!" } });
        Assert.Equal(before, File.ReadAllBytes(resx));

        // A new picture is added beside what is there; the others stay byte for byte.
        s.Apply(new[] { SetImage("pictureBox1", "BackgroundImage", Png("bg.png", 4, Color.Yellow)) });
        var text = File.ReadAllText(resx);
        Assert.Contains("pictureBox1.BackgroundImage", text);
        Assert.Contains(System.Text.Encoding.UTF8.GetString(before[3..]).Split("<data name=\"pictureBox1.Image\"")[1].Split("</data>")[0], text);
    }

    [Fact]
    public void TheImageStreamRecordIsWhatBinaryFormatterWrote()
    {
        // The header of the record the Gallery sample's .resx holds (written by VS), byte for byte.
        var record = ResxWriter.BinaryRecord(new byte[] { 1, 2, 3 });
        var expected = Convert.FromBase64String("AAEAAAD/////AQAAAAAAAAAMAgAAAEZTeXN0ZW0uV2luZG93cy5Gb3JtcywgQ3VsdHVyZT1uZXV0cmFsLCBQdWJsaWNLZXlUb2tlbj1iNzdhNWM1NjE5MzRlMDg5BQEAAAAmU3lzdGVtLldpbmRvd3MuRm9ybXMuSW1hZ2VMaXN0U3RyZWFtZXIBAAAABERhdGEHAgIAAAAJAwAAAA8DAAAA");
        Assert.Equal(expected, record[..expected.Length]);
        Assert.Equal(new byte[] { 3, 0, 0, 0, 2, 1, 2, 3, 11 }, record[expected.Length..]);
    }
}
