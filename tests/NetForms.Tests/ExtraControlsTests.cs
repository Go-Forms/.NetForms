using NetForms.Design;
using Xunit;
using static NetForms.Tests.DesignerCodeReaderTests;

namespace NetForms.Tests;

/// <summary>
/// The NetForms.ExtraControls package in the designer (decision 164): its build is scanned without running its code and
/// loaded as the designer loads any NuGet package's - live on the canvas, written to and read back from the form's
/// code. Its behaviour is tested in tests/NetForms.ExtraControls.Tests.
/// </summary>
public sealed class ExtraControlsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "netforms-extra-" + Guid.NewGuid().ToString("N"));

    public ExtraControlsTests()
    {
        TestPlatform.Install();
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    // --- the designer ------------------------------------------------------------------------------------

    /// <summary>The library's build output, copied to a folder of the test (as a project's bin with the package in it).</summary>
    private string LibraryBuild()
    {
        var config = AppContext.BaseDirectory.Contains($"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}") ? "Release" : "Debug";
        var bins = Path.Combine(RepoRoot, "src", "NetForms.ExtraControls", "bin");
        var output = new[] { config, config == "Release" ? "Debug" : "Release" }.Select(c => Path.Combine(bins, c, "net10.0"))
            .FirstOrDefault(d => File.Exists(Path.Combine(d, "NetForms.ExtraControls.dll")));
        Assert.True(output != null, "NetForms.ExtraControls is not built: " + Path.Combine(bins, config, "net10.0"));
        var bin = Path.Combine(_dir, "bin");
        Directory.CreateDirectory(bin);
        foreach (var f in Directory.GetFiles(output)) File.Copy(f, Path.Combine(bin, Path.GetFileName(f)));
        return Path.Combine(bin, "NetForms.ExtraControls.dll");
    }

    [Fact]
    public void TheScanFindsEveryControlWithItsIcon()
    {
        var report = Assert.Single(ControlLibraryScanner.Scan(new[] { LibraryBuild() }));
        Assert.Equal("ok", report.Verdict);
        Assert.True(report.NetForms);
        Assert.Equal(
            new[] { "CircularProgressBar", "ColorPickerButton", "CountdownTimer", "GradientPanel", "RatingStars", "ToggleSwitch" },
            report.Items.Select(i => i.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.All(report.Items, i => Assert.NotNull(i.Icon));
        Assert.True(report.Items.Single(i => i.Name == "CountdownTimer").Tray);
        Assert.False(report.Items.Single(i => i.Name == "GradientPanel").Tray);
    }

    [Fact]
    public void TheControlsAreLiveInTheDesignerAndRoundTripThroughTheCode()
    {
        var path = Path.Combine(_dir, "MainForm.Designer.cs");
        File.WriteAllText(path, """
            namespace App
            {
                partial class MainForm
                {
                    private System.ComponentModel.IContainer components = null;

                    private void InitializeComponent()
                    {
                        SuspendLayout();
                        ClientSize = new Size(500, 400);
                        Name = "MainForm";
                        Text = "Extras";
                        ResumeLayout(false);
                    }
                }
            }
            """);
        File.WriteAllText(Path.Combine(_dir, "MainForm.cs"), """
            namespace App
            {
                public partial class MainForm : Form
                {
                    public MainForm() { InitializeComponent(); }
                }
            }
            """);

        var dll = LibraryBuild();
        using var libraries = DesignerLibraries.Load(new[] { dll });
        Assert.Empty(libraries.Errors);
        using var surface = DesignSurface.Open(path, libraries);
        surface.Apply(new[]
        {
            new DesignerOp { Op = "add", Type = "NetForms.ExtraControls.GradientPanel", Parent = "", X = 10, Y = 10, W = 200, H = 150 },
            new DesignerOp { Op = "add", Type = "NetForms.ExtraControls.ToggleSwitch", Parent = "gradientPanel1", X = 10, Y = 10 },
            new DesignerOp { Op = "add", Type = "NetForms.ExtraControls.RatingStars", Parent = "", X = 220, Y = 10 },
            new DesignerOp { Op = "add", Type = "NetForms.ExtraControls.CircularProgressBar", Parent = "", X = 220, Y = 50 },
            new DesignerOp { Op = "add", Type = "NetForms.ExtraControls.ColorPickerButton", Parent = "", X = 220, Y = 160 },
            new DesignerOp { Op = "add", Type = "NetForms.ExtraControls.CountdownTimer" },
            new DesignerOp { Op = "setProp", Id = "toggleSwitch1", Prop = "Checked", Value = "True" },
            new DesignerOp { Op = "setProp", Id = "ratingStars1", Prop = "Value", Value = "4" },
            new DesignerOp { Op = "setProp", Id = "circularProgressBar1", Prop = "Value", Value = "70" },
            new DesignerOp { Op = "setProp", Id = "gradientPanel1", Prop = "GradientMode", Value = "Horizontal" },
            new DesignerOp { Op = "setProp", Id = "countdownTimer1", Prop = "Duration", Value = "00:05:00" },
        });

        var code = surface.Source;
        Assert.Contains("gradientPanel1 = new NetForms.ExtraControls.GradientPanel();", code);
        Assert.Contains("gradientPanel1.Controls.Add(toggleSwitch1);", code);
        Assert.Contains("toggleSwitch1.Checked = true;", code);
        Assert.Contains("ratingStars1.Value = 4;", code);
        Assert.Contains("circularProgressBar1.Value = 70;", code);
        Assert.Contains("gradientPanel1.GradientMode = System.Drawing.Drawing2D.LinearGradientMode.Horizontal;", code);
        Assert.Contains("countdownTimer1 = new NetForms.ExtraControls.CountdownTimer(components);", code);
        Assert.Contains("countdownTimer1.Duration = TimeSpan.Parse(\"00:05:00\");", code);
        Assert.DoesNotContain("circularProgressBar1.TabStop", code);
        Assert.DoesNotContain("colorPickerButton1.TextAlign", code);
        Assert.DoesNotContain("ratingStars1.Maximum", code); // its [DefaultValue]

        // The property grid shows the library's categories and descriptions; the Events tab its events.
        var checkedRow = surface.GetProperties("toggleSwitch1").Single(r => r.Name == "Checked");
        Assert.Equal("Appearance", checkedRow.Category);
        Assert.Equal("Whether the switch is on.", checkedRow.Description);
        Assert.Contains(surface.GetEvents("ratingStars1"), e => e.Name == "ValueChanged");

        var view = surface.Render();
        Assert.Contains(view.Tray, t => t.Id == "countdownTimer1");
        Assert.Contains(view.Items, i => i.Id == "toggleSwitch1" && i.Parent == "gradientPanel1");

        // Opened again from what was written: the same values, live.
        File.WriteAllText(path, code);
        using var again = DesignSurface.Open(path, libraries);
        var stars = again.Model.Find("ratingStars1")!;
        Assert.False(stars.IsPlaceholder);
        Assert.Equal(4, stars.Instance.GetType().GetProperty("Value")!.GetValue(stars.Instance));
        Assert.Equal(TimeSpan.FromMinutes(5), again.Model.Find("countdownTimer1")!.Instance.GetType().GetProperty("Duration")!.GetValue(again.Model.Find("countdownTimer1")!.Instance));
    }
}
