using NetForms.Design;
using Xunit;
using static NetForms.Tests.DesignerCodeReaderTests;

namespace NetForms.Tests;

/// <summary>
/// The kinds of strip items (decision 177): the Items editor adds a combo box, text box, progress bar, label,
/// drop-down or split button, separator - what VS's Items Collection Editor offers for that strip - not only buttons
/// and menu items.
/// </summary>
public sealed class DesignerStripItemTests
{
    public DesignerStripItemTests() => TestPlatform.Install();

    private static DesignSurface WithStrips()
    {
        var path = Sample("HelloForms/MainForm.Designer.cs");
        var s = DesignSurface.Load(File.ReadAllText(path), File.ReadAllText(Path.ChangeExtension(Path.ChangeExtension(path, null), ".cs")));
        s.Apply(new[]
        {
            new DesignerOp { Op = "add", Type = "System.Windows.Forms.ToolStrip", Parent = "" },
            new DesignerOp { Op = "add", Type = "System.Windows.Forms.StatusStrip", Parent = "" },
            new DesignerOp { Op = "add", Type = "System.Windows.Forms.MenuStrip", Parent = "" },
        });
        return s;
    }

    [Fact]
    public void TheGridSaysWhatKindsOfItemsAStripTakes()
    {
        using var s = WithStrips();
        Assert.Equal(new[] { "ToolStripButton", "ToolStripLabel", "ToolStripSplitButton", "ToolStripDropDownButton", "ToolStripSeparator", "ToolStripComboBox", "ToolStripTextBox", "ToolStripProgressBar" },
            s.GetProperties("toolStrip1").Single(r => r.Name == "Items").NewTypes);
        Assert.Equal("ToolStripStatusLabel", s.GetProperties("statusStrip1").Single(r => r.Name == "Items").NewTypes![0]);
        Assert.Equal(new[] { "ToolStripMenuItem", "ToolStripComboBox", "ToolStripTextBox" }, s.GetProperties("menuStrip1").Single(r => r.Name == "Items").NewTypes);
    }

    [Fact]
    public void NewItemsAreOfTheKindPicked()
    {
        using var s = WithStrips();
        s.Apply(new[] { new DesignerOp { Op = "setItems", Id = "toolStrip1", Prop = "Items", Values = new[] { "Open", "Find", "", "", "Size" }, Ids = new[] { "", "+ToolStripComboBox", "+ToolStripSeparator", "+ToolStripProgressBar", "+ToolStripSplitButton" } } });
        var strip = (ToolStrip)s.Model.Find("toolStrip1")!.Instance;
        Assert.Equal(new[] { typeof(ToolStripButton), typeof(ToolStripComboBox), typeof(ToolStripSeparator), typeof(ToolStripProgressBar), typeof(ToolStripSplitButton) },
            strip.Items.Cast<ToolStripItem>().Select(i => i.GetType()));
        var source = s.Source.Replace("this.", "");
        Assert.Contains("toolStripComboBox1 = new ToolStripComboBox();", source);
        Assert.Contains("toolStripProgressBar1 = new ToolStripProgressBar();", source);
        Assert.Contains("toolStripSplitButton1.Text = \"Size\";", source);
        var row = s.GetProperties("toolStrip1").Single(r => r.Name == "Items");
        Assert.Equal("ToolStripComboBox", row.ItemTypes![1]);

        s.Apply(new[] { new DesignerOp { Op = "setItems", Id = "statusStrip1", Prop = "Items", Values = new[] { "Ready", "" }, Ids = new[] { "", "+ToolStripProgressBar" } } });
        Assert.Contains("toolStripStatusLabel1.Text = \"Ready\";", s.Source.Replace("this.", ""));
        Assert.Contains("toolStripProgressBar2 = new ToolStripProgressBar();", s.Source.Replace("this.", ""));
    }

    [Fact]
    public void AKindTheStripDoesNotTakeIsRefused()
    {
        using var s = WithStrips();
        var ex = Assert.Throws<DesignerEditException>(() => s.Apply(new[] { new DesignerOp { Op = "setItems", Id = "menuStrip1", Prop = "Items", Values = new[] { "x" }, Ids = new[] { "+ToolStripProgressBar" } } }));
        Assert.Contains("ToolStripMenuItem", ex.Message);
    }
}
