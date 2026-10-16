using NetForms.Design;
using Xunit;
using static NetForms.Tests.DesignerCodeReaderTests;

namespace NetForms.Tests;

/// <summary>Smart tags (decision 186): what each control's Tasks panel offers, and its verbs.</summary>
public sealed class DesignerSmartTagTests
{
    public DesignerSmartTagTests() => TestPlatform.Install();

    private static DesignSurface HelloForms()
    {
        var path = Sample("HelloForms/MainForm.Designer.cs");
        return DesignSurface.Load(File.ReadAllText(path), File.ReadAllText(Path.ChangeExtension(Path.ChangeExtension(path, null), ".cs")));
    }

    private static DesignerOp Add(string type, string parent = "") => new() { Op = "add", Type = "System.Windows.Forms." + type, Parent = parent, X = 10, Y = 10 };

    private static DesignerOp Verb(string id, string verb) => new() { Op = "verb", Id = id, Value = verb };

    private static string Code(DesignSurface s) => s.Source.Replace("this.", "");

    private static List<DesignerSmartAction>? Tag(DesignSurface s, string id) => s.Render().Items.Single(i => i.Id == id).SmartTag;

    [Fact]
    public void EachControlOffersWhatItsVisualStudioDesignerOffers()
    {
        using var s = HelloForms();
        Assert.Null(Tag(s, "button1"));
        s.Apply(new[] { Add("TextBox"), Add("DataGridView"), Add("Panel") });
        Assert.Equal("Multiline", Assert.Single(Tag(s, "textBox1")!).Prop);
        var grid = Tag(s, "dataGridView1")!;
        Assert.Equal(new[] { "Choose Data Source", "Edit Columns…", "Enable Adding", "Enable Editing", "Enable Deleting", "Enable Column Reordering", "Dock in Parent Container" }, grid.Select(a => a.Label));
        Assert.True(grid.Single(a => a.Label == "Enable Editing").Invert);
        Assert.Equal("Columns", grid.Single(a => a.Label == "Edit Columns…").Edit);
        Assert.Equal("DockFill", Tag(s, "panel1")!.Single().Verb);

        s.Apply(new[] { Verb("panel1", "DockFill") });
        Assert.Contains("panel1.Dock = DockStyle.Fill;", Code(s));
        Assert.Equal("Undock", Tag(s, "panel1")!.Single().Verb);
        s.Apply(new[] { Verb("panel1", "Undock") });
        Assert.DoesNotContain("panel1.Dock", Code(s));
    }

    [Fact]
    public void TabsAndTableTracksAreAddedAndRemoved()
    {
        using var s = HelloForms();
        s.Apply(new[] { Add("TabControl"), Add("TableLayoutPanel") });
        s.Apply(new[] { Verb("tabControl1", "AddTab") });
        Assert.Contains("tabControl1.Controls.Add(tabPage3);", Code(s));
        Assert.Contains("tabPage3.Text = \"tabPage3\";", Code(s));
        s.Apply(new[] { Verb("tabControl1", "RemoveTab") });
        Assert.DoesNotContain("tabPage3", Code(s));

        s.Apply(new[] { Verb("tableLayoutPanel1", "AddColumn"), Verb("tableLayoutPanel1", "AddRow"), Verb("tableLayoutPanel1", "AddRow") });
        var table = (TableLayoutPanel)s.Model.Find("tableLayoutPanel1")!.Instance;
        Assert.Equal((3, 4), (table.ColumnCount, table.RowCount));
        s.Apply(new[] { Verb("tableLayoutPanel1", "RemoveRow") });
        Assert.Equal(3, ((TableLayoutPanel)s.Model.Find("tableLayoutPanel1")!.Instance).RowCount);
        Assert.Throws<DesignerEditException>(() => s.Apply(new[] { Verb("button1", "AddTab") }));
    }

    [Fact]
    public void AMenuGetsTheStandardItemsAndAStripIsEmbeddedInAToolStripContainer()
    {
        using var s = HelloForms();
        s.Apply(new[] { Add("MenuStrip"), Add("ToolStrip") });
        Assert.Contains(Tag(s, "menuStrip1")!, a => a.Verb == "InsertStandardItems");
        s.Apply(new[] { Verb("menuStrip1", "InsertStandardItems") });
        var code = Code(s);
        Assert.Contains("fileToolStripMenuItem.Text = \"&File\";", code);
        Assert.Contains("saveAsToolStripMenuItem.Text = \"Save &As\";", code);
        Assert.Contains("openToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.O;", code);
        Assert.Contains("aboutToolStripMenuItem.Text = \"&About...\";", code);
        Assert.DoesNotContain(Tag(s, "menuStrip1")!, a => a.Verb == "InsertStandardItems");

        s.Apply(new[] { Verb("toolStrip1", "EmbedInToolStripContainer") });
        code = Code(s);
        Assert.Contains("toolStripContainer1.Dock = DockStyle.Fill;", code);
        Assert.Contains("toolStripContainer1.TopToolStripPanel.Controls.Add(toolStrip1);", code);
        Assert.Contains("toolStripContainer1.TopToolStripPanel.Controls.Add(menuStrip1);", code);
        Assert.Contains("toolStripContainer1.ContentPanel.Controls.Add(button1);", code);
        Assert.Contains("toolStripContainer1.ContentPanel.Controls.Add(label1);", code);
        Assert.DoesNotContain(Tag(s, "toolStrip1")!, a => a.Verb == "EmbedInToolStripContainer");
        Assert.Contains(Tag(s, "toolStripContainer1")!, a => a.Prop == "TopToolStripPanelVisible");
    }
}
