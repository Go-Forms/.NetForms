using NetForms.Platform;
using Xunit;

namespace NetForms.Tests;

/// <summary>
/// Drag and drop (decision 155): the OLE protocol run by NetForms - QueryContinueDrag/GiveFeedback on the source,
/// DragEnter/DragOver/DragLeave/DragDrop on the target, the effect DoDragDrop returns - within a form, across forms,
/// with Escape; drops from other applications through the platform; DataObject's formats and conversions;
/// TreeView and ListView ItemDrag, including Microsoft's "drag nodes in a TreeView" sample.
/// </summary>
public class DragDropTests
{
    /// <summary>A form with a source panel on the left and a target panel (AllowDrop) on the right.</summary>
    private static (TestPlatform platform, Form form, TestWindow window, Panel source, Panel target, List<string> log) Setup(
        DragDropEffects targetEffect = DragDropEffects.Copy)
    {
        var platform = TestPlatform.Install();
        var form = new Form { ClientSize = new Size(400, 200) };
        var source = new Panel { Bounds = new Rectangle(0, 0, 200, 200) };
        var target = new Panel { Bounds = new Rectangle(200, 0, 200, 200), AllowDrop = true };
        var log = new List<string>();
        source.QueryContinueDrag += (_, e) => log.Add($"QueryContinueDrag {e.Action} esc={e.EscapePressed}");
        source.GiveFeedback += (_, e) => log.Add($"GiveFeedback {e.Effect}");
        target.DragEnter += (_, e) =>
        {
            log.Add($"DragEnter {e.Effect} allowed={e.AllowedEffect}");
            e.Effect = targetEffect;
        };
        target.DragOver += (_, e) => log.Add($"DragOver {e.Effect}");
        target.DragLeave += (_, _) => log.Add("DragLeave");
        target.DragDrop += (_, e) => log.Add($"DragDrop {e.Effect} {e.Data!.GetData(DataFormats.Text)} at {e.X},{e.Y}");
        form.Controls.Add(source);
        form.Controls.Add(target);
        form.Location = new Point(100, 50);
        form.Show();
        return (platform, form, platform.Windows.Last(), source, target, log);
    }

    [Fact]
    public void ADragWithinAFormRaisesTheOleEventsInOrderAndReturnsTheEffect()
    {
        var (platform, form, window, source, _, log) = Setup();
        using (form)
        {
            DragDropEffects result = DragDropEffects.All;
            source.MouseDown += (_, _) => result = source.DoDragDrop("hello", DragDropEffects.Copy | DragDropEffects.Move);
            platform.OnMessageLoop = () =>
            {
                window.Host.MouseMove(new Point(250, 100), MouseButton.Left, InputModifiers.None);
                window.Host.MouseMove(new Point(260, 100), MouseButton.Left, InputModifiers.None);
                window.Host.MouseUp(MouseButton.Left, new Point(260, 100), InputModifiers.None);
            };
            window.Host.MouseMove(new Point(50, 50), MouseButton.None, InputModifiers.None);
            window.Host.MouseDown(MouseButton.Left, new Point(50, 50), 1, InputModifiers.None);

            Assert.Equal(DragDropEffects.Copy, result);
            Assert.Equal(new[]
            {
                // At once, on the source itself: no target there.
                "QueryContinueDrag Continue esc=False", "GiveFeedback None",
                "QueryContinueDrag Continue esc=False", "DragEnter None allowed=Copy, Move", "GiveFeedback Copy",
                "QueryContinueDrag Continue esc=False", "DragOver Copy", "GiveFeedback Copy",
                // Releasing the button: the default action is Drop; X/Y are screen coordinates.
                "QueryContinueDrag Drop esc=False", "DragDrop Copy hello at 360,150",
            }, log);
            Assert.False(DragDropManager.IsDragging);
        }
    }

    [Fact]
    public void EscapeCancelsAndTheTargetGetsDragLeave()
    {
        var (platform, form, window, source, _, log) = Setup();
        using (form)
        {
            DragDropEffects result = DragDropEffects.All;
            source.MouseDown += (_, _) => result = source.DoDragDrop("hello", DragDropEffects.Copy);
            platform.OnMessageLoop = () =>
            {
                window.Host.MouseMove(new Point(250, 100), MouseButton.Left, InputModifiers.None);
                window.Host.KeyDown((int)Keys.Escape, InputModifiers.None);
                // After the drag, the release reaches nobody special.
                window.Host.MouseUp(MouseButton.Left, new Point(250, 100), InputModifiers.None);
            };
            window.Host.MouseDown(MouseButton.Left, new Point(50, 50), 1, InputModifiers.None);

            Assert.Equal(DragDropEffects.None, result);
            Assert.Contains("QueryContinueDrag Cancel esc=True", log);
            Assert.Equal("DragLeave", log.Last());
            Assert.DoesNotContain(log, l => l.StartsWith("DragDrop"));
        }
    }

    [Fact]
    public void ATargetThatDoesNotSetAnEffectRefusesTheDrop()
    {
        var (platform, form, window, source, _, log) = Setup(targetEffect: DragDropEffects.None);
        using (form)
        {
            DragDropEffects result = DragDropEffects.All;
            source.MouseDown += (_, _) => result = source.DoDragDrop("hello", DragDropEffects.Copy);
            platform.OnMessageLoop = () =>
            {
                window.Host.MouseMove(new Point(250, 100), MouseButton.Left, InputModifiers.None);
                window.Host.MouseUp(MouseButton.Left, new Point(250, 100), InputModifiers.None);
            };
            window.Host.MouseDown(MouseButton.Left, new Point(50, 50), 1, InputModifiers.None);
            Assert.Equal(DragDropEffects.None, result);
            Assert.Equal("DragLeave", log.Last());
            Assert.Contains("GiveFeedback None", log);
        }
    }

    [Fact]
    public void AnEffectOutsideTheAllowedOnesIsNotReturned()
    {
        var (platform, form, window, source, _, _) = Setup(targetEffect: DragDropEffects.Move);
        using (form)
        {
            DragDropEffects result = DragDropEffects.All;
            source.MouseDown += (_, _) => result = source.DoDragDrop("hello", DragDropEffects.Copy);
            platform.OnMessageLoop = () =>
            {
                window.Host.MouseMove(new Point(250, 100), MouseButton.Left, InputModifiers.None);
                window.Host.MouseUp(MouseButton.Left, new Point(250, 100), InputModifiers.None);
            };
            window.Host.MouseDown(MouseButton.Left, new Point(50, 50), 1, InputModifiers.None);
            Assert.Equal(DragDropEffects.None, result);
        }
    }

    [Fact]
    public void TheNearestParentWithAllowDropIsTheTargetAndKeyStateCarriesTheModifiers()
    {
        var (platform, form, window, source, target, log) = Setup();
        using (form)
        {
            var child = new Label { Bounds = new Rectangle(20, 20, 100, 100), Text = "child" };
            target.Controls.Add(child);
            int keyState = 0;
            target.DragOver += (_, e) => keyState = e.KeyState;
            source.MouseDown += (_, _) => source.DoDragDrop("hello", DragDropEffects.Copy);
            platform.OnMessageLoop = () =>
            {
                window.Host.MouseMove(new Point(250, 50), MouseButton.Left, InputModifiers.None);
                window.Host.MouseMove(new Point(251, 50), MouseButton.Left, InputModifiers.Control | InputModifiers.Shift);
                window.Host.MouseUp(MouseButton.Left, new Point(251, 50), InputModifiers.None);
            };
            window.Host.MouseDown(MouseButton.Left, new Point(50, 50), 1, InputModifiers.None);
            Assert.Contains(log, l => l.StartsWith("DragDrop Copy hello"));
            Assert.Equal(1 | 4 | 8, keyState); // MK_LBUTTON | MK_SHIFT | MK_CONTROL
        }
    }

    [Fact]
    public void ADragCrossesIntoAnotherFormOfTheApplication()
    {
        var (platform, form, window, source, _, _) = Setup();
        using (form)
        using (var other = new Form { ClientSize = new Size(200, 200), Location = new Point(700, 50) })
        {
            var list = new ListBox { Dock = DockStyle.Fill, AllowDrop = true };
            list.DragEnter += (_, e) => e.Effect = e.AllowedEffect & DragDropEffects.Move;
            list.DragDrop += (_, e) => list.Items.Add(e.Data!.GetData(typeof(string))!);
            other.Controls.Add(list);
            other.Show();

            DragDropEffects result = DragDropEffects.None;
            source.MouseDown += (_, _) => result = source.DoDragDrop("moved", DragDropEffects.Copy | DragDropEffects.Move);
            platform.OnMessageLoop = () =>
            {
                // The source window keeps the pointer: its coordinates are outside it, over the other form.
                window.Host.MouseMove(new Point(650, 100), MouseButton.Left, InputModifiers.None);
                window.Host.MouseUp(MouseButton.Left, new Point(650, 100), InputModifiers.None);
            };
            window.Host.MouseDown(MouseButton.Left, new Point(50, 50), 1, InputModifiers.None);
            Assert.Equal(DragDropEffects.Move, result);
            Assert.Equal(new object[] { "moved" }, list.Items.Cast<object>().ToArray());
        }
    }

    [Fact]
    public void DoDragDropOutsideADragButtonDropsAtOnce()
    {
        var (_, form, _, source, _, log) = Setup();
        using (form)
        {
            // No button is down: OLE's QueryContinueDrag answers Drop on its first call.
            var result = source.DoDragDrop("x", DragDropEffects.Copy);
            Assert.Equal(DragDropEffects.None, result);
            Assert.Equal("QueryContinueDrag Drop esc=False", log.Single());
        }
    }

    [Fact]
    public void FilesDroppedFromAnotherApplicationArriveAsFileDrop()
    {
        var (_, form, window, _, target, log) = Setup();
        using (form)
        {
            string[]? files = null;
            target.DragDrop += (_, e) => files = (string[]?)e.Data!.GetData(DataFormats.FileDrop);
            var data = new PlatformDragData { Files = ["/home/user/a.txt", "/home/user/b.png"] };

            Assert.Equal(PlatformDragEffects.None, window.Host.DragEnter(new Point(50, 50), data, PlatformDragEffects.Copy | PlatformDragEffects.Move, InputModifiers.None));
            Assert.Equal(PlatformDragEffects.Copy, window.Host.DragOver(new Point(250, 50), data, PlatformDragEffects.Copy | PlatformDragEffects.Move, InputModifiers.None));
            Assert.Equal(PlatformDragEffects.Copy, window.Host.Drop(new Point(250, 50), data, PlatformDragEffects.Copy | PlatformDragEffects.Move, InputModifiers.None));

            Assert.Equal(new[] { "/home/user/a.txt", "/home/user/b.png" }, files);
            Assert.Contains("DragEnter None allowed=Copy, Move", log);

            // Leaving without a drop: DragLeave.
            window.Host.DragEnter(new Point(250, 50), new PlatformDragData { Text = "t" }, PlatformDragEffects.Copy, InputModifiers.None);
            window.Host.DragLeave();
            Assert.Equal("DragLeave", log.Last());
        }
    }

    // --- DataObject -------------------------------------------------------------------------------------------

    [Fact]
    public void DataObjectConvertsBetweenTheWinFormsFormats()
    {
        var text = new DataObject("hello");
        Assert.Equal("hello", text.GetData(DataFormats.Text));
        Assert.Equal("hello", text.GetData(DataFormats.UnicodeText));
        Assert.Equal("hello", text.GetData(typeof(string)));
        Assert.True(text.GetDataPresent(DataFormats.Text));
        Assert.False(text.GetDataPresent(DataFormats.Text, autoConvert: false));
        Assert.Contains(DataFormats.UnicodeText, text.GetFormats());
        Assert.Equal(new[] { "System.String" }, text.GetFormats(autoConvert: false));
        Assert.True(text.ContainsText());
        Assert.Equal("hello", text.GetText());

        var files = new DataObject();
        var list = new System.Collections.Specialized.StringCollection { "/a", "/b" };
        files.SetFileDropList(list);
        Assert.True(files.ContainsFileDropList());
        Assert.Equal(new[] { "/a", "/b" }, (string[])files.GetData(DataFormats.FileDrop)!);
        Assert.Equal(new[] { "/a" }, (string[])files.GetData("FileNameW")!);
        Assert.Equal(2, files.GetFileDropList().Count);

        using var bitmap = new Bitmap(4, 4);
        var image = new DataObject(bitmap);
        Assert.True(image.ContainsImage());
        Assert.Same(bitmap, image.GetImage());
        Assert.Same(bitmap, image.GetData(typeof(Bitmap)));

        var node = new TreeNode("n");
        var custom = new DataObject(node);
        Assert.Same(node, custom.GetData(typeof(TreeNode)));
        Assert.True(custom.TryGetData<TreeNode>(out var typed));
        Assert.Same(node, typed);

        var rich = new DataObject();
        rich.SetText("{\\rtf1 x}", TextDataFormat.Rtf);
        Assert.True(rich.ContainsText(TextDataFormat.Rtf));
        Assert.False(rich.ContainsText());

        // A DataObject around another IDataObject hands out its data.
        var wrapped = new DataObject(new DataObject(DataFormats.Html, "<b>x</b>"));
        Assert.Equal("<b>x</b>", wrapped.GetData(DataFormats.Html));
    }

    private sealed record Point3(int X, int Y, int Z);

    [Fact]
    public void DataGoesAsJsonAndComesBackTyped()
    {
        var data = new DataObject();
        data.SetDataAsJson(new Point3(1, 2, 3));
        Assert.True(data.TryGetData<Point3>(out var back));
        Assert.Equal(new Point3(1, 2, 3), back);
        Assert.True(((IDataObject)data).TryGetData<Point3>(typeof(Point3).FullName!, out var viaExtension));
        Assert.Equal(back, viaExtension);
        Assert.False(data.TryGetData<string>(DataFormats.Text, out _));
        Assert.Throws<InvalidOperationException>(() => data.SetDataAsJson(new DataObject()));
    }

    // --- TreeView and ListView ------------------------------------------------------------------------------

    /// <summary>
    /// Microsoft's "How to: Perform Drag-and-Drop Operations Between Controls" / "drag nodes in a TreeView":
    /// ItemDrag starts the drag, DragEnter accepts Move, DragDrop finds the node under e.X/e.Y and moves the dragged
    /// node there. Dragging does not select the dragged node, as in the native tree.
    /// </summary>
    [Fact]
    public void TreeViewNodesMoveByDragAndDropAsInTheMicrosoftSample()
    {
        var platform = TestPlatform.Install();
        using var form = new Form { ClientSize = new Size(300, 300) };
        var tree = new TreeView { Dock = DockStyle.Fill, AllowDrop = true, BorderStyle = BorderStyle.None };
        var fruit = tree.Nodes.Add("Fruit");
        var apple = fruit.Nodes.Add("Apple");
        var vegetables = tree.Nodes.Add("Vegetables");
        tree.ExpandAll();
        tree.SelectedNode = fruit;

        ItemDragEventArgs? itemDrag = null;
        TreeNode? selectedWhenDragStarted = null;
        tree.ItemDrag += (_, e) =>
        {
            itemDrag = e;
            selectedWhenDragStarted = tree.SelectedNode;
            if (e.Button == MouseButtons.Left) tree.DoDragDrop(e.Item!, DragDropEffects.Move);
        };
        tree.DragEnter += (_, e) => e.Effect = e.AllowedEffect;
        tree.DragOver += (_, e) =>
        {
            var p = tree.PointToClient(new Point(e.X, e.Y));
            tree.SelectedNode = tree.GetNodeAt(p);
        };
        tree.DragDrop += (_, e) =>
        {
            var targetPoint = tree.PointToClient(new Point(e.X, e.Y));
            var targetNode = tree.GetNodeAt(targetPoint);
            var draggedNode = (TreeNode)e.Data!.GetData(typeof(TreeNode))!;
            if (!draggedNode.Equals(targetNode) && targetNode != null && e.Effect == DragDropEffects.Move)
            {
                draggedNode.Remove();
                targetNode.Nodes.Add(draggedNode);
                targetNode.Expand();
            }
        };
        form.Controls.Add(tree);
        form.Show();
        var window = platform.Windows.Last();

        var appleBounds = apple.Bounds;
        var vegBounds = vegetables.Bounds;
        var from = new Point(appleBounds.X + 5, appleBounds.Y + appleBounds.Height / 2);
        var to = new Point(vegBounds.X + 5, vegBounds.Y + vegBounds.Height / 2);
        platform.OnMessageLoop = () =>
        {
            window.Host.MouseMove(to, MouseButton.Left, InputModifiers.None);
            window.Host.MouseMove(new Point(to.X + 1, to.Y), MouseButton.Left, InputModifiers.None);
            window.Host.MouseUp(MouseButton.Left, to, InputModifiers.None);
        };
        window.Host.MouseMove(from, MouseButton.None, InputModifiers.None);
        window.Host.MouseDown(MouseButton.Left, from, 1, InputModifiers.None);
        window.Host.MouseMove(new Point(from.X + 10, from.Y), MouseButton.Left, InputModifiers.None);

        Assert.NotNull(itemDrag);
        Assert.Same(apple, itemDrag!.Item);
        Assert.Equal(MouseButtons.Left, itemDrag.Button);
        Assert.Same(fruit, selectedWhenDragStarted);   // the press did not select Apple
        Assert.Same(vegetables, apple.Parent);
        Assert.Empty(fruit.Nodes);
    }

    [Fact]
    public void ListViewRaisesItemDragWithTheItemUnderThePress()
    {
        var platform = TestPlatform.Install();
        using var form = new Form { ClientSize = new Size(300, 300) };
        var list = new ListView { Dock = DockStyle.Fill, View = View.List, MultiSelect = true };
        var a = list.Items.Add("Alpha");
        var b = list.Items.Add("Beta");
        form.Controls.Add(list);
        form.Show();
        var window = platform.Windows.Last();

        // Two selected: a press on one of them keeps both, so both can be dragged.
        a.Selected = true;
        b.Selected = true;
        ItemDragEventArgs? drag = null;
        int selectedAtDrag = 0;
        list.ItemDrag += (_, e) =>
        {
            drag = e;
            selectedAtDrag = list.SelectedItems.Count;
        };
        var p = new Point(b.Bounds.X + 5, b.Bounds.Y + b.Bounds.Height / 2);
        window.Host.MouseDown(MouseButton.Left, p, 1, InputModifiers.None);
        window.Host.MouseMove(new Point(p.X + 8, p.Y), MouseButton.Left, InputModifiers.None);
        window.Host.MouseUp(MouseButton.Left, new Point(p.X + 8, p.Y), InputModifiers.None);
        Assert.Same(b, drag!.Item);
        Assert.Equal(2, selectedAtDrag);

        // Without a drag, the release selects just the pressed item.
        window.Host.MouseDown(MouseButton.Left, p, 1, InputModifiers.None);
        window.Host.MouseUp(MouseButton.Left, p, InputModifiers.None);
        Assert.Equal(new[] { b }, list.SelectedItems.Cast<ListViewItem>().ToArray());

        ListViewItem? hovered = null;
        list.ItemMouseHover += (_, e) => hovered = e.Item;
        var pa = new Point(a.Bounds.X + 5, a.Bounds.Y + a.Bounds.Height / 2);
        window.Host.MouseMove(pa, MouseButton.None, InputModifiers.None);
        list.OnHoverElapsed();
        Assert.Same(a, hovered);
    }
}
