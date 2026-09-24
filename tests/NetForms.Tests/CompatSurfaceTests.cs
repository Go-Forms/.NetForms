using System.ComponentModel;
using System.Drawing.Imaging;
using NetForms.Platform;
using Xunit;

namespace NetForms.Tests;

/// <summary>
/// Ф6.К, decisions 114-115: the API real projects reached for and NetForms lacked - pixel access, color
/// matrices, background images, PreviewKeyDown, ThreadException, forms inside panels, the .NET 1.x events.
/// </summary>
public class CompatSurfaceTests
{
    private static (Form form, TestWindow window) ShowForm(params Control[] controls)
    {
        var platform = TestPlatform.Install();
        var form = new Form { ClientSize = new Size(300, 200) };
        form.Controls.AddRange(controls);
        form.Show();
        return (form, platform.Windows.Last());
    }

    [Fact]
    public void LockBitsReadsAndWritesPixelsInTheGdiPlusLayout()
    {
        using var bitmap = new Bitmap(3, 2);
        bitmap.SetPixel(0, 0, Color.FromArgb(255, 10, 20, 30));
        bitmap.SetPixel(2, 1, Color.FromArgb(128, 200, 100, 50));

        var data = bitmap.LockBits(new Rectangle(0, 0, 3, 2), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        Assert.Equal(12, data.Stride);
        var bytes = new byte[data.Stride * 2];
        System.Runtime.InteropServices.Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
        Assert.Equal(new byte[] { 30, 20, 10, 255 }, bytes[0..4]);          // B G R A, not premultiplied
        Assert.Equal(128, bytes[12 + 8 + 3]);
        Assert.InRange(bytes[12 + 8 + 2], 199, 201);
        bytes[4] = 255; bytes[5] = 0; bytes[6] = 0; bytes[7] = 255;           // (1,0) becomes blue
        System.Runtime.InteropServices.Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
        bitmap.UnlockBits(data);
        Assert.Equal(Color.FromArgb(255, 0, 0, 255).ToArgb(), bitmap.GetPixel(1, 0).ToArgb());

        // 24bpp: three bytes a pixel, rows padded to four.
        var rgb = bitmap.LockBits(new Rectangle(0, 0, 3, 2), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        Assert.Equal(12, rgb.Stride);
        bitmap.UnlockBits(rgb);
        Assert.Equal(IntPtr.Zero, rgb.Scan0);
    }

    [Fact]
    public void AColorMatrixRecolorsDrawImage()
    {
        using var source = new Bitmap(2, 2);
        using (var g = Graphics.FromImage(source)) g.Clear(Color.FromArgb(255, 200, 0, 0));
        // The classic grayscale matrix of WinForms tutorials.
        var gray = new ColorMatrix(new[]
        {
            new[] { .3f, .3f, .3f, 0, 0 },
            new[] { .59f, .59f, .59f, 0, 0 },
            new[] { .11f, .11f, .11f, 0, 0 },
            new[] { 0f, 0, 0, 1, 0 },
            new[] { 0f, 0, 0, 0, 1 },
        });
        using var attributes = new ImageAttributes();
        attributes.SetColorMatrix(gray);
        using var target = new Bitmap(2, 2);
        using (var g = Graphics.FromImage(target))
            g.DrawImage(source, new Rectangle(0, 0, 2, 2), 0, 0, 2, 2, GraphicsUnit.Pixel, attributes);
        var c = target.GetPixel(1, 1);
        Assert.InRange(c.R, 58, 62); // 0.3 × 200
        Assert.Equal(c.R, c.G);
        Assert.Equal(c.R, c.B);
    }

    [Fact]
    public void RotateFlipTurnsTheImage()
    {
        using var bitmap = new Bitmap(3, 2);
        bitmap.SetPixel(0, 0, Color.Red);
        bitmap.RotateFlip(RotateFlipType.Rotate90FlipNone);
        Assert.Equal(new Size(2, 3), bitmap.Size);
        Assert.Equal(Color.Red.ToArgb(), bitmap.GetPixel(1, 0).ToArgb()); // top-left goes top-right
        bitmap.RotateFlip(RotateFlipType.RotateNoneFlipX);
        Assert.Equal(Color.Red.ToArgb(), bitmap.GetPixel(0, 0).ToArgb());
    }

    [Theory]
    [InlineData(ImageLayout.Center, 45, 45)]
    [InlineData(ImageLayout.None, 0, 0)]
    [InlineData(ImageLayout.Tile, 10, 10)]
    public void TheBackgroundImageIsLaidOutAsWinFormsLaysItOut(ImageLayout layout, int x, int y)
    {
        using var image = new Bitmap(10, 10);
        using (var g = Graphics.FromImage(image)) g.Clear(Color.Blue);
        var panel = new Panel { Bounds = new Rectangle(0, 0, 100, 100), BackColor = Color.White, BackgroundImage = image, BackgroundImageLayout = layout };
        var (form, window) = ShowForm(panel);
        using (form)
        {
            using var shot = window.Paint();
            Assert.Equal(Color.Blue.ToArgb(), shot.GetPixel(x + 2, y + 2).ToArgb());
            if (layout != ImageLayout.Tile) Assert.Equal(Color.White.ToArgb(), shot.GetPixel(x + 20, y + 20).ToArgb());
        }
        Assert.Equal(new Rectangle(0, 25, 100, 50), Control.CalculateBackgroundImageRectangle(new Rectangle(0, 0, 100, 100), new Size(20, 10), ImageLayout.Zoom));
    }

    [Fact]
    public void PreviewKeyDownComesFirstAndCanMakeAKeyAnInputKey()
    {
        var button = new Button();
        var other = new Button { Left = 100 };
        var order = new List<string>();
        button.PreviewKeyDown += (_, e) => { order.Add("preview " + e.KeyCode); if (e.KeyCode == Keys.Tab) e.IsInputKey = true; };
        button.KeyDown += (_, e) => order.Add("down " + e.KeyCode);
        var (form, window) = ShowForm(button, other);
        using (form)
        {
            button.Focus();
            window.Host.KeyDown((int)Keys.Tab, InputModifiers.None);
            // An input key is not a dialog key: focus stays, and the button sees KeyDown.
            Assert.Equal(new[] { "preview Tab", "down Tab" }, order);
            Assert.True(button.Focused);
        }
    }

    [Fact]
    public void AnExceptionInAHandlerGoesToThreadException()
    {
        var button = new Button { Bounds = new Rectangle(10, 10, 80, 30) };
        button.Click += (_, _) => throw new InvalidOperationException("boom");
        Exception? caught = null;
        ThreadExceptionEventHandler handler = (_, e) => caught = e.Exception;
        Application.ThreadException += handler;
        var (form, window) = ShowForm(button);
        try
        {
            window.Click(new Point(20, 20));
            Assert.IsType<InvalidOperationException>(caught);
        }
        finally
        {
            Application.ThreadException -= handler;
            form.Dispose();
        }
        // Without a handler (and no screen for the dialog), it propagates as before.
        var (form2, window2) = ShowForm(button);
        using (form2) Assert.Throws<InvalidOperationException>(() => window2.Click(new Point(20, 20)));
    }

    [Fact]
    public void AFormWithTopLevelFalseLivesInsideAPanel()
    {
        var panel = new Panel { Bounds = new Rectangle(0, 0, 200, 150) };
        var (host, _) = ShowForm(panel);
        using (host)
        {
            var inner = new Form { TopLevel = false, FormBorderStyle = FormBorderStyle.None, Bounds = new Rectangle(10, 10, 100, 80) };
            bool loaded = false, closed = false;
            inner.Load += (_, _) => loaded = true;
            inner.FormClosed += (_, _) => closed = true;
            panel.Controls.Add(inner);
            inner.Show();
            Assert.True(loaded);
            Assert.True(inner.Visible);
            Assert.False(inner.TopLevel);
            Assert.Same(panel, inner.Parent);
            inner.Close();
            Assert.True(closed);
            Assert.Empty(panel.Controls);
            Assert.True(inner.IsDisposed);
        }
        Assert.Throws<ArgumentException>(() => new Panel().Controls.Add(new Form()));
    }

#pragma warning disable WFDEV004 // the obsolete events are what is tested
    [Fact]
    public void ClosingAndClosedComeBeforeTheirFormCounterparts()
    {
        var (form, _) = ShowForm();
        var order = new List<string>();
        form.Closing += (_, _) => order.Add("Closing");
        form.FormClosing += (_, _) => order.Add("FormClosing");
        form.Closed += (_, _) => order.Add("Closed");
        form.FormClosed += (_, _) => order.Add("FormClosed");
        form.HandleDestroyed += (_, _) => order.Add("HandleDestroyed");
        form.Close();
        Assert.Equal(new[] { "Closing", "FormClosing", "Closed", "FormClosed", "HandleDestroyed" }, order);
    }
#pragma warning restore WFDEV004

    [Fact]
    public void AButtonShowsTheImageItsImageListNames()
    {
        using var red = new Bitmap(8, 8);
        using (var g = Graphics.FromImage(red)) g.Clear(Color.Red);
        var list = new ImageList();
        list.Images.Add("stop", red);
        var button = new Button { Bounds = new Rectangle(0, 0, 60, 30), ImageList = list, ImageKey = "stop", ImageAlign = ContentAlignment.MiddleLeft };
        Assert.Same(list.Images["stop"], button.Image);
        var (form, window) = ShowForm(button);
        using (form)
        {
            using var shot = window.Paint();
            Assert.Equal(Color.Red.ToArgb(), shot.GetPixel(7, 15).ToArgb());
        }
        button.ImageIndex = 0;
        Assert.Equal("", button.ImageKey);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        Assert.Equal(0, button.FlatAppearance.BorderSize);
    }

    [Fact]
    public void ApplicationContextEndsTheLoopWhenItsFormCloses()
    {
        TestPlatform.Install();
        var form = new Form();
        var context = new ApplicationContext(form);
        bool exited = false;
        context.ThreadExit += (_, _) => exited = true;
        form.Show();
        form.Close();
        Assert.True(exited);
    }

    /// <summary>Decision 146: <c>Images.Keys</c> is the BCL <c>StringCollection</c>, a copy with "" for an image without a key.</summary>
    [Fact]
    public void ImageKeysAreACopyInTheBclStringCollection()
    {
        var list = new ImageList();
        list.Images.Add("open", new Bitmap(16, 16));
        list.Images.Add(new Bitmap(16, 16));
        System.Collections.Specialized.StringCollection keys = list.Images.Keys;
        Assert.Equal(new[] { "open", "" }, keys.Cast<string>());
        keys.Add("more");
        Assert.Equal(2, list.Images.Keys.Count);
        Assert.Equal(typeof(System.Drawing.Drawing2D.FlushIntention), typeof(Graphics).GetMethod(nameof(Graphics.Flush), new[] { typeof(System.Drawing.Drawing2D.FlushIntention) })!.GetParameters()[0].ParameterType);
    }
}
