using NetForms.Design;
using Xunit;
using static NetForms.Tests.DesignerCodeReaderTests;

namespace NetForms.Tests;

/// <summary>
/// ToolStripContainer on the design surface (decision 178): its five panels are parts of it, as SplitContainer's
/// are; tool strips go into the side panels, joining the row under the drop, everything else into the content
/// panel; the code is what VS writes (the panels' contents, never where they are).
/// </summary>
public sealed class DesignerToolStripContainerTests
{
    public DesignerToolStripContainerTests() => TestPlatform.Install();

    private static DesignSurface HelloForms()
    {
        var path = Sample("HelloForms/MainForm.Designer.cs");
        return DesignSurface.Load(File.ReadAllText(path), File.ReadAllText(Path.ChangeExtension(Path.ChangeExtension(path, null), ".cs")));
    }

    private static DesignerOp Add(string type, string parent, int? x = null, int? y = null) =>
        new() { Op = "add", Type = "System.Windows.Forms." + type, Parent = parent, X = x, Y = y };

    private const string Top = "toolStripContainer1.TopToolStripPanel";

    private static DesignSurface WithContainer()
    {
        var s = HelloForms();
        s.Apply(new[] { new DesignerOp { Op = "add", Type = "System.Windows.Forms.ToolStripContainer", Parent = "", X = 10, Y = 40, W = 300, H = 200 } });
        return s;
    }

    private static string Code(DesignSurface s) => s.Source.Replace("this.", "").Replace("System.Drawing.", "").Replace("System.Windows.Forms.", "");

    [Fact]
    public void ANewContainerIsWrittenAsVSWritesIt()
    {
        using var s = WithContainer();
        var code = Code(s);
        Assert.Contains("toolStripContainer1.Text = \"toolStripContainer1\";", code);
        Assert.Contains("toolStripContainer1.ContentPanel.Size = new Size(", code);
        // Where the side panels are is the container's business.
        Assert.DoesNotContain("ToolStripPanel.Dock", code);
        Assert.DoesNotContain("ToolStripPanel.Location", code);
        Assert.DoesNotContain("ToolStripPanel.Size", code);
        Assert.DoesNotContain("ToolStripPanel.Visible", code);
        Assert.DoesNotContain("RowMargin", code);
        Assert.DoesNotContain("ToolStripPanelVisible", code);

        var view = s.Render();
        var panels = view.Items.Where(i => i.Parent == "toolStripContainer1").ToList();
        Assert.Equal(5, panels.Count);
        Assert.All(panels, p => Assert.Equal("nested", p.Kind));
        var top = panels.Single(p => p.Id == Top);
        Assert.True(top.Container && top.Strips);
        // While designed, an empty top panel keeps a band to drop a strip onto (25, as in VS), the others a thin one.
        Assert.Equal(25, top.H);
        Assert.Equal(8, panels.Single(p => p.Id == "toolStripContainer1.LeftToolStripPanel").W);
        var content = panels.Single(p => p.Id == "toolStripContainer1.ContentPanel");
        Assert.True(content.Container && !content.Strips);
        Assert.Equal(top.Y + 25, content.Y);
    }

    [Fact]
    public void AStripDroppedOnTheTopPanelJoinsItsFirstRow()
    {
        using var s = WithContainer();
        s.Apply(new[] { Add("ToolStrip", Top, 0, 5) });
        var code = Code(s);
        Assert.Contains($"{Top}.Controls.Add(toolStrip1);", code);
        Assert.Contains("toolStrip1.Dock = DockStyle.None;", code);
        Assert.Contains("toolStrip1.Location = new Point(3, 0);", code);
        Assert.Contains("toolStrip1.Size = new Size(111, 25);", code);
        Assert.Contains($"{Top}.SuspendLayout();", code);
        Assert.Contains($"{Top}.PerformLayout();", code);

        var view = s.Render();
        var strip = view.Items.Single(i => i.Id == "toolStrip1");
        Assert.Equal(Top, strip.Parent);
        Assert.True(strip.Movable && strip.Strip);
        // The panel grows to the strip (and its band past the last row), the content panel goes down.
        var top = view.Items.Single(i => i.Id == Top);
        Assert.Equal(25 + 8, top.H);
        Assert.Equal(top.Y + top.H, view.Items.Single(i => i.Id == "toolStripContainer1.ContentPanel").Y);

        // The same once read back.
        using var again = DesignSurface.Load(s.Source, s.CompanionSource);
        Assert.Equal(s.Source, again.Source);
    }

    [Fact]
    public void TheBandPastTheLastRowStartsANewRow()
    {
        using var s = WithContainer();
        s.Apply(new[] { Add("ToolStrip", Top, 0, 0) });
        s.Apply(new[] { Add("ToolStrip", Top, 0, 0) });
        var code = Code(s);
        Assert.Contains("toolStrip2.Location = new Point(114, 0);", code);

        // Moved onto the band: a second row.
        s.Apply(new[] { new DesignerOp { Op = "setBounds", Id = "toolStrip2", X = 3, Y = 28 } });
        Assert.Contains("toolStrip2.Location = new Point(3, 25);", Code(s));
        Assert.Equal(50 + 8, s.Render().Items.Single(i => i.Id == Top).H);

        // And back up, behind the first one.
        s.Apply(new[] { new DesignerOp { Op = "setBounds", Id = "toolStrip2", X = 150, Y = 10 } });
        Assert.Contains("toolStrip2.Location = new Point(150, 0);", Code(s));
    }

    [Fact]
    public void AStripMovedToTheLeftPanelStandsUp()
    {
        using var s = WithContainer();
        s.Apply(new[] { Add("ToolStrip", Top, 0, 0) });
        s.Apply(new[] { new DesignerOp { Op = "setParent", Id = "toolStrip1", Parent = "toolStripContainer1.LeftToolStripPanel", X = 2, Y = 0 } });
        var code = Code(s);
        Assert.Contains("toolStripContainer1.LeftToolStripPanel.Controls.Add(toolStrip1);", code);
        Assert.DoesNotContain($"{Top}.Controls.Add", code);
        Assert.Contains("toolStrip1.Location = new Point(0, 3);", code);
        var strip = s.Render().Items.Single(i => i.Id == "toolStrip1");
        Assert.True(strip.H > strip.W);
    }

    [Fact]
    public void OnlyStripsGoIntoTheSidePanelsAndNothingIntoTheContainerItself()
    {
        using var s = WithContainer();
        Assert.Throws<DesignerEditException>(() => s.Apply(new[] { Add("Button", Top, 0, 0) }));
        Assert.Throws<DesignerEditException>(() => s.Apply(new[] { Add("Button", "toolStripContainer1", 0, 0) }));
        Assert.Throws<DesignerEditException>(() => s.Apply(new[] { new DesignerOp { Op = "setParent", Id = "button1", Parent = Top, X = 0, Y = 0 } }));

        s.Apply(new[] { Add("Button", "toolStripContainer1.ContentPanel", 10, 10) });
        Assert.Contains("toolStripContainer1.ContentPanel.Controls.Add(button2);", Code(s));
    }

    [Fact]
    public void AHiddenPanelIsTheContainersProperty()
    {
        using var s = WithContainer();
        s.Apply(new[] { new DesignerOp { Op = "setProp", Id = "toolStripContainer1", Prop = "LeftToolStripPanelVisible", Value = "False" } });
        var code = Code(s);
        Assert.Contains("toolStripContainer1.LeftToolStripPanelVisible = false;", code);
        Assert.DoesNotContain("LeftToolStripPanel.Visible", code);
        Assert.False(s.Render().Items.Single(i => i.Id == "toolStripContainer1.LeftToolStripPanel").Visible);
    }

    [Fact]
    public void AVisualStudioFileReadsWithItsRows()
    {
        var source = """
            namespace Demo
            {
                partial class MainForm
                {
                    private System.ComponentModel.IContainer components = null;

                    protected override void Dispose(bool disposing)
                    {
                        if (disposing && (components != null)) components.Dispose();
                        base.Dispose(disposing);
                    }

                    private void InitializeComponent()
                    {
                        this.toolStripContainer1 = new System.Windows.Forms.ToolStripContainer();
                        this.toolStrip1 = new System.Windows.Forms.ToolStrip();
                        this.toolStrip2 = new System.Windows.Forms.ToolStrip();
                        this.toolStripContainer1.TopToolStripPanel.SuspendLayout();
                        this.toolStripContainer1.SuspendLayout();
                        this.SuspendLayout();
                        //
                        // toolStripContainer1
                        //
                        //
                        // toolStripContainer1.ContentPanel
                        //
                        this.toolStripContainer1.ContentPanel.Size = new System.Drawing.Size(284, 211);
                        this.toolStripContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
                        this.toolStripContainer1.Location = new System.Drawing.Point(0, 0);
                        this.toolStripContainer1.Name = "toolStripContainer1";
                        this.toolStripContainer1.Size = new System.Drawing.Size(284, 261);
                        this.toolStripContainer1.TabIndex = 0;
                        this.toolStripContainer1.Text = "toolStripContainer1";
                        //
                        // toolStripContainer1.TopToolStripPanel
                        //
                        this.toolStripContainer1.TopToolStripPanel.Controls.Add(this.toolStrip1);
                        this.toolStripContainer1.TopToolStripPanel.Controls.Add(this.toolStrip2);
                        //
                        // toolStrip1
                        //
                        this.toolStrip1.Dock = System.Windows.Forms.DockStyle.None;
                        this.toolStrip1.Location = new System.Drawing.Point(3, 0);
                        this.toolStrip1.Name = "toolStrip1";
                        this.toolStrip1.Size = new System.Drawing.Size(111, 25);
                        this.toolStrip1.TabIndex = 0;
                        //
                        // toolStrip2
                        //
                        this.toolStrip2.Dock = System.Windows.Forms.DockStyle.None;
                        this.toolStrip2.Location = new System.Drawing.Point(3, 25);
                        this.toolStrip2.Name = "toolStrip2";
                        this.toolStrip2.Size = new System.Drawing.Size(111, 25);
                        this.toolStrip2.TabIndex = 1;
                        //
                        // MainForm
                        //
                        this.ClientSize = new System.Drawing.Size(284, 261);
                        this.Controls.Add(this.toolStripContainer1);
                        this.Name = "MainForm";
                        this.toolStripContainer1.TopToolStripPanel.ResumeLayout(false);
                        this.toolStripContainer1.TopToolStripPanel.PerformLayout();
                        this.toolStripContainer1.ResumeLayout(false);
                        this.toolStripContainer1.PerformLayout();
                        this.ResumeLayout(false);
                    }

                    private System.Windows.Forms.ToolStripContainer toolStripContainer1;
                    private System.Windows.Forms.ToolStrip toolStrip1;
                    private System.Windows.Forms.ToolStrip toolStrip2;
                }
            }
            """;
        using var s = DesignSurface.Load(source, "namespace Demo { partial class MainForm : System.Windows.Forms.Form { public MainForm() { InitializeComponent(); } } }");
        var container = (ToolStripContainer)s.Model.Components.Single(c => c.Name == "toolStripContainer1").Instance;
        Assert.Equal(2, container.TopToolStripPanel.Rows.Length);
        var view = s.Render();
        Assert.Equal(new[] { 0, 25 }, new[] { "toolStrip1", "toolStrip2" }.Select(id => view.Items.Single(i => i.Id == id).Top));
        // Nothing moves on the next edit.
        s.Apply(new[] { new DesignerOp { Op = "setProp", Id = "toolStrip1", Prop = "Text", Value = "Main" } });
        var code = Code(s);
        Assert.Contains("toolStrip1.Location = new Point(3, 0);", code);
        Assert.Contains("toolStrip2.Location = new Point(3, 25);", code);
    }

    [Fact]
    public void TheToolboxMarksTheStrips()
    {
        var items = DesignerToolbox.Categories().SelectMany(c => c.Items).ToList();
        Assert.True(items.First(i => i.Name == "ToolStrip").Strip);
        Assert.True(items.First(i => i.Name == "MenuStrip").Strip);
        Assert.False(items.First(i => i.Name == "ContextMenuStrip").Strip);
        Assert.False(items.First(i => i.Name == "Button").Strip);
    }
}
