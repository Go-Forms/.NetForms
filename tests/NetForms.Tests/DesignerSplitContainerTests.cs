using NetForms.Design;
using Xunit;
using static NetForms.Tests.DesignerCodeReaderTests;

namespace NetForms.Tests;

/// <summary>
/// SplitContainer on the design surface (decision 180): its splitter is on the canvas to be dragged (the client sets
/// SplitterDistance), and what the file says is what stays - no edit moves the splitter by itself.
/// </summary>
public sealed class DesignerSplitContainerTests
{
    public DesignerSplitContainerTests() => TestPlatform.Install();

    private static DesignSurface HelloForms()
    {
        var path = Sample("HelloForms/MainForm.Designer.cs");
        return DesignSurface.Load(File.ReadAllText(path), File.ReadAllText(Path.ChangeExtension(Path.ChangeExtension(path, null), ".cs")));
    }

    private static string Code(DesignSurface s) => s.Source.Replace("this.", "").Replace("System.Drawing.", "").Replace("System.Windows.Forms.", "");

    [Fact]
    public void TheSplitterIsOnTheCanvasAndADragSetsTheDistance()
    {
        using var s = HelloForms();
        s.Apply(new[] { new DesignerOp { Op = "add", Type = "System.Windows.Forms.SplitContainer", Parent = "", X = 10, Y = 10, W = 250, H = 100 } });
        var item = s.Render().Items.Single(i => i.Id == "splitContainer1");
        // Drawn 250 wide, the default distance (50 of 150) grows with it, as in VS.
        Assert.Equal(new[] { 10 + 83, 10, 4, 100 }, item.Splitter);
        Assert.True(item.SplitterVertical);

        s.Apply(new[] { new DesignerOp { Op = "setProp", Id = "splitContainer1", Prop = "SplitterDistance", Value = "120" } });
        Assert.Contains("splitContainer1.SplitterDistance = 120;", Code(s));
        Assert.Equal(10 + 120, s.Render().Items.Single(i => i.Id == "splitContainer1").Splitter![0]);

        // Horizontal, the bar lies across.
        s.Apply(new[] { new DesignerOp { Op = "setProp", Id = "splitContainer1", Prop = "Orientation", Value = "Horizontal" } });
        item = s.Render().Items.Single(i => i.Id == "splitContainer1");
        Assert.False(item.SplitterVertical);
        Assert.Equal(250, item.Splitter![2]);

        // A fixed splitter is not offered.
        s.Apply(new[] { new DesignerOp { Op = "setProp", Id = "splitContainer1", Prop = "IsSplitterFixed", Value = "True" } });
        Assert.Null(s.Render().Items.Single(i => i.Id == "splitContainer1").Splitter);
    }

    [Fact]
    public void AVisualStudioFileKeepsItsSplitter()
    {
        var source = """
            namespace Demo
            {
                partial class MainForm
                {
                    private System.ComponentModel.IContainer components = null;

                    private void InitializeComponent()
                    {
                        this.splitContainer1 = new System.Windows.Forms.SplitContainer();
                        this.listBox1 = new System.Windows.Forms.ListBox();
                        ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
                        this.splitContainer1.Panel1.SuspendLayout();
                        this.splitContainer1.SuspendLayout();
                        this.SuspendLayout();
                        // 
                        // splitContainer1
                        // 
                        this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
                        this.splitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
                        this.splitContainer1.Location = new System.Drawing.Point(0, 0);
                        this.splitContainer1.Name = "splitContainer1";
                        // 
                        // splitContainer1.Panel1
                        // 
                        this.splitContainer1.Panel1.Controls.Add(this.listBox1);
                        this.splitContainer1.Panel1MinSize = 120;
                        this.splitContainer1.Panel2MinSize = 300;
                        this.splitContainer1.Size = new System.Drawing.Size(784, 461);
                        this.splitContainer1.SplitterDistance = 261;
                        this.splitContainer1.SplitterWidth = 6;
                        this.splitContainer1.TabIndex = 0;
                        // 
                        // listBox1
                        // 
                        this.listBox1.Dock = System.Windows.Forms.DockStyle.Fill;
                        this.listBox1.Location = new System.Drawing.Point(0, 0);
                        this.listBox1.Name = "listBox1";
                        this.listBox1.Size = new System.Drawing.Size(261, 461);
                        this.listBox1.TabIndex = 0;
                        // 
                        // MainForm
                        // 
                        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
                        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
                        this.ClientSize = new System.Drawing.Size(784, 461);
                        this.Controls.Add(this.splitContainer1);
                        this.Name = "MainForm";
                        this.splitContainer1.Panel1.ResumeLayout(false);
                        ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
                        this.splitContainer1.ResumeLayout(false);
                        this.ResumeLayout(false);
                    }

                    private System.Windows.Forms.SplitContainer splitContainer1;
                    private System.Windows.Forms.ListBox listBox1;
                }
            }
            """;
        using var s = DesignSurface.Load(source, "namespace Demo { partial class MainForm : System.Windows.Forms.Form { public MainForm() { InitializeComponent(); } } }");
        var split = (SplitContainer)s.Model.Components.Single(c => c.Name == "splitContainer1").Instance;
        Assert.Equal(261, split.SplitterDistance);
        Assert.Equal((120, 300, 6), (split.Panel1MinSize, split.Panel2MinSize, split.SplitterWidth));
        Assert.Equal(261, split.Panel1.Width);

        s.Apply(new[] { new DesignerOp { Op = "setProp", Id = "listBox1", Prop = "Sorted", Value = "True" } });
        Assert.Contains("splitContainer1.SplitterDistance = 261;", Code(s));
        // The form grown: Panel1 is fixed, so the splitter stays.
        s.Apply(new[] { new DesignerOp { Op = "setForm", W = 1000, H = 500 } });
        Assert.Contains("splitContainer1.SplitterDistance = 261;", Code(s));
    }
}
