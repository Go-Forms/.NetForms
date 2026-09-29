using System.ComponentModel;
using System.Drawing.Drawing2D;
using NetForms.ExtraControls;
using NetForms.Platform;
using NetForms.Tests;
using Xunit;

namespace NetForms.ExtraControls.Tests;

/// <summary>The controls of the NetForms.ExtraControls package, driven through NetForms' window-less test platform.</summary>
public sealed class ControlsTests
{
    public ControlsTests() => TestPlatform.Install();

    private static (Form form, TestWindow window) Show(params Control[] controls)
    {
        var platform = TestPlatform.Install();
        var form = new Form { ClientSize = new Size(400, 300) };
        foreach (var c in controls) form.Controls.Add(c);
        form.Show();
        return (form, platform.Windows.Last());
    }

    private static void Key(TestWindow w, Keys key)
    {
        w.Host.KeyDown((int)key, InputModifiers.None);
        w.Host.KeyUp((int)key, InputModifiers.None);
    }

    // --- behaviour ---------------------------------------------------------------------------------------

    [Fact]
    public void ToggleSwitchTogglesOnClickAndSpace()
    {
        var toggle = new ToggleSwitch { Location = new Point(10, 10), Text = "Wi-Fi", Size = new Size(120, 24) };
        var (form, window) = Show(toggle);
        using (form)
        {
            int changes = 0;
            toggle.CheckedChanged += (_, _) => changes++;
            window.Click(new Point(20, 20));
            Assert.True(toggle.Checked);
            toggle.Focus();
            Key(window, Keys.Space);
            Assert.False(toggle.Checked);
            Assert.Equal(2, changes);

            using var bmp = new Bitmap(120, 24);
            toggle.Checked = true;
            toggle.DrawToBitmap(bmp, new Rectangle(0, 0, 120, 24));
            var track = bmp.GetPixel(8, 12); // the track, on: DodgerBlue (lighter while the mouse is over it)
            Assert.True(track.B > 200 && track.R < 150, track.ToString());
        }
    }

    [Fact]
    public void RatingStarsTakeClicksAndArrowKeys()
    {
        var stars = new RatingStars { Location = new Point(0, 0), Size = new Size(100, 20) };
        var (form, window) = Show(stars);
        using (form)
        {
            window.Click(new Point(65, 10)); // the fourth star (20 px each)
            Assert.Equal(4, stars.Value);
            window.Click(new Point(65, 10)); // the lit last star again: cleared
            Assert.Equal(0, stars.Value);
            Key(window, Keys.Right);
            Key(window, Keys.Right);
            Assert.Equal(2, stars.Value);
            Key(window, Keys.End);
            Assert.Equal(5, stars.Value);
            stars.Maximum = 3;
            Assert.Equal(3, stars.Value);
            Assert.Throws<ArgumentOutOfRangeException>(() => stars.Value = 4);
            stars.ReadOnly = true;
            window.Click(new Point(5, 10));
            Assert.Equal(3, stars.Value);
        }
    }

    [Fact]
    public void CircularProgressBarKeepsItsValueInItsBounds()
    {
        var ring = new CircularProgressBar();
        int changes = 0;
        ring.ValueChanged += (_, _) => changes++;
        ring.Value = 25;
        Assert.Equal(0.25, ring.Fraction);
        ring.Increment(100);
        Assert.Equal(100, ring.Value);
        Assert.Equal(2, changes);
        Assert.Throws<ArgumentOutOfRangeException>(() => ring.Value = 101);
        ring.Maximum = 50; // the value follows
        Assert.Equal(50, ring.Value);
        Assert.Throws<ArgumentOutOfRangeException>(() => ring.LineWidth = 0);

        using var bmp = new Bitmap(96, 96);
        ring.Size = new Size(96, 96);
        ring.DrawToBitmap(bmp, new Rectangle(0, 0, 96, 96));
        Assert.Equal(ring.ProgressColor.ToArgb(), bmp.GetPixel(48, 6).ToArgb()); // twelve o'clock is done
    }

    [Fact]
    public void GradientPanelPaintsItsGradientAndHoldsChildren()
    {
        var panel = new GradientPanel { Size = new Size(100, 100), StartColor = Color.Red, EndColor = Color.Blue, GradientMode = LinearGradientMode.Horizontal };
        panel.Controls.Add(new Label { Text = "Inside", Location = new Point(200, 200) });
        using var bmp = new Bitmap(100, 100);
        panel.DrawToBitmap(bmp, new Rectangle(0, 0, 100, 100));
        var left = bmp.GetPixel(1, 50);
        var right = bmp.GetPixel(98, 50);
        Assert.True(left.R > 200 && left.B < 60, $"left {left}");
        Assert.True(right.B > 200 && right.R < 60, $"right {right}");
        Assert.Single(panel.Controls);
    }

    private sealed class AnsweringPicker : ColorPickerButton
    {
        public Color? Answer;

        protected override Color? AskForColor(Color current) => Answer;
    }

    [Fact]
    public void ColorPickerButtonTakesTheColourTheDialogGives()
    {
        var button = new AnsweringPicker { Location = new Point(10, 10), Answer = Color.Teal };
        var (form, _) = Show(button);
        using (form)
        {
            int changes = 0;
            button.SelectedColorChanged += (_, _) => changes++;
            button.PerformClick();
            Assert.Equal(Color.Teal, button.SelectedColor);
            button.Answer = null; // cancelled
            button.PerformClick();
            Assert.Equal(Color.Teal, button.SelectedColor);
            Assert.Equal(1, changes);
        }
    }

    [Fact]
    public void CountdownTimerTicksDownAndFinishes()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var timer = new CountdownTimer { Duration = TimeSpan.FromSeconds(3), Now = () => now };
        int ticks = 0, finished = 0;
        timer.Tick += (_, _) => ticks++;
        timer.Finished += (_, _) => finished++;
        timer.Start();
        Assert.True(timer.Running);
        now = now.AddSeconds(1);
        timer.OnTimerTick();
        Assert.Equal(TimeSpan.FromSeconds(2), timer.Remaining);
        timer.Stop();
        now = now.AddSeconds(10); // paused: nothing passes
        Assert.Equal(TimeSpan.FromSeconds(2), timer.Remaining);
        timer.Start();
        now = now.AddSeconds(2);
        timer.OnTimerTick();
        Assert.False(timer.Running);
        Assert.Equal((2, 1), (ticks, finished));
        timer.Reset();
        Assert.Equal(TimeSpan.FromSeconds(3), timer.Remaining);

        var container = new Container();
        var inForm = new CountdownTimer(container);
        Assert.Same(container, inForm.Container);
        container.Dispose();
    }

    [Fact]
    public void DeclaredDefaultsMatchAFreshInstance()
    {
        var failures = new List<string>();
        foreach (var type in typeof(ToggleSwitch).Assembly.GetExportedTypes().Where(t => typeof(IComponent).IsAssignableFrom(t) && !t.IsAbstract))
        {
            using var instance = (IComponent)Activator.CreateInstance(type)!;
            foreach (PropertyDescriptor pd in TypeDescriptor.GetProperties(instance))
            {
                if (pd.ComponentType.Assembly != type.Assembly) continue;
                if (pd.Attributes[typeof(DefaultValueAttribute)] is not DefaultValueAttribute d) continue;
                if (!Equals(d.Value, pd.GetValue(instance))) failures.Add($"{type.Name}.{pd.Name}: [DefaultValue({d.Value})] but {pd.GetValue(instance)}");
            }
        }
        Assert.Empty(failures);
    }

    [Fact]
    public void TheGalleryRenders()
    {
        var panel = new GradientPanel { Location = new Point(12, 12), Size = new Size(220, 120), GradientMode = LinearGradientMode.ForwardDiagonal };
        panel.Controls.Add(new ToggleSwitch { Location = new Point(12, 12), Size = new Size(140, 24), Text = "Wi-Fi", Checked = true, ForeColor = Color.White });
        Assert.Equal(Color.Transparent, ((ToggleSwitch)panel.Controls[0]).BackColor);
        panel.Controls.Add(new ToggleSwitch { Location = new Point(12, 44), Size = new Size(140, 24), Text = "Bluetooth", ForeColor = Color.White });
        var form = new Form { ClientSize = new Size(460, 160), BackColor = Color.White };
        form.Controls.Add(panel);
        form.Controls.Add(new RatingStars { Location = new Point(250, 12), Size = new Size(150, 28), Value = 4 });
        form.Controls.Add(new CircularProgressBar { Location = new Point(250, 48), Size = new Size(96, 96), Value = 70 });
        form.Controls.Add(new ColorPickerButton { Location = new Point(12, 140 - 4), Size = new Size(140, 24), Text = "Colour", SelectedColor = Color.Crimson });
        form.ClientSize = new Size(460, 172);
        using var bmp = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
        form.DrawToBitmap(bmp, new Rectangle(Point.Empty, form.ClientSize));
        var outDir = Path.Combine(AppContext.BaseDirectory, "render-out");
        Directory.CreateDirectory(outDir);
        bmp.Save(Path.Combine(outDir, "extra-controls.png"));
        Assert.NotEqual(Color.White.ToArgb(), bmp.GetPixel(20, 20).ToArgb());
    }
}
