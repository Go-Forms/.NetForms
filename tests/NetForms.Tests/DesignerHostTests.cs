using System.Text.Json;
using System.Text.Json.Nodes;
using NetForms.Design;
using NetForms.Design.Serialization;
using Xunit;
using static NetForms.Tests.DesignerCodeReaderTests;

namespace NetForms.Tests;

/// <summary>
/// Ф5.3: the designer host. The gate: protocol scenarios without any UI - open a sample, edit it the
/// way the canvas would, save, read the file back and find exactly what was done.
/// </summary>
public sealed class DesignerHostTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "netforms-host-" + Guid.NewGuid().ToString("N"));

    public DesignerHostTests()
    {
        TestPlatform.Install();
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    /// <summary>A copy of a sample's form files in a temp folder; returns the designer file's path.</summary>
    private string CopySample(string designerFile)
    {
        var source = Sample(designerFile);
        var target = Path.Combine(_dir, Path.GetFileName(source));
        File.Copy(source, target);
        var companion = DesignerCodeReader.CompanionPath(source);
        if (companion != null && File.Exists(companion)) File.Copy(companion, DesignerCodeReader.CompanionPath(target)!);
        var resx = ResxResources.ResxPathOf(source);
        if (resx != null && File.Exists(resx)) File.Copy(resx, ResxResources.ResxPathOf(target)!);
        return target;
    }

    private static DesignerOp Op(string op, string? id = null) => new() { Op = op, Id = id };

    private static void LayOut(Control control)
    {
        control.PerformLayout();
        foreach (Control child in control.Controls) LayOut(child);
    }

    // --- the gate --------------------------------------------------------------------------------

    [Fact]
    public void MovingAControlInGallerySavesAndReadsBackTheSameModel()
    {
        var path = CopySample("Gallery/GalleryForm.Designer.cs");
        using (var surface = DesignSurface.Open(path))
        {
            surface.AutoSave = true;
            surface.Apply(new[] { new DesignerOp { Op = "setBounds", Id = "button1", X = 220, Y = 160 } });
            Assert.False(surface.IsDirty);
        }

        var model = new DesignerCodeReader().ReadFile(path);
        Assert.Equal(new Point(220, 160), ((Control)model.Find("button1")!.Instance).Location);
        // Everything else is what the file said before (the first save names Gallery's inline objects).
        var original = new DesignerCodeReader().ReadFile(Sample("Gallery/GalleryForm.Designer.cs"));
        // Both laid out: the file's tab page bounds are what VS computed with Segoe UI, and a machine
        // without it (Linux) lays the pages out a little differently - on both sides alike.
        LayOut((Control)original.Root.Instance);
        LayOut((Control)model.Root.Instance);
        foreach (var c in original.Components.Where(c => c.Name != null && c.Instance is Control && c.Name != "button1"))
            Assert.Equal(((Control)c.Instance).Bounds, ((Control)model.Find(c.Name!)!.Instance).Bounds);
    }

    [Theory]
    [MemberData(nameof(SampleForms), MemberType = typeof(DesignerCodeReaderTests))]
    public void EverySampleOpensAndRenders(string designerFile, Type formType)
    {
        _ = formType;
        using var surface = DesignSurface.Open(CopySample(designerFile));
        var view = surface.Render();
        Assert.Equal(surface.Model.ClassName, view.ClassName);
        Assert.True(view.Width > 0 && view.Height > 0);
        Assert.NotEmpty(Convert.FromBase64String(view.Png));
        // Every named control of the form is on the canvas, with its id.
        // (Inline objects of a hand-written file have no name, hence no id, until the first save.)
        foreach (var c in surface.Model.Components.Where(c => c.Name != null && c.Instance is Control and not ToolStripDropDown))
            Assert.Contains(view.Items, i => i.Id == c.Name);
    }

    // --- editing ---------------------------------------------------------------------------------

    [Fact]
    public void AddedControlsGetTheDefaultsOfTheVisualStudioDesigner()
    {
        using var surface = DesignSurface.Open(CopySample("HelloForms/MainForm.Designer.cs"));
        surface.Apply(new[]
        {
            new DesignerOp { Op = "add", Type = "System.Windows.Forms.CheckBox", X = 12, Y = 40 },
            new DesignerOp { Op = "add", Type = "TabControl", X = 12, Y = 70, W = 200, H = 60 },
            new DesignerOp { Op = "add", Type = "Timer" },
        });
        var model = surface.Model;
        var check = (CheckBox)model.Find("checkBox1")!.Instance;
        Assert.Equal("checkBox1", check.Text);
        Assert.True(check.AutoSize);
        Assert.True(check.UseVisualStyleBackColor);
        Assert.Equal(2, check.TabIndex);
        // On top of the z-order, as a control dropped in VS is.
        Assert.Same(model.Find("tabControl1")!.Instance, ((Form)model.Root.Instance).Controls[0]);

        var tabs = (TabControl)model.Find("tabControl1")!.Instance;
        Assert.Equal(new[] { "tabPage1", "tabPage2" }, tabs.TabPages.Cast<TabPage>().Select(p => p.Text));
        Assert.True(tabs.TabPages[0].UseVisualStyleBackColor);

        Assert.Contains(surface.Render().Tray, t => t.Id == "timer1");
        Assert.Contains("timer1 = new System.Windows.Forms.Timer(components);", surface.Source);
        Assert.Contains("components = new System.ComponentModel.Container();", surface.Source);
    }

    [Fact]
    public void AnAddedErrorProviderIsWrittenAsVisualStudioWritesIt()
    {
        using var surface = DesignSurface.Open(CopySample("HelloForms/MainForm.Designer.cs"));
        surface.Apply(new[] { new DesignerOp { Op = "add", Type = "ErrorProvider" } });
        Assert.Contains(surface.Render().Tray, t => t.Id == "errorProvider1");
        Assert.Contains("errorProvider1 = new ErrorProvider(components);", surface.Source);
        Assert.Contains("((System.ComponentModel.ISupportInitialize)errorProvider1).BeginInit();", surface.Source);
        Assert.Contains("errorProvider1.ContainerControl = this;", surface.Source);
        Assert.Contains("((System.ComponentModel.ISupportInitialize)errorProvider1).EndInit();", surface.Source);
        // The file is re-read after every edit (decision 80): the reference to the form survives it.
        var provider = (ErrorProvider)surface.Model.Find("errorProvider1")!.Instance;
        Assert.Same(surface.Model.Root.Instance, provider.ContainerControl);
    }

    [Fact]
    public void RemovingAControlTakesItsChildrenAndReferencesWithIt()
    {
        using var surface = DesignSurface.Open(CopySample("Strips/MainForm.Designer.cs"));
        surface.Apply(new[] { Op("remove", "menuStrip1"), Op("remove", "contextMenuStrip1") });
        var model = surface.Model;
        Assert.Null(model.Find("menuStrip1"));
        Assert.Null(model.Find("fileMenu"));      // an item of the menu
        Assert.Null(model.Find("recentAMenuItem")); // an item of an item
        Assert.Null(((Form)model.Root.Instance).MainMenuStrip);
        Assert.Null(((TextBox)model.Find("editor")!.Instance).ContextMenuStrip);
        Assert.DoesNotContain("menuStrip1", surface.Source);
        Assert.DoesNotContain("contextMenuStrip1", surface.Source);
    }

    [Fact]
    public void UndoAndRedoRestoreBothFiles()
    {
        var path = CopySample("HelloForms/MainForm.Designer.cs");
        var designer = File.ReadAllText(path);
        var code = File.ReadAllText(DesignerCodeReader.CompanionPath(path)!);
        using var surface = DesignSurface.Open(path);
        surface.AutoSave = true;
        surface.Apply(new[] { new DesignerOp { Op = "setEvent", Id = "label1", Event = "Click", Handler = "label1_Click" } });
        Assert.Contains("private void label1_Click(object sender, EventArgs e)", File.ReadAllText(DesignerCodeReader.CompanionPath(path)!));
        Assert.Contains("label1.Click += label1_Click;", File.ReadAllText(path));

        Assert.True(surface.Undo());
        Assert.Equal(designer, File.ReadAllText(path));
        Assert.Equal(code, File.ReadAllText(DesignerCodeReader.CompanionPath(path)!));
        Assert.False(surface.CanUndo);

        Assert.True(surface.Redo());
        Assert.Contains("label1.Click += label1_Click;", File.ReadAllText(path));
    }

    [Fact]
    public void AFailingBatchChangesNothing()
    {
        using var surface = DesignSurface.Open(CopySample("HelloForms/MainForm.Designer.cs"));
        var before = surface.Source;
        var ex = Assert.Throws<DesignerEditException>(() => surface.Apply(new[]
        {
            new DesignerOp { Op = "setProp", Id = "button1", Prop = "Text", Value = "Changed" },
            new DesignerOp { Op = "setProp", Id = "button1", Prop = "TabIndex", Value = "not a number" },
        }));
        Assert.Contains("TabIndex", ex.Message);
        Assert.Equal(before, surface.Source);
        Assert.Equal("Click me", ((Button)surface.Model.Find("button1")!.Instance).Text);
        Assert.False(surface.CanUndo);
    }

    [Fact]
    public void HiddenAndDisabledControlsStayOnTheCanvas()
    {
        using var surface = DesignSurface.Open(CopySample("HelloForms/MainForm.Designer.cs"));
        surface.Apply(new[]
        {
            new DesignerOp { Op = "setProp", Id = "button1", Prop = "Visible", Value = "False" },
            new DesignerOp { Op = "setProp", Id = "label1", Prop = "Enabled", Value = "false" },
        });
        // Written to the file...
        Assert.Contains("button1.Visible = false;", surface.Source);
        Assert.Contains("label1.Enabled = false;", surface.Source);
        // ...but the design-time objects stay visible and enabled, and the grid shows the written value.
        var button = (Button)surface.Model.Find("button1")!.Instance;
        Assert.True(button.VisibleOwn);
        Assert.True(surface.Render().Items.Single(i => i.Id == "button1").Visible);
        var row = surface.GetProperties("button1").Single(r => r.Name == "Visible");
        Assert.Equal("False", row.Value);
        Assert.True(row.Modified);

        surface.Apply(new[] { new DesignerOp { Op = "setProp", Id = "button1", Prop = "Visible", Value = "True" } });
        Assert.DoesNotContain("button1.Visible", surface.Source);
    }

    [Fact]
    public void ReparentingMovesAControlIntoAContainer()
    {
        using var surface = DesignSurface.Open(CopySample("Gallery/GalleryForm.Designer.cs"));
        surface.Apply(new[] { new DesignerOp { Op = "setParent", Id = "button1", Parent = "panel1", X = 10, Y = 10 } });
        var button = (Button)surface.Model.Find("button1")!.Instance;
        Assert.Same(surface.Model.Find("panel1")!.Instance, button.Parent);
        Assert.Contains("panel1.Controls.Add(button1);", surface.Source);
        Assert.DoesNotContain("tabPage1.Controls.Add(button1);", surface.Source);

        var ex = Assert.Throws<DesignerEditException>(() => surface.Apply(new[] { new DesignerOp { Op = "setParent", Id = "panel1", Parent = "button1" } }));
        Assert.NotNull(ex);
    }

    [Fact]
    public void NestedPanelsAreAddressableAndCanTakeControls()
    {
        using var surface = DesignSurface.Open(CopySample("Gallery/GalleryForm.Designer.cs"));
        var view = surface.Render();
        var panel = view.Items.Single(i => i.Id == "splitContainer1.Panel2");
        Assert.Equal("nested", panel.Kind);
        Assert.True(panel.Container);
        Assert.False(panel.Movable);

        surface.Apply(new[] { new DesignerOp { Op = "add", Type = "Label", Parent = "splitContainer1.Panel2", X = 5, Y = 5 } });
        Assert.Contains("splitContainer1.Panel2.Controls.Add(label", surface.Source);
    }

    [Fact]
    public void PropertyGridAndEventsDescribeTheComponent()
    {
        using var surface = DesignSurface.Open(CopySample("HelloForms/MainForm.Designer.cs"));
        var rows = surface.GetProperties("button1");
        Assert.Equal("button1", rows.Single(r => r.Name == "Name").Value);
        var anchor = rows.Single(r => r.Name == "Anchor");
        Assert.Equal("flags", anchor.Editor);
        Assert.True(anchor.Modified);
        Assert.Contains("Bottom", anchor.Options!);
        Assert.Equal("bool", rows.Single(r => r.Name == "UseVisualStyleBackColor").Editor);
        Assert.Equal("color", rows.Single(r => r.Name == "BackColor").Editor);
        Assert.False(rows.Single(r => r.Name == "BackColor").Modified);
        Assert.True(rows.Select(r => r.Category).Distinct().Count() >= 5);

        var events = surface.GetEvents("button1");
        Assert.Equal("button1_Click", events.Single(e => e.Name == "Click").Handler);
        Assert.Equal("object sender, MouseEventArgs e", events.Single(e => e.Name == "MouseDown").Parameters);
        Assert.Equal("MainForm_Load", surface.GetEvents("").Single(e => e.Name == "Load").DefaultHandler);
    }

    [Fact]
    public void RenamingThroughTheGridRenamesTheField()
    {
        using var surface = DesignSurface.Open(CopySample("HelloForms/MainForm.Designer.cs"));
        surface.Apply(new[] { new DesignerOp { Op = "setProp", Id = "label1", Prop = "Name", Value = "greeting" } });
        Assert.Contains("private Label greeting;", surface.Source);
        Assert.Throws<DesignerEditException>(() => surface.Apply(new[] { new DesignerOp { Op = "rename", Id = "greeting", Value = "button1" } }));
    }

    [Fact]
    public void RenamingTakesTheCodeAndTheHandlersAlong()
    {
        TestPlatform.Install();
        var path = CopySample("HelloForms/MainForm.Designer.cs");
        var codePath = DesignerCodeReader.CompanionPath(path)!;
        // Besides the real references: a local, a string and a comment that only share the name.
        File.WriteAllText(codePath, File.ReadAllText(codePath).Replace("""
                    private void button1_Click(object sender, EventArgs e)
            """, """
                    private void Other()
                    {
                        var label1 = "label1"; // label1
                        Console.WriteLine(label1);
                        button1_Click(this, EventArgs.Empty);
                    }

                    private void button1_Click(object sender, EventArgs e)
            """));
        using var surface = DesignSurface.Open(path);
        surface.AutoSave = true;
        surface.Apply(new[]
        {
            new DesignerOp { Op = "rename", Id = "button1", Value = "okButton" },
            new DesignerOp { Op = "rename", Id = "label1", Value = "greeting" },
        });

        var code = File.ReadAllText(codePath);
        Assert.Contains("private void okButton_Click(object sender, EventArgs e)", code);
        Assert.Contains("okButton_Click(this, EventArgs.Empty);", code);
        Assert.Contains("greeting.Text = $\"Hello from NetForms!", code);
        Assert.Contains("var label1 = \"label1\"; // label1", code);
        Assert.Contains("Console.WriteLine(label1);", code);
        Assert.DoesNotContain("button1", code);
        Assert.Contains("okButton.Click += okButton_Click;", surface.Source);
        DesignerCodeWriterTests.Compile(Path.GetDirectoryName(path)!, path, surface.Source);

        // One step: undo puts both files back.
        surface.Undo();
        Assert.Contains("private void button1_Click(object sender, EventArgs e)", File.ReadAllText(codePath));
        Assert.Contains("button1.Click += button1_Click;", surface.Source);
    }

    [Fact]
    public void AHandlerNotNamedAfterTheComponentKeepsItsName()
    {
        var renamed = CodeRenamer.Rename("""
            partial class F : Form
            {
                void Save(object sender, EventArgs e) { button1.Text = "x"; }
            }
            """, """
            partial class F
            {
                private Button button1;
                void InitializeComponent() { button1 = new Button(); button1.Click += Save; }
            }
            """, "F", new Dictionary<string, string> { ["button1"] = "saveButton" });
        Assert.Contains("void Save(object sender, EventArgs e) { saveButton.Text = \"x\"; }", renamed);
    }

    [Fact]
    public void TreeNodesAndListViewItemsAreEditedAsTextAndWrittenTheWayVisualStudioWritesThem()
    {
        var path = CopySample("Gallery/CollectionsForm.Designer.cs");
        using var surface = DesignSurface.Open(path);
        var nodesRow = surface.GetProperties("treeView1").Single(r => r.Name == "Nodes");
        Assert.Equal("tree", nodesRow.ItemsFormat);
        Assert.Equal(new[] { "Root", "  Child", "Second" }, nodesRow.Items);
        var itemsRow = surface.GetProperties("listView1").Single(r => r.Name == "Items");
        Assert.Equal(new[] { "Apple", "Pear | green", "Plum | red", "Kiwi | x", "Fig" }, itemsRow.Items);

        surface.Apply(new[]
        {
            new DesignerOp { Op = "setItems", Id = "treeView1", Prop = "Nodes", Values = new[] { "Root", "  Child", "  Other", "    Leaf", "Second" } },
            new DesignerOp { Op = "setItems", Id = "listView1", Prop = "Items", Values = new[] { "Apple", "Pear | green", "Plum | red", "Kiwi | x", "Fig", "Cherry | blue | small" } },
        });

        // The untouched nodes and items keep what they had (the group); new ones are VS-shaped locals.
        var tree = (TreeView)surface.Model.Find("treeView1")!.Instance;
        Assert.Equal("Leaf", tree.Nodes[0].Nodes[1].Nodes[0].Text);
        var list = (ListView)surface.Model.Find("listView1")!.Instance;
        Assert.NotNull(list.Items[0].Group);
        Assert.Contains("TreeNode treeNode1 = new TreeNode(\"Child\");", surface.Source);
        Assert.Contains("TreeNode treeNode3 = new TreeNode(\"Other\", new TreeNode[] { treeNode2 });", surface.Source);
        Assert.Contains("TreeNode treeNode4 = new TreeNode(\"Root\", new TreeNode[] { treeNode1, treeNode3 });", surface.Source);
        Assert.Contains("ListViewItem listViewItem6 = new ListViewItem(new string[] { \"Cherry\", \"blue\", \"small\" }, -1);", surface.Source);
        Assert.Contains("new ListViewItem.ListViewSubItem(null, \"red\", Color.Red, SystemColors.Window, new Font(\"Segoe UI\", 9F))", surface.Source);
        Assert.Contains("treeNode2.Name = \"Leaf\";", surface.Source);

        // And the file reads back into the same tree.
        var reread = new DesignerCodeReader().Read(surface.Source, new[] { surface.CompanionSource! }, path);
        var again = (TreeView)reread.Find("treeView1")!.Instance;
        Assert.Equal(new[] { "Root", "Second" }, again.Nodes.Cast<TreeNode>().Select(n => n.Text));
        Assert.Equal(new[] { "Child", "Other" }, again.Nodes[0].Nodes.Cast<TreeNode>().Select(n => n.Text));
        Assert.Throws<DesignerEditException>(() => surface.Apply(new[]
        {
            new DesignerOp { Op = "setItems", Id = "treeView1", Prop = "Nodes", Values = new[] { "Root", "      TooDeep" } },
        }));
    }

    [Fact]
    public void DesignedComponentsAreInDesignModeAndBehaveAsCreated()
    {
        // A VS design surface sites its components and gives them handles: flipping a TrackBar's
        // Orientation there swaps its size, as it does on a shown form (and not in a bare initializer).
        using var surface = DesignSurface.Open(CopySample("Gallery/GalleryForm.Designer.cs"));
        var bar = surface.Model.Components.Select(c => c.Instance).OfType<TrackBar>().First();
        Assert.True(bar.Site?.DesignMode);
        var name = surface.Model.FindByInstance(bar)!.Name!;
        var size = bar.Size;
        surface.Apply(new[] { new DesignerOp { Op = "setProp", Id = name, Prop = "Orientation", Value = "Vertical" } });
        Assert.Equal(new Size(size.Height, size.Width), ((TrackBar)surface.Model.Find(name)!.Instance).Size);
    }

    // --- the protocol ----------------------------------------------------------------------------

    [Fact]
    public void TheProtocolAnswersOneLinePerRequest()
    {
        var path = CopySample("HelloForms/MainForm.Designer.cs");
        using var protocol = new DesignerProtocol();
        JsonObject Call(object request)
        {
            var line = protocol.Handle(JsonSerializer.Serialize(request));
            Assert.DoesNotContain('\n', line);
            return JsonNode.Parse(line)!.AsObject();
        }

        Assert.Equal(DesignerProtocol.Version, (int)Call(new { id = 1, method = "hello" })["result"]!["protocol"]!);
        var noForm = Call(new { id = 2, method = "render" });
        Assert.Equal("edit", (string?)noForm["error"]!["kind"]);

        var opened = Call(new { id = 3, method = "open", @params = new { path, autoSave = false } });
        Assert.Equal(3, (int)opened["id"]!);
        Assert.Equal("MainForm", (string?)opened["result"]!["className"]);

        var applied = Call(new { id = 4, method = "apply", @params = new { ops = new object[] { new { op = "setBounds", id = "button1", x = 5, y = 6 } } } });
        var item = applied["result"]!["items"]!.AsArray().First(i => (string?)i!["id"] == "button1")!;
        Assert.Equal(5, (int)item["x"]!);
        Assert.True((bool)applied["result"]!["canUndo"]!);
        Assert.DoesNotContain("new Point(5, 6)", File.ReadAllText(path)); // autoSave off
        Call(new { id = 5, method = "save" });
        Assert.Contains("button1.Location = new Point(5, 6);", File.ReadAllText(path));

        var refused = Call(new { id = 6, method = "apply", @params = new { ops = new object[] { new { op = "remove", id = "nothing" } } } });
        Assert.Equal("edit", (string?)refused["error"]!["kind"]);

        var broken = Path.Combine(_dir, "Broken.Designer.cs");
        File.WriteAllText(broken, "partial class Broken : System.Windows.Forms.Form { void InitializeComponent() { for (;;) {} } }");
        var code = Call(new { id = 7, method = "open", @params = new { path = broken } });
        Assert.Equal("code", (string?)code["error"]!["kind"]);
        Assert.Equal(1, (int)code["error"]!["line"]!);

        Call(new { id = 8, method = "close" });
        Assert.True(protocol.Closed);
    }
}
