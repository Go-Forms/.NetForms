using HelloForms;
using NetForms.Platform;
using Xunit;

namespace NetForms.Tests;

/// <summary>Ф1 core: timers, validation order, modal dialogs, MessageBox, cursors, dirty-rect invalidation.</summary>
public class CoreBehaviourTests
{
    [Fact]
    public void TimerTicksOnThePlatformTimer()
    {
        var platform = TestPlatform.Install();
        int ticks = 0;
        using var timer = new System.Windows.Forms.Timer { Interval = 250 };
        timer.Tick += (_, _) => ticks++;
        Assert.Empty(platform.Timers);

        timer.Start();
        var pt = Assert.Single(platform.Timers);
        Assert.Equal(TimeSpan.FromMilliseconds(250), pt.Interval);
        Assert.True(pt.IsRunning);

        pt.Fire();
        pt.Fire();
        Assert.Equal(2, ticks);

        timer.Stop();
        pt.Fire();
        Assert.Equal(2, ticks);
        Assert.False(timer.Enabled);
    }

    [Fact]
    public void FocusChangeRaisesLeaveValidatingValidatedEnterLostFocusGotFocus()
    {
        var platform = TestPlatform.Install();
        using var form = new Form { ClientSize = new Size(200, 100) };
        var a = new Button { Name = "a", Bounds = new Rectangle(10, 10, 50, 20), TabIndex = 0 };
        var b = new Button { Name = "b", Bounds = new Rectangle(10, 40, 50, 20), TabIndex = 1 };
        form.Controls.Add(a);
        form.Controls.Add(b);

        var events = new List<string>();
        foreach (var c in new[] { a, b })
        {
            c.Enter += (s, _) => events.Add(((Control)s!).Name + ".Enter");
            c.Leave += (s, _) => events.Add(((Control)s!).Name + ".Leave");
            c.GotFocus += (s, _) => events.Add(((Control)s!).Name + ".GotFocus");
            c.LostFocus += (s, _) => events.Add(((Control)s!).Name + ".LostFocus");
            c.Validating += (s, _) => events.Add(((Control)s!).Name + ".Validating");
            c.Validated += (s, _) => events.Add(((Control)s!).Name + ".Validated");
        }

        form.Show();
        Assert.Equal(new[] { "a.Enter", "a.GotFocus" }, events);
        events.Clear();

        // Focus(): Win32 moves the focus first, so a loses it before the ContainerControl events
        // (as real WinForms does, CompatScenarios focus/b-focus).
        b.Focus();
        Assert.Equal(new[] { "a.LostFocus", "a.Leave", "a.Validating", "a.Validated", "b.Enter", "b.GotFocus" }, events);
        Assert.Same(b, form.ActiveControl);
        events.Clear();

        // Select() goes through ContainerControl.UpdateFocusedControl before the window focus moves.
        a.Select();
        Assert.Equal(new[] { "b.Leave", "b.Validating", "b.Validated", "a.Enter", "b.LostFocus", "a.GotFocus" }, events);
        events.Clear();
        b.Select();
        events.Clear();

        // A cancelled Validating keeps the focus on b.
        b.Validating += (_, e) => e.Cancel = true;
        a.Select();
        Assert.Equal(new[] { "b.Leave", "b.Validating", "b.Enter" }, events);
        Assert.True(b.Focused);
        Assert.Same(b, form.ActiveControl);
    }

    [Fact]
    public void TabKeyWalksTheTabOrderAndShowsFocusCues()
    {
        var platform = TestPlatform.Install();
        using var form = new Form { ClientSize = new Size(200, 100) };
        var first = new Button { Bounds = new Rectangle(10, 10, 50, 20), TabIndex = 0 };
        var second = new Button { Bounds = new Rectangle(10, 40, 50, 20), TabIndex = 1 };
        var skipped = new Button { Bounds = new Rectangle(10, 70, 50, 20), TabIndex = 2, TabStop = false };
        form.Controls.Add(skipped);
        form.Controls.Add(second);
        form.Controls.Add(first);
        form.Show();
        var window = platform.Windows.Last();

        Assert.True(first.Focused);
        Assert.False(first.ShowFocusCues);

        window.Host.KeyDown((int)Keys.Tab, InputModifiers.None);
        Assert.True(second.Focused);
        Assert.True(second.ShowFocusCues);

        window.Host.KeyDown((int)Keys.Tab, InputModifiers.None);
        Assert.True(first.Focused);

        window.Host.KeyDown((int)Keys.Tab, InputModifiers.Shift);
        Assert.True(second.Focused);
    }

    [Fact]
    public void ShowDialogRunsANestedLoopAndReturnsTheDialogResult()
    {
        var platform = TestPlatform.Install();
        using var owner = new MainForm();
        owner.Show();

        using var dialog = new Form { ClientSize = new Size(120, 60), Text = "Ask" };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Bounds = new Rectangle(10, 10, 60, 23) };
        dialog.Controls.Add(ok);
        var closed = new List<string>();
        dialog.FormClosed += (_, e) => closed.Add(e.CloseReason.ToString());

        platform.OnMessageLoop = () =>
        {
            var w = platform.Windows.Last();
            Assert.True(w.IsModal);
            Assert.Same(dialog, Form.ActiveForm);
            w.Click(new Point(20, 20));
        };
        var result = dialog.ShowDialog(owner);

        Assert.Equal(DialogResult.OK, result);
        Assert.Equal(new[] { "UserClosing" }, closed);
        Assert.False(dialog.Visible);
        Assert.False(dialog.IsDisposed);
        Assert.Contains(owner, Application.OpenForms);
        Assert.DoesNotContain(dialog, Application.OpenForms);
    }

    [Fact]
    public void MessageBoxReturnsTheClickedButton()
    {
        var platform = TestPlatform.Install();
        platform.OnMessageLoop = () =>
        {
            var w = platform.Windows.Last();
            Assert.Equal("Question", w.Title);
            // Second button of Yes/No is "No".
            var box = Application.OpenForms[Application.OpenForms.Count - 1];
            var no = box.Controls.OfType<Button>().Single(b => b.DialogResult == DialogResult.No);
            w.Click(new Point(no.Left + 5, no.Top + 5));
        };
        var result = MessageBox.Show("Save changes to the document?", "Question", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        Assert.Equal(DialogResult.No, result);
    }

    [Fact]
    public void MessageBoxEnterAndEscapeUseDefaultAndCancelButtons()
    {
        var platform = TestPlatform.Install();
        platform.OnMessageLoop = () => platform.Windows.Last().Host.KeyDown((int)Keys.Escape, InputModifiers.None);
        Assert.Equal(DialogResult.Cancel, MessageBox.Show("text", "caption", MessageBoxButtons.OKCancel));

        platform.OnMessageLoop = () => platform.Windows.Last().Host.KeyDown((int)Keys.Return, InputModifiers.None);
        Assert.Equal(DialogResult.Retry, MessageBox.Show("text", "caption", MessageBoxButtons.AbortRetryIgnore, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2));
    }

    [Fact]
    public void MessageBoxGolden()
    {
        var platform = TestPlatform.Install();
        Bitmap? shot = null;
        platform.OnMessageLoop = () =>
        {
            var w = platform.Windows.Last();
            shot = w.Paint();
            w.Host.KeyDown((int)Keys.Escape, InputModifiers.None);
        };
        var defaultFont = Application.DefaultFont;
        Application.SetDefaultFont(Golden.Font);
        try
        {
            MessageBox.Show("The file has been modified.\nDo you want to save the changes?", "Editor", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
        }
        finally
        {
            Application.SetDefaultFont(defaultFont);
        }
        Assert.NotNull(shot);
        Golden.Assert(shot!, "messagebox");
    }

    [Fact]
    public void CursorOfTheControlUnderThePointerReachesTheWindow()
    {
        var platform = TestPlatform.Install();
        using var form = new Form { ClientSize = new Size(200, 100) };
        var hand = new Label { Bounds = new Rectangle(10, 10, 50, 20), Cursor = Cursors.Hand };
        form.Controls.Add(hand);
        form.Show();
        var window = platform.Windows.Last();

        window.Host.MouseMove(new Point(20, 20), MouseButton.None, InputModifiers.None);
        Assert.Equal("Hand", window.CursorName);
        window.Host.MouseMove(new Point(150, 80), MouseButton.None, InputModifiers.None);
        Assert.Equal("Default", window.CursorName);
    }

    [Fact]
    public void InvalidateReportsTheControlRectangleInWindowCoordinates()
    {
        var platform = TestPlatform.Install();
        using var form = new MainForm();
        form.Show();
        var window = platform.Windows.Last();
        window.Invalidated.Clear();

        var button = form.Controls["button1"]!;
        button.Invalidate();
        Assert.Contains(new Rectangle(197, 105, 75, 23), window.Invalidated);
    }

    [Fact]
    public void PaintingOnlyTheClipLeavesTheRestOfTheBufferAlone()
    {
        var platform = TestPlatform.Install();
        using var form = new MainForm();
        form.Show();
        var window = platform.Windows.Last();
        using var bmp = new Bitmap(284, 140);
        using (var g = Graphics.FromImage(bmp)) g.Clear(Color.Magenta);
        using (var canvas = new SkiaSharp.SKCanvas(bmp.Skia))
        {
            var clip = new Rectangle(190, 100, 90, 35);
            canvas.ClipRect(new SkiaSharp.SKRect(clip.Left, clip.Top, clip.Right, clip.Bottom));
            window.Host.Paint(canvas, new Size(284, 140), clip);
        }
        Assert.Equal(Color.Magenta.ToArgb(), bmp.GetPixel(10, 10).ToArgb());
        Assert.NotEqual(Color.Magenta.ToArgb(), bmp.GetPixel(200, 110).ToArgb());
    }

    [Fact]
    public void SynchronizationContextPostsToThePlatform()
    {
        TestPlatform.Install();
        var ctx = new WindowsFormsSynchronizationContext();
        bool ran = false;
        ctx.Post(_ => ran = true, null);
        Assert.True(ran);
        int value = 0;
        ctx.Send(s => value = (int)s!, 42);
        Assert.Equal(42, value);
    }

    /// <summary>
    /// A form that starts maximized must already report the maximized size when Load runs - real
    /// WinForms creates the handle maximized, and ported code sizes its bitmaps from Width/Height
    /// in Load (samples/GenLabs does exactly that).
    /// </summary>
    [Fact]
    public void AMaximizedFormReportsItsRealSizeInLoad()
    {
        TestPlatform.Install();
        var working = Screen.PrimaryScreen.WorkingArea;

        Size atLoad = Size.Empty;
        Size pictureAtLoad = Size.Empty;
        using var form = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            ClientSize = new Size(594, 505),
            WindowState = FormWindowState.Maximized,
        };
        var picture = new PictureBox { Dock = DockStyle.Fill };
        form.Controls.Add(picture);
        form.Load += (_, _) =>
        {
            atLoad = form.Size;
            pictureAtLoad = picture.Size;
        };
        form.Show();

        Assert.Equal(working.Size, atLoad);
        Assert.Equal(working.Size, form.Size);
        // A Dock.Fill child has the same size in Load as it will have on screen, so a bitmap
        // allocated there fits the control instead of lagging a layout behind.
        Assert.Equal(working.Size, pictureAtLoad);
    }

    [Fact]
    public void AutoSizeGrowsTheFormAroundItsContent()
    {
        TestPlatform.Install();
        using var image = new Bitmap(400, 300);
        var picture = new PictureBox { Location = new Point(10, 10), SizeMode = PictureBoxSizeMode.AutoSize, Image = image };

        using var form = new Form { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        form.Controls.Add(picture);
        form.Show();

        Assert.Equal(new Size(400, 300), picture.Size);
        Assert.Equal(new Size(413, 313), form.ClientSize);

        // A bigger image grows the picture box, and the form follows it.
        using var bigger = new Bitmap(640, 480);
        picture.Image = bigger;
        Assert.Equal(new Size(640, 480), picture.Size);
        Assert.Equal(new Size(653, 493), form.ClientSize);
    }
}
