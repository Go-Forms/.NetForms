using System.Text.Json;
using NetForms.Design;
using Xunit;
using static NetForms.Tests.DesignerCodeReaderTests;

namespace NetForms.Tests;

/// <summary>The TreeNode Editor (decision 184): every node with its name, pictures, tag, tool tip and check, the whole tree sent back.</summary>
public sealed class DesignerTreeNodeTests
{
    public DesignerTreeNodeTests() => TestPlatform.Install();

    private static DesignSurface Collections()
    {
        var path = Sample("Gallery/CollectionsForm.Designer.cs");
        return DesignSurface.Load(File.ReadAllText(path), File.ReadAllText(Path.ChangeExtension(Path.ChangeExtension(path, null), ".cs")));
    }

    private static DesignerOp SetNodes(IEnumerable<DesignerTreeNode> nodes) =>
        new() { Op = "setTreeNodes", Id = "treeView1", Value = JsonSerializer.Serialize(nodes, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }) };

    [Fact]
    public void TheGridGivesEveryNodeWithItsProperties()
    {
        using var s = Collections();
        var row = s.GetProperties("treeView1").Single(r => r.Name == "Nodes");
        Assert.Equal(new[] { ("Root", 0), ("Child", 1), ("Second", 0) }, row.TreeNodes!.Select(n => (n.Text, n.Depth)));
        Assert.Equal(new int?[] { 0, 1, 2 }, row.TreeNodes!.Select(n => n.From));
        Assert.All(row.TreeNodes!, n => Assert.Equal(n.Text, n.Name));
        Assert.Null(row.TreeImages);
    }

    [Fact]
    public void NamesPicturesTagsAndPlacesAreWrittenAsVisualStudioWritesThem()
    {
        using var s = Collections();
        var nodes = s.GetProperties("treeView1").Single(r => r.Name == "Nodes").TreeNodes!;
        // Root keeps its child but is renamed and tagged; Second gets a picture, a tool tip and a new child; Child is checked.
        nodes[0].Name = "rootNode";
        nodes[0].Tag = "home";
        nodes[1].Checked = true;
        nodes[2].Image = "#1";
        nodes[2].SelectedImage = "open";
        nodes[2].ToolTipText = "The second one";
        var edited = nodes.ToList();
        edited.Add(new DesignerTreeNode { Depth = 1, Text = "Third" });
        s.Apply(new[] { SetNodes(edited) });

        var code = s.Source.Replace("this.", "");
        Assert.Contains("treeNode2.Name = \"rootNode\";", code);
        Assert.Contains("treeNode2.Tag = \"home\";", code);
        Assert.Contains("treeNode1.Checked = true;", code);
        Assert.Contains("treeNode3.Name = \"Node0\";", code);
        Assert.Contains("TreeNode treeNode4 = new TreeNode(\"Second\", 1, -1, new TreeNode[] { treeNode3 });", code);
        Assert.Contains("treeNode4.SelectedImageKey = \"open\";", code);
        Assert.Contains("treeNode4.ToolTipText = \"The second one\";", code);

        // Read back: the same tree.
        var again = s.GetProperties("treeView1").Single(r => r.Name == "Nodes").TreeNodes!;
        Assert.Equal(new[] { ("Root", 0, "rootNode"), ("Child", 1, "Child"), ("Second", 0, "Second"), ("Third", 1, "Node0") }, again.Select(n => (n.Text, n.Depth, n.Name)));
        Assert.Equal(("#1", "open", "home"), (again[2].Image, again[2].SelectedImage, again[0].Tag));
    }

    [Fact]
    public void ANodeMovesUnderAnotherParentWithItsProperties()
    {
        using var s = Collections();
        var nodes = s.GetProperties("treeView1").Single(r => r.Name == "Nodes").TreeNodes!;
        nodes[1].ToolTipText = "kept";
        s.Apply(new[] { SetNodes(nodes) });
        nodes = s.GetProperties("treeView1").Single(r => r.Name == "Nodes").TreeNodes!;
        // Child moves under Second.
        var moved = new[] { nodes[0], nodes[2], nodes[1] };
        moved[2].Depth = 1;
        s.Apply(new[] { SetNodes(moved) });
        var after = s.GetProperties("treeView1").Single(r => r.Name == "Nodes").TreeNodes!;
        Assert.Equal(new[] { ("Root", 0), ("Second", 0), ("Child", 1) }, after.Select(n => (n.Text, n.Depth)));
        Assert.Equal("kept", after[2].ToolTipText);

        Assert.Throws<DesignerEditException>(() => s.Apply(new[] { SetNodes(new[] { new DesignerTreeNode { Depth = 1, Text = "Orphan" } }) }));
    }
}
