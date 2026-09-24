using HelloForms;
using SkiaSharp;
using Xunit;

namespace NetForms.Tests;

/// <summary>The sample form, exactly as the designer wrote it, rendered and driven without a window.</summary>
public class HelloFormsTests
{
    private static string OutputDir
    {
        get
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "render-out");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static Color Pixel(Bitmap bmp, int x, int y) => Color.FromArgb(bmp.GetPixel(x, y).ToArgb());

    /// <summary>What a color is painted as: system colors come from the tests' fixed palette (decision 105).</summary>
    private static int Painted(Color c) => HermeticRendering.Resolve(c).ToArgb();

    private static bool HasDarkPixels(Bitmap bmp, Rectangle area, Color background)
    {
        for (int y = area.Top; y < area.Bottom; y++)
            for (int x = area.Left; x < area.Right; x++)
            {
                var c = bmp.GetPixel(x, y);
                if (c.R + c.G + c.B < (background.R + background.G + background.B) - 150) return true;
            }
        return false;
    }

    [Fact]
    public void DesignerGeometryIsHonoured()
    {
        using var form = new MainForm();
        Assert.Equal(new Size(284, 140), form.ClientSize);
        Assert.Equal("HelloForms", form.Text);

        var button = Assert.IsType<Button>(form.Controls["button1"]);
        var label = Assert.IsType<Label>(form.Controls["label1"]);
        Assert.Equal(new Rectangle(197, 105, 75, 23), button.Bounds);
        Assert.Equal(AnchorStyles.Bottom | AnchorStyles.Right, button.Anchor);
        Assert.Equal(new Point(12, 9), label.Location);
        Assert.True(label.AutoSize);
        // Designer z-order: label1 was added first, so it is in front.
        Assert.Same(label, form.Controls[0]);
        Assert.Same(button, form.Controls[1]);
    }

    [Fact]
    public void RendersOffscreenIntoSkBitmap()
    {
        using var form = new MainForm();
        using var bmp = new Bitmap(284, 140);
        form.DrawToBitmap(bmp, new Rectangle(0, 0, 284, 140));
        bmp.Save(Path.Combine(OutputDir, "helloforms.png"));

        SKBitmap sk = bmp.Skia;
        Assert.Equal(284, sk.Width);
        Assert.Equal(140, sk.Height);

        // Form background where nothing else is drawn.
        Assert.Equal(Painted(form.BackColor), Pixel(bmp, 150, 70).ToArgb());
        // Button face and border.
        Assert.Equal(Painted(Theme.ButtonFace), Pixel(bmp, 197 + 37, 105 + 3).ToArgb());
        Assert.Equal(Painted(Theme.ButtonBorder), Pixel(bmp, 197, 105).ToArgb());
        Assert.Equal(Painted(Theme.ButtonBorder), Pixel(bmp, 197 + 74, 105 + 22).ToArgb());
        // Text was drawn in the label and in the button.
        var label = form.Controls["label1"]!;
        Assert.True(HasDarkPixels(bmp, label.Bounds, form.BackColor), "label text missing");
        Assert.True(HasDarkPixels(bmp, new Rectangle(200, 108, 69, 17), Theme.ButtonFace), "button text missing");
    }

    [Fact]
    public void LabelAutoSizesToItsText()
    {
        using var form = new MainForm();
        var label = form.Controls["label1"]!;
        var before = label.Size;
        Assert.True(before.Width > 0 && before.Height > 0);

        label.Text = "A considerably longer piece of text";
        Assert.True(label.Width > before.Width, $"label did not grow: {before.Width} -> {label.Width}");
        Assert.Equal(before.Height, label.Height);
    }

    [Fact]
    public void ButtonSlidesWhenTheFormIsResized()
    {
        using var form = new MainForm();
        var button = form.Controls["button1"]!;
        var label = form.Controls["label1"]!;

        form.ClientSize = new Size(484, 240);
        Assert.Equal(new Point(397, 205), button.Location);
        Assert.Equal(new Size(75, 23), button.Size);
        Assert.Equal(new Point(12, 9), label.Location);

        using var bmp = new Bitmap(484, 240);
        form.DrawToBitmap(bmp, new Rectangle(0, 0, 484, 240));
        bmp.Save(Path.Combine(OutputDir, "helloforms-resized.png"));
        Assert.Equal(Painted(Theme.ButtonBorder), Pixel(bmp, 397, 205).ToArgb());
        Assert.Equal(Painted(form.BackColor), Pixel(bmp, 197, 105).ToArgb());
    }

    [Fact]
    public void ClickThroughThePlatformChangesTheLabel()
    {
        var platform = TestPlatform.Install();
        using var form = new MainForm();
        var button = form.Controls["button1"]!;
        var label = form.Controls["label1"]!;

        var events = new List<string>();
        button.MouseDown += (_, _) => events.Add("MouseDown");
        button.Click += (_, _) => events.Add("Click");
        button.MouseClick += (_, _) => events.Add("MouseClick");
        button.MouseUp += (_, _) => events.Add("MouseUp");
        button.GotFocus += (_, _) => events.Add("GotFocus");

        form.Show();
        var window = platform.Windows.Last();
        Assert.Equal(new Size(284, 140), window.ClientSize);
        Assert.Equal("HelloForms", window.Title);

        window.Click(new Point(197 + 30, 105 + 10));
        Assert.Equal("Hello from NetForms! Clicked 1 time(s).", label.Text);
        // Focus moves on mouse-down (the native button class does the same), then the WinForms click sequence.
        Assert.Equal(new[] { "GotFocus", "MouseDown", "Click", "MouseClick", "MouseUp" }, events);
        Assert.True(button.Focused);

        // A click outside the button does nothing.
        window.Click(new Point(50, 70));
        Assert.Equal("Hello from NetForms! Clicked 1 time(s).", label.Text);

        // Resizing through the window slides the button, and the next click still lands.
        window.Resize(484, 240);
        Assert.Equal(new Point(397, 205), button.Location);
        window.Click(new Point(397 + 30, 205 + 10));
        Assert.Equal("Hello from NetForms! Clicked 2 time(s).", label.Text);

        using var painted = window.Paint();
        // The button has focus now, so it wears the accent border.
        Assert.Equal(Painted(Theme.ButtonBorderDefault), Pixel(painted, 397, 205).ToArgb());
        Assert.Equal(Painted(form.BackColor), Pixel(painted, 197, 105).ToArgb());
        form.Close();
        Assert.True(form.IsDisposed);
    }

    [Fact]
    public void FormLifecycleEventsFireInOrder()
    {
        TestPlatform.Install();
        var form = new MainForm();
        var events = new List<string>();
        form.Load += (_, _) => events.Add("Load");
        form.Shown += (_, _) => events.Add("Shown");
        form.Activated += (_, _) => events.Add("Activated");
        form.Resize += (_, _) => events.Add("Resize");
        form.FormClosing += (_, e) => { events.Add("FormClosing:" + e.CloseReason); };
        form.FormClosed += (_, _) => events.Add("FormClosed");

        form.Show();
        form.ClientSize = new Size(300, 200);
        form.Close();

        Assert.Equal(new[] { "Load", "Shown", "Activated", "Resize", "FormClosing:UserClosing", "FormClosed" }, events);
    }

    [Fact]
    public void FormClosingCanBeCancelled()
    {
        TestPlatform.Install();
        using var form = new MainForm();
        bool cancel = true;
        form.FormClosing += (_, e) => e.Cancel = cancel;
        form.Show();

        form.Close();
        Assert.True(form.Visible);
        Assert.False(form.IsDisposed);

        cancel = false;
        form.Close();
        Assert.True(form.IsDisposed);
    }

    [Fact]
    public void KeyboardActivatesTheFocusedButton()
    {
        var platform = TestPlatform.Install();
        using var form = new MainForm();
        var label = form.Controls["label1"]!;
        form.Show();
        var window = platform.Windows.Last();

        // The button is the only tab stop, so it has focus after activation; Space clicks it.
        Assert.True(form.Controls["button1"]!.Focused);
        window.Host.KeyDown((int)Keys.Space, NetForms.Platform.InputModifiers.None);
        window.Host.KeyUp((int)Keys.Space, NetForms.Platform.InputModifiers.None);
        Assert.Equal("Hello from NetForms! Clicked 1 time(s).", label.Text);

        window.Host.KeyDown((int)Keys.Return, NetForms.Platform.InputModifiers.None);
        window.Host.KeyUp((int)Keys.Return, NetForms.Platform.InputModifiers.None);
        Assert.Equal("Hello from NetForms! Clicked 2 time(s).", label.Text);
    }
}
