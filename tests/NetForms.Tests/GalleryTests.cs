using Gallery;
using Xunit;

namespace NetForms.Tests;

/// <summary>The Ф2 control gallery, rendered offscreen with the bundled font.</summary>
public class GalleryTests
{
    private static Bitmap RenderGallery(int tab)
    {
        var defaultFont = Application.DefaultFont;
        Application.SetDefaultFont(Golden.Font);
        try
        {
            using var form = new GalleryForm();
            var tabs = (TabControl)form.Controls["tabControl1"]!;
            tabs.SelectedIndex = tab;
            var bmp = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
            form.DrawToBitmap(bmp, new Rectangle(Point.Empty, form.ClientSize));
            return bmp;
        }
        finally
        {
            Application.SetDefaultFont(defaultFont);
        }
    }

    [Fact]
    public void ControlsPageGolden()
    {
        using var bmp = RenderGallery(0);
        Golden.Assert(bmp, "gallery-controls");
    }

    [Fact]
    public void LayoutPageGolden()
    {
        using var bmp = RenderGallery(1);
        Golden.Assert(bmp, "gallery-layout");
    }

    [Fact]
    public void PanelsPageGolden()
    {
        using var bmp = RenderGallery(2);
        Golden.Assert(bmp, "gallery-panels");
    }

    [Fact]
    public void DesignerCodeProducesTheExpectedTree()
    {
        using var form = new GalleryForm();
        var tabs = (TabControl)form.Controls["tabControl1"]!;
        Assert.Equal(3, tabs.TabCount);
        Assert.Equal(0, tabs.SelectedIndex);
        Assert.Same(tabs.TabPages[0], tabs.SelectedTab);
        Assert.Equal("Controls", tabs.SelectedTab!.Text);
        Assert.Equal(new Size(560, 316), tabs.Size);

        var group = (GroupBox)tabs.TabPages[0].Controls["groupBox1"]!;
        Assert.Equal(4, group.Controls.Count);
        Assert.True(((RadioButton)group.Controls["radioButton1"]!).Checked);
        Assert.Equal(CheckState.Checked, ((CheckBox)group.Controls["checkBox2"]!).CheckState);

        var split = (SplitContainer)tabs.TabPages[1].Controls["splitContainer1"]!;
        Assert.Equal(180, split.SplitterDistance);
        Assert.Equal(DockStyle.Fill, split.Dock);
        Assert.Equal(new Rectangle(0, 0, 180, split.Height), split.Panel1.Bounds);
        Assert.Equal(180 + split.SplitterWidth, split.Panel2.Left);
    }
}
