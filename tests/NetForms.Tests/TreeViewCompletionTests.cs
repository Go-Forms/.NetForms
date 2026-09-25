using System.Runtime.Serialization;
using NetForms.Platform;
using Xunit;

namespace NetForms.Tests;

/// <summary>
/// The rest of the TreeView surface: selection on release (a press that becomes a drag selects nothing), NodeMouseClick
/// after the release, NodeMouseHover, hot tracking, node tool tips and context menus, state images and image keys,
/// RightToLeftLayout, the node handle, TreeNode serialization and the TreeNodeCollection overloads.
/// </summary>
public class TreeViewCompletionTests
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

    private static Point Middle(TreeNode node) => new(node.Bounds.X + 4, node.Bounds.Y + node.Bounds.Height / 2);

    [Fact]
    public void TheLabelIsSelectedOnReleaseAndNodeMouseClickFollows()
    {
        var (_, form, window, tree) = ShowTree();
        using (form)
        {
            var a = tree.Nodes.Add("A");
            var b = tree.Nodes.Add("B");
            var log = new List<string>();
            tree.AfterSelect += (_, e) => log.Add("AfterSelect " + e.Node!.Text + " " + e.Action);
            tree.NodeMouseClick += (_, e) => log.Add("NodeMouseClick " + e.Node.Text + " " + e.Button);
            tree.MouseUp += (_, _) => log.Add("MouseUp");

            window.Host.MouseDown(MouseButton.Left, Middle(b), 1, InputModifiers.None);
            Assert.Empty(log);
            window.Host.MouseUp(MouseButton.Left, Middle(b), InputModifiers.None);
            Assert.Equal(new[] { "AfterSelect B ByMouse", "NodeMouseClick B Left", "MouseUp" }, log);

            // The right button does not select (a known WinForms trait); NodeMouseClick still reports the node.
            log.Clear();
            window.Host.MouseDown(MouseButton.Right, Middle(a), 1, InputModifiers.None);
            window.Host.MouseUp(MouseButton.Right, Middle(a), InputModifiers.None);
            Assert.Same(b, tree.SelectedNode);
            Assert.Contains("NodeMouseClick A Right", log);
        }
    }

    [Fact]
    public void ARightDragRaisesItemDragWithTheRightButton()
    {
        var (_, form, window, tree) = ShowTree();
        using (form)
        {
            var a = tree.Nodes.Add("A");
            ItemDragEventArgs? drag = null;
            tree.ItemDrag += (_, e) => drag = e;
            var p = Middle(a);
            window.Host.MouseDown(MouseButton.Right, p, 1, InputModifiers.None);
            window.Host.MouseMove(new Point(p.X, p.Y + 1), MouseButton.Right, InputModifiers.None); // within DragSize
            Assert.Null(drag);
            window.Host.MouseMove(new Point(p.X + 6, p.Y), MouseButton.Right, InputModifiers.None);
            Assert.Equal(MouseButtons.Right, drag!.Button);
            Assert.Same(a, drag.Item);
            window.Host.MouseUp(MouseButton.Right, p, InputModifiers.None);
            Assert.Null(tree.SelectedNode);
        }
    }

    [Fact]
    public void HoverRaisesNodeMouseHoverOnceAndShowsTheNodeToolTip()
    {
        var (_, form, window, tree) = ShowTree(t => t.ShowNodeToolTips = true);
        using (form)
        {
            var a = tree.Nodes.Add("A");
            a.ToolTipText = "About A";
            var hovers = new List<TreeNode>();
            int plainHovers = 0;
            tree.NodeMouseHover += (_, e) => hovers.Add(e.Node);
            tree.MouseHover += (_, _) => plainHovers++;

            window.Host.MouseMove(Middle(a), MouseButton.None, InputModifiers.None);
            tree.OnHoverElapsed();
            tree.OnHoverElapsed();
            Assert.Equal(new[] { a }, hovers);

            window.Host.MouseMove(new Point(150, 250), MouseButton.None, InputModifiers.None);
            tree.OnHoverElapsed();
            Assert.Equal(1, plainHovers);
        }
    }

    [Fact]
    public void HotTrackingUnderlinesTheLabelUnderThePointer()
    {
        var (_, form, window, tree) = ShowTree(t => t.HotTracking = true);
        using (form)
        {
            var a = tree.Nodes.Add("Hot node");
            window.Host.MouseMove(Middle(a), MouseButton.None, InputModifiers.None);
            Assert.Same(a, tree.HotNode);
            Assert.Same(Cursors.Hand, tree.Cursor);
            window.Host.MouseLeave();
            Assert.Null(tree.HotNode);
        }
    }

    [Fact]
    public void ARightClickOnANodeOpensTheNodesContextMenu()
    {
        var (_, form, window, tree) = ShowTree();
        using (form)
        {
            var treeMenu = new ContextMenuStrip();
            treeMenu.Items.Add("Tree");
            var nodeMenu = new ContextMenuStrip();
            nodeMenu.Items.Add("Node");
            tree.ContextMenuStrip = treeMenu;
            var a = tree.Nodes.Add("A");
            a.ContextMenuStrip = nodeMenu;

            window.Host.MouseDown(MouseButton.Right, Middle(a), 1, InputModifiers.None);
            window.Host.MouseUp(MouseButton.Right, Middle(a), InputModifiers.None);
            Assert.True(nodeMenu.Visible);
            Assert.False(treeMenu.Visible);
            nodeMenu.Close();

            window.Host.MouseDown(MouseButton.Right, new Point(150, 250), 1, InputModifiers.None);
            window.Host.MouseUp(MouseButton.Right, new Point(150, 250), InputModifiers.None);
            Assert.True(treeMenu.Visible);
            treeMenu.Close();
        }
    }

    [Fact]
    public void StateImagesImageKeysAndRightToLeftLayout()
    {
        var (_, form, window, tree) = ShowTree();
        using (form)
        {
            var images = new ImageList { ImageSize = new Size(16, 16) };
            using (var redImage = new Bitmap(16, 16))
            {
                using (var g = Graphics.FromImage(redImage)) g.Clear(Color.Red);
                images.Images.Add("red", (Image)redImage.Clone());
            }
            using (var blueImage = new Bitmap(16, 16))
            {
                using (var g = Graphics.FromImage(blueImage)) g.Clear(Color.Blue);
                images.Images.Add("blue", (Image)blueImage.Clone());
            }
            tree.ImageList = images;
            tree.StateImageList = images;
            var node = tree.Nodes.Add("key", "Keyed", "blue");
            node.StateImageKey = "red";
            Assert.Equal("blue", node.ImageKey);
            Assert.Equal(-1, node.StateImageIndex);

            using var shot = window.Paint();
            var row = node.Bounds;
            // The state image comes first (red), then the node's image by key (blue).
            bool red = false, blue = false;
            for (int x = 0; x < row.X; x++)
            {
                var c = shot.GetPixel(x, row.Y + row.Height / 2);
                red |= c.R > 200 && c.G < 50 && c.B < 50;
                blue |= c.B > 200 && c.R < 50 && c.G < 50;
            }
            Assert.True(red && blue);

            int changed = 0;
            tree.RightToLeftLayoutChanged += (_, _) => changed++;
            tree.RightToLeftLayout = true;
            Assert.Equal(1, changed);
        }
    }

    [Fact]
    public void NodeHandlesFindTheirNodes()
    {
        var (_, form, _, tree) = ShowTree();
        using (form)
        {
            var a = tree.Nodes.Add("A");
            var b = a.Nodes.Add("B");
            Assert.NotEqual(IntPtr.Zero, b.Handle);
            Assert.NotEqual(a.Handle, b.Handle);
            Assert.Same(b, TreeNode.FromHandle(tree, b.Handle));
            Assert.Null(TreeNode.FromHandle(tree, (IntPtr)123456789));
        }
    }

    [Fact]
    public void CollectionOverloadsAndRemoveMatchWinForms()
    {
        var tree = new TreeView();
        var a = tree.Nodes.Add("a", "A", "img");
        var b = tree.Nodes.Add("b", "B", "img", "sel");
        Assert.Equal(("img", "sel"), (b.ImageKey, b.SelectedImageKey));
        var c = tree.Nodes.Insert(0, "c", "C", 1, 2);
        Assert.Equal((1, 2), (c.ImageIndex, c.SelectedImageIndex));
        tree.Nodes.Insert(1, "d", "D", "k", "s");
        Assert.Equal(new[] { "C", "D", "A", "B" }, tree.Nodes.Cast<TreeNode>().Select(n => n.Text).ToArray());
        tree.Nodes.Remove(a);
        Assert.Equal(3, tree.Nodes.Count);
        var copy = new TreeNode[3];
        tree.Nodes.CopyTo((Array)copy, 0);
        Assert.Same(c, copy[0]);
    }

    private sealed class SerializableNode : TreeNode
    {
        public SerializableNode() { }

        public SerializableNode(SerializationInfo info, StreamingContext context) : base(info, context) { }

        public void Write(SerializationInfo info) => Serialize(info, default);
    }

    [Fact]
    public void ATreeNodeSerializesItsTextImagesAndChildren()
    {
        var node = new SerializableNode { Text = "Root", Name = "r", ImageKey = "folder", ToolTipText = "tip", Checked = true, Tag = "data" };
        node.Nodes.Add("child");
        var info = new SerializationInfo(typeof(TreeNode), new FormatterConverter());
        node.Write(info);
        var back = new SerializableNode(info, default);
        Assert.Equal(("Root", "r", "folder", "tip", true, "data"), (back.Text, back.Name, back.ImageKey, back.ToolTipText, back.Checked, back.Tag));
        Assert.Equal("child", back.Nodes[0].Text);
    }

    [Fact]
    public void TheConvertersAndTheRenderStylesBag()
    {
        var converter = new TreeNodeConverter();
        var descriptor = (System.ComponentModel.Design.Serialization.InstanceDescriptor)converter.ConvertTo(new TreeNode("x", new[] { new TreeNode("y") }), typeof(System.ComponentModel.Design.Serialization.InstanceDescriptor))!;
        var rebuilt = (TreeNode)descriptor.Invoke()!;
        Assert.Equal(("x", "y"), (rebuilt.Text, rebuilt.Nodes[0].Text));

        var index = new TreeViewImageIndexConverter();
        Assert.Equal("(default)", index.ConvertTo(-1, typeof(string)));
        Assert.Equal("(none)", index.ConvertTo(-2, typeof(string)));
        Assert.Equal(-2, index.ConvertFrom("(none)"));
        Assert.Equal("(default)", new TreeViewImageKeyConverter().ConvertTo("", typeof(string)));

        var bag = OwnerDrawPropertyBag.Copy(null);
        Assert.True(bag.IsEmpty());
        bag.ForeColor = Color.Red;
        Assert.False(OwnerDrawPropertyBag.Copy(bag).IsEmpty());
    }
}
