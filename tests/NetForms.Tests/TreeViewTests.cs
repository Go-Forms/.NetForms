using NetForms.Platform;
using Xunit;

namespace NetForms.Tests;

/// <summary>Tree structure, expansion, selection, checking and keyboard navigation.</summary>
public class TreeViewTests
{
    private static (TestPlatform platform, Form form, TestWindow window, TreeView tree) ShowTree(Action<TreeView>? configure = null)
    {
        var platform = TestPlatform.Install();
        var form = new Form { ClientSize = new Size(300, 300) };
        var tree = new TreeView { Bounds = new Rectangle(0, 0, 300, 300), BorderStyle = BorderStyle.None };
        configure?.Invoke(tree);
        form.Controls.Add(tree);
        form.Show();
        return (platform, form, platform.Windows.Last(), tree);
    }

    /// <summary>Root → Child 0 (→ Grandchild), Child 1; Root2.</summary>
    private static (TreeNode root, TreeNode child0, TreeNode grandchild, TreeNode child1, TreeNode root2) Build(TreeView tree)
    {
        var root = tree.Nodes.Add("Root");
        var child0 = root.Nodes.Add("Child 0");
        var grandchild = child0.Nodes.Add("Grandchild");
        var child1 = root.Nodes.Add("Child 1");
        var root2 = tree.Nodes.Add("Root 2");
        return (root, child0, grandchild, child1, root2);
    }

    private static void Key(TestWindow w, Keys key, InputModifiers mods = InputModifiers.None)
    {
        w.Host.KeyDown((int)(key & Keys.KeyCode), mods);
        w.Host.KeyUp((int)(key & Keys.KeyCode), mods);
    }

    // --- structure ------------------------------------------------------------------------

    [Fact]
    public void NodesKnowTheirParentTreeLevelAndPath()
    {
        var (_, form, _, tree) = ShowTree();
        using (form)
        {
            var (root, child0, grandchild, child1, root2) = Build(tree);

            Assert.Same(tree, root.TreeView);
            Assert.Same(tree, grandchild.TreeView);
            Assert.Same(root, child0.Parent);
            Assert.Null(root.Parent);

            Assert.Equal(0, root.Level);
            Assert.Equal(1, child0.Level);
            Assert.Equal(2, grandchild.Level);

            Assert.Equal(0, child0.Index);
            Assert.Equal(1, child1.Index);
            Assert.Equal(1, root2.Index);

            Assert.Equal("Root\\Child 0\\Grandchild", grandchild.FullPath);
            Assert.Same(child1, child0.NextNode);
            Assert.Same(child0, child1.PrevNode);
            Assert.Null(child1.NextNode);

            Assert.Equal(2, tree.GetNodeCount(false));
            Assert.Equal(5, tree.GetNodeCount(true));
            Assert.Equal(3, root.GetNodeCount(true));
        }
    }

    [Fact]
    public void MovingANodeToAnotherParentDetachesItFromTheOldOne()
    {
        var (_, form, _, tree) = ShowTree();
        using (form)
        {
            var (root, child0, _, _, root2) = Build(tree);
            root2.Nodes.Add(child0);

            Assert.Same(root2, child0.Parent);
            Assert.DoesNotContain(child0, root.Nodes);
            Assert.Equal(1, root.Nodes.Count);
            Assert.Same(tree, child0.TreeView);

            child0.Remove();
            Assert.Null(child0.Parent);
            Assert.Null(child0.TreeView);
            Assert.Equal(0, root2.Nodes.Count);
        }
    }

    // --- expansion ------------------------------------------------------------------------

    [Fact]
    public void OnlyExpandedBranchesTakeUpRows()
    {
        var (_, form, _, tree) = ShowTree();
        using (form)
        {
            var (root, child0, grandchild, _, root2) = Build(tree);

            // Collapsed: only the two roots are laid out.
            Assert.Equal(Rectangle.Empty, child0.Bounds);
            Assert.Equal(root.Bounds.Bottom, root2.Bounds.Top);

            root.Expand();
            Assert.True(root.IsExpanded);
            Assert.NotEqual(Rectangle.Empty, child0.Bounds);
            Assert.Equal(root.Bounds.Bottom, child0.Bounds.Top);
            // The grandchild stays hidden while its own parent is collapsed.
            Assert.Equal(Rectangle.Empty, grandchild.Bounds);
            Assert.False(grandchild.IsVisible);

            tree.ExpandAll();
            Assert.True(grandchild.IsVisible);
            Assert.Equal(child0.Bounds.Bottom, grandchild.Bounds.Top);

            tree.CollapseAll();
            Assert.False(root.IsExpanded);
            Assert.Equal(root.Bounds.Bottom, root2.Bounds.Top);
        }
    }

    [Fact]
    public void ExpandAndCollapseRaiseTheirEventsAndCanBeCancelled()
    {
        var (_, form, _, tree) = ShowTree();
        using (form)
        {
            var (root, _, _, _, _) = Build(tree);
            var log = new List<string>();
            tree.BeforeExpand += (_, e) => log.Add("before-expand:" + e.Node!.Text);
            tree.AfterExpand += (_, e) => log.Add("after-expand:" + e.Node!.Text);
            tree.BeforeCollapse += (_, e) => log.Add("before-collapse:" + e.Node!.Text);
            tree.AfterCollapse += (_, e) => log.Add("after-collapse:" + e.Node!.Text);

            root.Expand();
            root.Collapse();
            Assert.Equal(new[] { "before-expand:Root", "after-expand:Root", "before-collapse:Root", "after-collapse:Root" }, log);

            tree.BeforeExpand += (_, e) => e.Cancel = true;
            root.Expand();
            Assert.False(root.IsExpanded);
        }
    }

    [Fact]
    public void CollapsingTheBranchWithTheSelectionMovesItToTheParent()
    {
        var (_, form, _, tree) = ShowTree();
        using (form)
        {
            var (root, child0, grandchild, _, _) = Build(tree);
            tree.ExpandAll();
            tree.SelectedNode = grandchild;

            root.Collapse();
            Assert.Same(root, tree.SelectedNode);
        }
    }

    [Fact]
    public void EnsureVisibleExpandsTheAncestors()
    {
        var (_, form, _, tree) = ShowTree();
        using (form)
        {
            var (root, child0, grandchild, _, _) = Build(tree);
            grandchild.EnsureVisible();

            Assert.True(root.IsExpanded);
            Assert.True(child0.IsExpanded);
            Assert.True(grandchild.IsVisible);
        }
    }

    // --- mouse and keyboard -----------------------------------------------------------------

    [Fact]
    public void ClickingTheGlyphTogglesAndClickingTheLabelSelects()
    {
        var (_, form, window, tree) = ShowTree();
        using (form)
        {
            var (root, child0, _, _, _) = Build(tree);
            var selections = new List<string>();
            tree.AfterSelect += (_, e) => selections.Add(e.Node!.Text + ":" + e.Action);

            var row = new Rectangle(0, 0, tree.Width, tree.ItemHeight);
            // The glyph sits in the indent column to the left of the label.
            window.Click(new Point(tree.Indent / 2, row.Height / 2));
            Assert.True(root.IsExpanded);

            window.Click(new Point(root.Bounds.X + 5, root.Bounds.Y + root.Bounds.Height / 2));
            Assert.Same(root, tree.SelectedNode);
            Assert.Equal(new[] { "Root:ByMouse" }, selections);

            window.Click(new Point(child0.Bounds.X + 5, child0.Bounds.Y + child0.Bounds.Height / 2));
            Assert.Same(child0, tree.SelectedNode);
        }
    }

    [Fact]
    public void ArrowKeysWalkAndFoldTheTree()
    {
        var (_, form, window, tree) = ShowTree();
        using (form)
        {
            var (root, child0, grandchild, child1, root2) = Build(tree);
            tree.SelectedNode = root;

            // Right expands, then steps into the branch.
            Key(window, Keys.Right);
            Assert.True(root.IsExpanded);
            Key(window, Keys.Right);
            Assert.Same(child0, tree.SelectedNode);

            Key(window, Keys.Down);
            Assert.Same(child1, tree.SelectedNode);

            // Left on a collapsed node goes to the parent.
            Key(window, Keys.Left);
            Assert.Same(root, tree.SelectedNode);

            // Left again folds the branch.
            Key(window, Keys.Left);
            Assert.False(root.IsExpanded);

            Key(window, Keys.End);
            Assert.Same(root2, tree.SelectedNode);
            Key(window, Keys.Home);
            Assert.Same(root, tree.SelectedNode);
        }
    }

    [Fact]
    public void CheckBoxesToggleWithTheMouseAndSpaceAndCanBeVetoed()
    {
        var (_, form, window, tree) = ShowTree(t => t.CheckBoxes = true);
        using (form)
        {
            var (root, _, _, _, _) = Build(tree);
            int checks = 0;
            tree.AfterCheck += (_, _) => checks++;

            var row = new Rectangle(0, 0, tree.Width, tree.ItemHeight);
            window.Click(new Point(tree.Indent + 8, row.Height / 2));
            Assert.True(root.Checked);
            Assert.Equal(1, checks);

            tree.SelectedNode = root;
            Key(window, Keys.Space);
            Assert.False(root.Checked);

            tree.BeforeCheck += (_, e) => e.Cancel = true;
            root.Checked = true;
            Assert.False(root.Checked);
        }
    }

    [Fact]
    public void SelectionCanBeVetoedAndReportsItsAction()
    {
        var (_, form, _, tree) = ShowTree();
        using (form)
        {
            var (root, _, _, _, root2) = Build(tree);
            tree.SelectedNode = root;

            tree.BeforeSelect += (_, e) => e.Cancel = e.Node == root2;
            tree.SelectedNode = root2;
            Assert.Same(root, tree.SelectedNode);
        }
    }

    // --- sorting, hit-testing, painting --------------------------------------------------------

    [Fact]
    public void SortOrdersEveryLevel()
    {
        var (_, form, _, tree) = ShowTree();
        using (form)
        {
            var b = tree.Nodes.Add("Bravo");
            var a = tree.Nodes.Add("Alpha");
            a.Nodes.Add("z");
            a.Nodes.Add("m");

            tree.Sort();
            Assert.Equal("Alpha", tree.Nodes[0].Text);
            Assert.Equal("Bravo", tree.Nodes[1].Text);
            Assert.Equal("m", tree.Nodes[0].Nodes[0].Text);
            Assert.Equal("z", tree.Nodes[0].Nodes[1].Text);
        }
    }

    [Fact]
    public void HitTestTellsTheGlyphFromTheLabel()
    {
        var (_, form, _, tree) = ShowTree(t => t.CheckBoxes = true);
        using (form)
        {
            var (root, _, _, _, _) = Build(tree);
            int mid = tree.ItemHeight / 2;

            Assert.Equal(TreeViewHitTestLocations.PlusMinus, tree.HitTest(tree.Indent / 2, mid).Location);
            Assert.Equal(TreeViewHitTestLocations.StateImage, tree.HitTest(tree.Indent + 8, mid).Location);
            Assert.Equal(TreeViewHitTestLocations.Label, tree.HitTest(root.Bounds.X + 4, mid).Location);
            Assert.Equal(TreeViewHitTestLocations.RightOfLabel, tree.HitTest(tree.Width - 5, mid).Location);
            Assert.Null(tree.GetNodeAt(5, 295));
        }
    }

    [Fact]
    public void ScrollingKeepsTheSelectedNodeInView()
    {
        var (_, form, _, tree) = ShowTree();
        using (form)
        {
            tree.BeginUpdate();
            for (int i = 0; i < 200; i++) tree.Nodes.Add("Node " + i);
            tree.EndUpdate();

            Assert.Same(tree.Nodes[0], tree.TopNode);
            tree.SelectedNode = tree.Nodes[150];
            Assert.NotSame(tree.Nodes[0], tree.TopNode);

            var bounds = tree.Nodes[150].Bounds;
            Assert.True(bounds.Top >= 0 && bounds.Bottom <= tree.Height);
        }
    }

    [Fact]
    public void TheTreePaintsWithLinesGlyphsChecksAndSelection()
    {
        var (_, form, window, tree) = ShowTree(t =>
        {
            t.CheckBoxes = true;
            t.ShowLines = true;
            t.ShowPlusMinus = true;
        });
        using (form)
        {
            var (root, child0, grandchild, child1, root2) = Build(tree);
            root2.Nodes.Add("Leaf");
            tree.ExpandAll();
            tree.SelectedNode = child1;
            child0.Checked = true;
            grandchild.ForeColor = Color.DarkGreen;

            using var bitmap = window.Paint();
            Assert.Equal(300, bitmap.Width);

            var outDir = Path.Combine(AppContext.BaseDirectory, "render-out");
            Directory.CreateDirectory(outDir);
            bitmap.Save(Path.Combine(outDir, "treeview.png"));
        }
    }
}
