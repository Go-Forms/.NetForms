using System.Reflection;
using Xunit;

namespace NetForms.Tests;

/// <summary>
/// Ф6: .resx. A form's image and icon live in its .resx the way Visual Studio keeps them - base64 bytes
/// typed "System.Drawing.Bitmap, System.Drawing" - and ComponentResourceManager reads them at run time.
/// The name binds through the shared framework's System.Drawing facade to System.Drawing.Common, which
/// NetForms ships as a facade forwarding to NetForms.Drawing (decision 107).
/// </summary>
public class ResourcesTests
{
    [Fact]
    public void ImagesAndIconsComeOutOfTheResx()
    {
        TestPlatform.Install();
        using var form = new Gallery.ResourcesForm();
        var picture = form.Controls.OfType<PictureBox>().Single();
        var image = Assert.IsType<Bitmap>(picture.Image);
        Assert.Equal(new Size(24, 24), image.Size);
        Assert.Equal(Color.FromArgb(255, 140, 0), image.GetPixel(12, 12));   // the orange disc
        Assert.Equal(Color.FromArgb(30, 90, 200), image.GetPixel(1, 1));     // the blue square
        Assert.NotNull(form.Icon);
        Assert.Equal(Color.FromArgb(0, 150, 60), form.Icon!.ToBitmap().GetPixel(8, 8));
    }

    [Fact]
    public void TheOldAssemblyNamesBindToNetForms()
    {
        // What a .resx says and what a library compiled against System.Drawing.Common references.
        var bitmap = Type.GetType("System.Drawing.Bitmap, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", throwOnError: true);
        Assert.Same(typeof(Bitmap), bitmap);
        Assert.Same(typeof(Icon), Type.GetType("System.Drawing.Icon, System.Drawing.Common", throwOnError: true));
        Assert.Same(typeof(Font), Type.GetType("System.Drawing.Font, System.Drawing.Common", throwOnError: true));
    }

    [Fact]
    public void TheFacadeForwardsEveryTypeNetFormsDrawingSharesWithSystemDrawingCommon()
    {
        using var stream = typeof(NetForms.Converter.ProjectConverter).Assembly.GetManifestResourceStream("NetForms.Converter.winforms-types.txt")!;
        var real = new StreamReader(stream).ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();
        var shared = typeof(Bitmap).Assembly.GetExportedTypes().Where(t => !t.IsNested && real.Contains(t.FullName!)).ToList();
        var facade = Assembly.Load("System.Drawing.Common");
        var forwarded = facade.GetForwardedTypes().ToHashSet();
        Assert.Empty(shared.Where(t => !forwarded.Contains(t)).Select(t => t.FullName));
    }

    /// <summary>
    /// The same for System.Windows.Forms (decision 123): regenerate src/NetForms.WindowsForms/Forwards.cs with
    /// <c>tools/NetForms.ApiDiff --forwards-winforms</c> when this fails.
    /// </summary>
    [Fact]
    public void TheFacadeForwardsEveryTypeNetFormsSharesWithSystemWindowsForms()
    {
        using var stream = typeof(NetForms.Converter.ProjectConverter).Assembly.GetManifestResourceStream("NetForms.Converter.winforms-types.txt")!;
        var real = new StreamReader(stream).ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();
        var shared = typeof(Control).Assembly.GetExportedTypes().Where(t => !t.IsNested && real.Contains(t.FullName!)).ToList();
        var facade = Assembly.Load("System.Windows.Forms");
        var forwarded = facade.GetForwardedTypes().ToHashSet();
        Assert.Empty(shared.Where(t => !forwarded.Contains(t)).Select(t => t.FullName));
        // What a .resx names a binary-formatted ImageList stream by.
        Assert.Same(typeof(ImageListStreamer), Type.GetType("System.Windows.Forms.ImageListStreamer, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", throwOnError: true));
    }
}

/// <summary>A localizable form: its properties come from the .resx through ApplyResources; the designer shows it and refuses to write it.</summary>
public sealed class LocalizableFormTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "netforms-loc-" + Guid.NewGuid().ToString("N"));

    public LocalizableFormTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void ALocalizableFormIsShownAndNotRewritten()
    {
        TestPlatform.Install();
        File.WriteAllText(Path.Combine(_dir, "LocForm.cs"), "namespace Loc { public partial class LocForm : Form { public LocForm() { InitializeComponent(); } } }");
        var designer = Path.Combine(_dir, "LocForm.Designer.cs");
        File.WriteAllText(designer, """
            namespace Loc
            {
                partial class LocForm
                {
                    private System.ComponentModel.IContainer components = null;

                    private void InitializeComponent()
                    {
                        System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(LocForm));
                        button1 = new Button();
                        SuspendLayout();
                        //
                        // button1
                        //
                        resources.ApplyResources(button1, "button1");
                        button1.Name = "button1";
                        //
                        // LocForm
                        //
                        resources.ApplyResources(this, "$this");
                        AutoScaleMode = AutoScaleMode.Font;
                        Controls.Add(button1);
                        Name = "LocForm";
                        ResumeLayout(false);
                    }

                    private Button button1;
                }
            }
            """);
        File.WriteAllText(Path.Combine(_dir, "LocForm.resx"), """
            <?xml version="1.0" encoding="utf-8"?>
            <root>
              <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
              <assembly alias="System.Drawing" name="System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a" />
              <metadata name="$this.Localizable" type="System.Boolean, mscorlib"><value>True</value></metadata>
              <data name="button1.Location" type="System.Drawing.Point, System.Drawing"><value>12, 12</value></data>
              <data name="button1.Size" type="System.Drawing.Size, System.Drawing"><value>90, 23</value></data>
              <data name="button1.Text" xml:space="preserve"><value>Сохранить</value></data>
              <data name="$this.ClientSize" type="System.Drawing.Size, System.Drawing"><value>200, 80</value></data>
              <data name="$this.Text" xml:space="preserve"><value>Localized</value></data>
            </root>
            """);

        using var surface = NetForms.Design.DesignSurface.Open(designer);
        var button = (Button)surface.Model.Find("button1")!.Instance;
        Assert.Equal("Сохранить", button.Text);
        Assert.Equal(new Rectangle(12, 12, 90, 23), button.Bounds);
        Assert.Equal("Localized", ((Form)surface.Model.Root.Instance).Text);
        Assert.NotEmpty(surface.Render().Png);

        var before = surface.Source;
        var ex = Assert.Throws<NetForms.Design.DesignerEditException>(() => surface.Apply(new[] { new NetForms.Design.DesignerOp { Op = "setBounds", Id = "button1", X = 40, Y = 40 } }));
        Assert.Contains("localizable", ex.Message);
        Assert.Equal(before, surface.Source);
    }
}
