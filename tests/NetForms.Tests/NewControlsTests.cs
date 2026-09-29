using System.ComponentModel;
using System.Windows.Forms.VisualStyles;
using NetForms.Design;
using NetForms.Design.Serialization;
using NetForms.Platform;
using Xunit;

namespace NetForms.Tests;

/// <summary>
/// DomainUpDown, HelpProvider, Splitter, BindingNavigator and the control renderers (decision 163): WinForms'
/// behaviour, driven through the test platform, and the designer's part (the navigator's standard items).
/// </summary>
public class NewControlsTests
{
    private static (TestPlatform platform, Form form, TestWindow window) ShowForm(params Control[] controls)
    {
        var platform = TestPlatform.Install();
        var form = new Form { ClientSize = new Size(400, 300) };
        foreach (var c in controls) form.Controls.Add(c);
        form.Show();
        return (platform, form, platform.Windows.Last());
    }

    private static void Key(TestWindow w, Keys key)
    {
        w.Host.KeyDown((int)(key & Keys.KeyCode), InputModifiers.None);
        w.Host.KeyUp((int)(key & Keys.KeyCode), InputModifiers.None);
    }

    // --- DomainUpDown ------------------------------------------------------------------------------------

    [Fact]
    public void DomainUpDownStepsThroughItsItemsAndWraps()
    {
        var box = new DomainUpDown { Bounds = new Rectangle(10, 10, 120, 23) };
        box.Items.AddRange(new[] { "Low", "Medium", "High" });
        var (_, form, window) = ShowForm(box);
        using (form)
        {
            int changes = 0;
            box.SelectedItemChanged += (_, _) => changes++;
            Assert.Equal(-1, box.SelectedIndex);
            Assert.Null(box.SelectedItem);

            box.DownButton(); // WinForms: "down" is the next item
            Assert.Equal(0, box.SelectedIndex);
            Assert.Equal("Low", box.Text);
            box.DownButton();
            box.DownButton();
            Assert.Equal("High", box.SelectedItem);
            box.DownButton(); // no Wrap: stays
            Assert.Equal(2, box.SelectedIndex);
            box.Wrap = true;
            box.DownButton();
            Assert.Equal("Low", box.Text);
            box.UpButton();
            Assert.Equal("High", box.Text);
            Assert.True(changes >= 5);

            // The arrow keys step it when it has the focus.
            box.Focus();
            Key(window, Keys.Up);
            Assert.Equal("Medium", box.Text);

            // Typing matches the start of an item; the arrows then go on from it.
            box.Text = "lo";
            Assert.Equal(-1, box.SelectedIndex); // a user edit
            box.DownButton();
            Assert.Equal("Low", box.Text);
            Assert.Equal(0, box.SelectedIndex);
        }
    }

    [Fact]
    public void DomainUpDownSortsAndKeepsTheSelectionOnRemove()
    {
        var box = new DomainUpDown();
        box.Items.Add("cherry");
        box.Items.Add("apple");
        box.Items.Add("banana");
        box.SelectedItem = "banana";
        box.Sorted = true;
        Assert.Equal(new object[] { "apple", "banana", "cherry" }, box.Items.Cast<object>().ToArray());
        // WinForms' sort writes the items back through the indexer, which re-selects the old index: the selection
        // becomes the item that lands there ("cherry"), not the one that was selected. NetForms does the same.
        Assert.Equal("cherry", box.SelectedItem);
        box.SelectedItem = "banana";

        box.Items.RemoveAt(0);
        Assert.Equal("banana", box.SelectedItem);
        box.Items.Remove("banana");
        Assert.Equal(-1, box.SelectedIndex);
        Assert.Throws<ArgumentOutOfRangeException>(() => box.Items.Remove("durian"));
        Assert.Throws<ArgumentOutOfRangeException>(() => box.SelectedIndex = 5);
        Assert.Contains("Items.Count: 1", box.ToString());
    }

    [Fact]
    public void DomainUpDownRoundTripsThroughTheDesignerCode()
    {
        const string source = """
            namespace App
            {
                partial class Form1 : Form
                {
                    private System.ComponentModel.IContainer components = null;
                    private void InitializeComponent()
                    {
                        domainUpDown1 = new DomainUpDown();
                        SuspendLayout();
                        domainUpDown1.Items.Add("North");
                        domainUpDown1.Items.Add("South");
                        domainUpDown1.Location = new Point(12, 12);
                        domainUpDown1.Name = "domainUpDown1";
                        domainUpDown1.Size = new Size(120, 23);
                        domainUpDown1.TabIndex = 0;
                        domainUpDown1.Wrap = true;
                        Controls.Add(domainUpDown1);
                        Name = "Form1";
                        ResumeLayout(false);
                    }
                    private DomainUpDown domainUpDown1;
                }
            }
            """;
        var model = new DesignerCodeReader().Read(source);
        var box = (DomainUpDown)model.Find("domainUpDown1")!.Instance;
        Assert.Equal(new object[] { "North", "South" }, box.Items.Cast<object>().ToArray());
        Assert.True(box.Wrap);
        box.Items.Add("East");
        var code = new DesignerCodeWriter().Write(model, source);
        Assert.Contains("domainUpDown1.Items.Add(\"East\");", code);
        Assert.Contains("domainUpDown1.Items.Add(\"North\");", code);
        Assert.Contains("domainUpDown1.Wrap = true;", code);
    }

    // --- HelpProvider --------------------------------------------------------------------------------------

    [Fact]
    public void HelpProviderExtendsControlsAndAnswersF1()
    {
        var box = new TextBox { Bounds = new Rectangle(10, 10, 100, 23) };
        var other = new TextBox { Bounds = new Rectangle(10, 40, 100, 23) };
        var (platform, form, window) = ShowForm(box, other);
        var timersBefore = platform.Timers.ToList();
        using (form)
        {
            var help = new HelpProvider();
            Assert.True(help.CanExtend(box));
            Assert.False(help.CanExtend(new System.Windows.Forms.Timer()));
            Assert.False(help.GetShowHelp(box));

            help.SetHelpString(box, "The customer's name.");
            Assert.True(help.GetShowHelp(box)); // a help string turns ShowHelp on
            Assert.Equal(HelpNavigator.AssociateIndex, help.GetHelpNavigator(box));

            // F1 on the box: the provider handles HelpRequested, so it does not bubble to the form.
            bool formAsked = false;
            form.HelpRequested += (_, _) => formAsked = true;
            box.Focus();
            Key(window, Keys.F1);
            Assert.False(formAsked);

            // Another control has no help: F1 reaches the form.
            other.Focus();
            Key(window, Keys.F1);
            Assert.True(formAsked);

            // Accessibility asks the provider too.
            var query = new QueryAccessibilityHelpEventArgs();
            help.SetHelpKeyword(box, "customers");
            help.HelpNamespace = "https://example.com/help";
            typeof(Control).GetMethod("OnQueryAccessibilityHelp", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(box, new object[] { query });

            help.SetShowHelp(box, false);
            Assert.False(help.GetShowHelp(box));
            help.ResetShowHelp(box);
            Assert.Contains("HelpNamespace: https://example.com/help", help.ToString());
        }
        // The pop-up is a ToolTip whose hide timer the test platform never fires: leave no timer to other tests.
        foreach (var timer in platform.Timers.Except(timersBefore).ToList()) timer.Dispose();
    }

    [Fact]
    public void HelpProviderPropertiesAppearOnControlsInTheDesigner()
    {
        var container = new Container();
        var help = new HelpProvider();
        var button = new Button();
        container.Add(help, "helpProvider1");
        container.Add(button, "button1");
        var names = TypeDescriptor.GetProperties(button).Cast<PropertyDescriptor>().Select(p => p.Name).ToList();
        Assert.Contains("HelpString", names);
        Assert.Contains("ShowHelp", names);
    }

    // --- Splitter -----------------------------------------------------------------------------------------

    [Fact]
    public void SplitterResizesItsTargetWhenDragged()
    {
        var tree = new Panel { Dock = DockStyle.Left, Width = 100 };
        var splitter = new Splitter();
        var fill = new Panel { Dock = DockStyle.Fill };
        // Docking goes from the back of the z-order: the fill first in the collection, then the splitter, then the tree.
        var (_, form, window) = ShowForm(fill, splitter, tree);
        using (form)
        {
            Assert.Equal(DockStyle.Left, splitter.Dock);
            Assert.Equal(new Rectangle(100, 0, 3, 300), splitter.Bounds);
            Assert.Equal(100, splitter.SplitPosition);
            Assert.Equal(Cursors.VSplit, splitter.Cursor);

            var moving = new List<int>();
            int moved = 0;
            splitter.SplitterMoving += (_, e) => moving.Add(e.SplitX);
            splitter.SplitterMoved += (_, _) => moved++;

            // Drag from x=101 to x=181: the bar follows, the tree is resized on release.
            window.Host.MouseMove(new Point(101, 50), MouseButton.None, InputModifiers.None);
            window.Host.MouseDown(MouseButton.Left, new Point(101, 50), 1, InputModifiers.None);
            window.Host.MouseMove(new Point(181, 50), MouseButton.Left, InputModifiers.None);
            Assert.NotEmpty(moving);
            Assert.Equal(100, tree.Width); // not yet
            Assert.Equal(180, splitter.SplitBarBounds!.Value.X);
            window.Host.MouseUp(MouseButton.Left, new Point(181, 50), InputModifiers.None);
            Assert.Null(splitter.SplitBarBounds);
            Assert.Equal(180, tree.Width);
            Assert.Equal(180, splitter.Left);
            Assert.Equal(1, moved);

            // MinSize and MinExtra bound it.
            splitter.SplitPosition = 5;
            Assert.Equal(25, tree.Width);
            splitter.SplitPosition = 1000;
            Assert.Equal(400 - 3 - 25, tree.Width);

            Assert.Throws<ArgumentException>(() => splitter.Dock = DockStyle.Fill);
            splitter.Anchor = AnchorStyles.Bottom;
            Assert.Equal(AnchorStyles.None, splitter.Anchor);
        }
    }

    // --- BindingNavigator ---------------------------------------------------------------------------------

    private sealed class Customer
    {
        public string Name { get; set; } = "";
    }

    [Fact]
    public void BindingNavigatorMovesThroughTheBindingSource()
    {
        var list = new BindingList<Customer> { new() { Name = "Ann" }, new() { Name = "Bob" }, new() { Name = "Cid" } };
        var source = new BindingSource { DataSource = list };
        var navigator = new BindingNavigator(source);
        var (_, form, _) = ShowForm(navigator);
        using (form)
        {
            Assert.Equal(11, navigator.Items.Count);
            Assert.Equal("bindingNavigatorMoveFirstItem", navigator.MoveFirstItem!.Name);
            Assert.Equal("1", navigator.PositionItem!.Text);
            Assert.Equal("of 3", navigator.CountItem!.Text);
            Assert.False(navigator.MoveFirstItem.Enabled);
            Assert.True(navigator.MoveNextItem!.Enabled);

            navigator.MoveLastItem!.PerformClick();
            Assert.Equal(2, source.Position);
            Assert.Equal("3", navigator.PositionItem.Text);
            Assert.False(navigator.MoveNextItem.Enabled);
            Assert.True(navigator.MovePreviousItem!.Enabled);

            navigator.MovePreviousItem.PerformClick();
            Assert.Equal("Bob", ((Customer)source.Current!).Name);

            navigator.AddNewItem!.PerformClick();
            Assert.Equal(4, list.Count);
            Assert.Equal("of 4", navigator.CountItem.Text);
            Assert.Equal("4", navigator.PositionItem.Text);

            navigator.DeleteItem!.PerformClick();
            Assert.Equal(3, list.Count);

            // The position box: a number and Enter moves there.
            var box = ((ToolStripTextBox)navigator.PositionItem).TextBox;
            box.Text = "1";
            box.Focus();
            var window = TestPlatform.Install().Windows.Last();
            Key(window, Keys.Enter);
            Assert.Equal(0, source.Position);

            int refreshes = 0;
            navigator.RefreshItems += (_, _) => refreshes++;
            source.MoveNext();
            Assert.True(refreshes > 0);

            navigator.CountItemFormat = "/ {0}";
            Assert.Equal("/ 3", navigator.CountItem.Text);

            // A user-disabled Add button stays disabled.
            navigator.AddNewItem.Enabled = false;
            source.MoveFirst();
            Assert.False(navigator.AddNewItem.Enabled);

            navigator.BindingSource = null;
            Assert.Equal("0", navigator.PositionItem.Text);
            Assert.False(navigator.DeleteItem.Enabled);
        }
    }

    [Fact]
    public void TheDesignerDropsABindingNavigatorWithItsStandardItems()
    {
        var path = Path.Combine(Path.GetTempPath(), $"netforms-nav-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        try
        {
            var file = Path.Combine(path, "Form1.Designer.cs");
            File.WriteAllText(file, """
                namespace App
                {
                    partial class Form1 : Form
                    {
                        private System.ComponentModel.IContainer components = null;
                        private void InitializeComponent()
                        {
                            SuspendLayout();
                            ClientSize = new Size(400, 300);
                            Name = "Form1";
                            ResumeLayout(false);
                        }
                    }
                }
                """);
            using var surface = DesignSurface.Open(file);
            surface.Apply(new[] { new DesignerOp { Op = "add", Type = "BindingNavigator" } });
            var nav = (BindingNavigator)surface.Model.Find("bindingNavigator1")!.Instance;
            Assert.Equal(11, nav.Items.Count);
            Assert.NotNull(surface.Model.Find("bindingNavigatorMoveFirstItem"));
            Assert.NotNull(surface.Model.Find("bindingNavigatorPositionItem"));
            Assert.Same(nav.MoveFirstItem, surface.Model.Find("bindingNavigatorMoveFirstItem")!.Instance);

            var code = surface.Source;
            Assert.Contains("bindingNavigator1 = new BindingNavigator(components);", code);
            Assert.Contains("bindingNavigator1.MoveFirstItem = bindingNavigatorMoveFirstItem;", code);
            Assert.Contains("bindingNavigatorMoveFirstItem = new ToolStripButton();", code);

            // And it reads back: the items are the navigator's, wired to it.
            var reread = new DesignerCodeReader().Read(code);
            var nav2 = (BindingNavigator)reread.Find("bindingNavigator1")!.Instance;
            Assert.Equal(11, nav2.Items.Count);
            Assert.Same(reread.Find("bindingNavigatorDeleteItem")!.Instance, nav2.DeleteItem);
            // The standard pictures are not in the code; the navigator puts them back (EndInit).
            Assert.DoesNotContain("bindingNavigatorMoveFirstItem.Image", code);
            Assert.NotNull(nav2.MoveFirstItem!.Image);
        }
        finally
        {
            Directory.Delete(path, true);
        }
    }

    // --- renderers ------------------------------------------------------------------------------------------

    [Fact]
    public void TheRenderersPaintInTheThemeOfTheControls()
    {
        using var bmp = new Bitmap(200, 60);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.White);
            ButtonRenderer.DrawButton(g, new Rectangle(0, 0, 80, 24), "OK", SystemFonts.DefaultFont, false, PushButtonState.Normal);
            CheckBoxRenderer.DrawCheckBox(g, new Point(90, 5), CheckBoxState.CheckedNormal);
            RadioButtonRenderer.DrawRadioButton(g, new Point(110, 5), RadioButtonState.CheckedNormal);
            ComboBoxRenderer.DrawTextBox(g, new Rectangle(0, 30, 100, 22), "Item", SystemFonts.DefaultFont, ComboBoxState.Normal);
            ComboBoxRenderer.DrawDropDownButton(g, new Rectangle(82, 31, 17, 20), ComboBoxState.Hot);
            ProgressBarRenderer.DrawHorizontalBar(g, new Rectangle(110, 30, 80, 12));
            ProgressBarRenderer.DrawHorizontalChunks(g, new Rectangle(111, 31, 40, 10));
            ScrollBarRenderer.DrawArrowButton(g, new Rectangle(160, 0, 17, 17), ScrollBarArrowButtonState.UpNormal);
            ScrollBarRenderer.DrawVerticalThumb(g, new Rectangle(180, 0, 17, 30), ScrollBarState.Hot);
            ScrollBarRenderer.DrawSizeBox(g, new Rectangle(180, 40, 17, 17), ScrollBarSizeBoxState.RightAlign);
        }
        // The button face is the theme's (not white), the check box is ticked, the progress fill is the theme's green.
        Assert.Equal(Color.FromArgb(0xE1, 0xE1, 0xE1).ToArgb(), bmp.GetPixel(5, 12).ToArgb());
        Assert.NotEqual(Color.White.ToArgb(), bmp.GetPixel(96, 11).ToArgb());
        Assert.Equal(Color.FromArgb(0x06, 0xB0, 0x25).ToArgb(), bmp.GetPixel(120, 35).ToArgb());
        Assert.True(ComboBoxRenderer.IsSupported && ProgressBarRenderer.IsSupported && ScrollBarRenderer.IsSupported);
        using (var g = Graphics.FromImage(bmp))
        {
            Assert.Equal(new Size(13, 13), CheckBoxRenderer.GetGlyphSize(g, CheckBoxState.UncheckedNormal));
            Assert.Equal(new Size(13, 13), RadioButtonRenderer.GetGlyphSize(g, RadioButtonState.UncheckedNormal));
        }
        Assert.Throws<ArgumentNullException>(() => ButtonRenderer.DrawButton(null!, Rectangle.Empty, PushButtonState.Normal));
    }

    [Fact]
    public void TheNewControlsAreInTheToolbox()
    {
        var all = DesignerToolbox.Categories().First(c => c.Name == "All Windows Forms").Items.Select(i => i.Name).ToList();
        Assert.Contains("DomainUpDown", all);
        Assert.Contains("HelpProvider", all);
        Assert.Contains("Splitter", all);
        Assert.Contains("BindingNavigator", all);
        var data = DesignerToolbox.Categories().First(c => c.Name == "Data").Items.Select(i => i.Name).ToList();
        Assert.Equal(new[] { "BindingNavigator", "BindingSource", "DataGridView" }, data);
        var components = DesignerToolbox.Categories().First(c => c.Name == "Components");
        Assert.True(components.Items.Single(i => i.Name == "HelpProvider").Tray);
    }
}
