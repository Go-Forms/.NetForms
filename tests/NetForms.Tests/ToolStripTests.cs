using NetForms.Platform;
using Xunit;

namespace NetForms.Tests;

/// <summary>
/// Behaviour of the Ф4 strips: item layout on a tool bar, menu bar and status bar, the
/// drop-down chain (open, hover, click, Escape), context menus and menu shortcuts.
/// </summary>
public class ToolStripTests
{
    private static (TestPlatform platform, Form form, TestWindow window) ShowForm(params Control[] controls)
    {
        var platform = TestPlatform.Install();
        var form = new Form { ClientSize = new Size(400, 300) };
        foreach (var c in controls) form.Controls.Add(c);
        form.Show();
        return (platform, form, platform.Windows.Last());
    }

    private static void Key(TestWindow w, Keys key, InputModifiers mods = InputModifiers.None)
    {
        w.Host.KeyDown((int)(key & Keys.KeyCode), mods);
        w.Host.KeyUp((int)(key & Keys.KeyCode), mods);
    }

    // --- layout -------------------------------------------------------------------------

    [Fact]
    public void ToolStripStacksItemsAfterTheGripAndStretchesThemAcrossTheStrip()
    {
        var strip = new ToolStrip();
        var first = new ToolStripButton("New");
        var second = new ToolStripButton("Open");
        strip.Items.AddRange(new ToolStripItem[] { first, second });
        var (_, form, _) = ShowForm(strip);
        using (form)
        {
            // Docked Top by default; Padding (0,0,1,0) and a 5px grip (visual styles) with a 2px margin,
            // the numbers real WinForms reports (tests/Shared/CompatScenarios.cs, "exact/toolstrip/*").
            Assert.Equal(new Size(400, 25), strip.Size);
            Assert.Equal(DockStyle.Top, strip.Dock);
            Assert.Equal(new Rectangle(2, 0, ToolStrip.GripThickness, 25), strip.GripRectangle);

            // Items start after the grip and its margins, and take the strip's height minus their margin.
            Assert.Equal(9, first.Bounds.Left);
            Assert.Equal(1, first.Bounds.Top);
            Assert.Equal(22, first.Bounds.Height);
            Assert.Equal(first.Bounds.Right, second.Bounds.Left);
            Assert.Equal(ToolStripItemPlacement.Main, second.Placement);
        }
    }

    [Fact]
    public void HiddenGripGivesTheRoomBackToTheItems()
    {
        var strip = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };
        var button = new ToolStripButton("New");
        strip.Items.Add(button);
        var (_, form, _) = ShowForm(strip);
        using (form)
        {
            Assert.Equal(Rectangle.Empty, strip.GripRectangle);
            Assert.Equal(0, button.Bounds.Left);
        }
    }

    [Fact]
    public void RightAlignedItemsAreLaidOutFromTheOtherEnd()
    {
        var strip = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, CanOverflow = false };
        var left = new ToolStripButton("Left");
        var right = new ToolStripButton("Right") { Alignment = ToolStripItemAlignment.Right };
        strip.Items.AddRange(new ToolStripItem[] { left, right });
        var (_, form, _) = ShowForm(strip);
        using (form)
        {
            Assert.Equal(0, left.Bounds.Left);
            // Right against the display rectangle: the strip is 400 wide with a 1px right padding.
            Assert.Equal(399, right.Bounds.Right);
        }
    }

    [Fact]
    public void ItemsThatDoNotFitMoveToTheOverflow()
    {
        // AutoSize off: an undocked AutoSize strip grows to fit its items instead (as in WinForms).
        var strip = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, AutoSize = false, Width = 120 };
        var items = new ToolStripItem[]
        {
            new ToolStripButton("First"), new ToolStripButton("Second"), new ToolStripButton("Third"), new ToolStripButton("Fourth"),
        };
        strip.Items.AddRange(items);
        var (_, form, _) = ShowForm();
        using (form)
        {
            form.Controls.Add(strip);
            strip.Dock = DockStyle.None;
            strip.Width = 120;
            strip.PerformLayout();

            Assert.Contains(items, i => i.Placement == ToolStripItemPlacement.Overflow);
            Assert.Equal(ToolStripItemPlacement.Main, items[0].Placement);
            Assert.True(strip.OverflowButton.HasDropDownItems);
            // Everything still on the strip fits inside it, with the chevron's 16px reserved.
            foreach (var item in items)
            {
                if (item.Placement == ToolStripItemPlacement.Main) Assert.True(item.Bounds.Right <= 120 - ToolStrip.OverflowButtonWidth);
            }
        }
    }

    [Fact]
    public void StatusStripSpringLabelTakesTheRemainingWidth()
    {
        var strip = new StatusStrip();
        var fixedLabel = new ToolStripStatusLabel("Ready");
        var spring = new ToolStripStatusLabel("Spring") { Spring = true };
        strip.Items.AddRange(new ToolStripItem[] { fixedLabel, spring });
        var (_, form, _) = ShowForm(strip);
        using (form)
        {
            Assert.Equal(new Size(400, 22), strip.Size);
            Assert.Equal(DockStyle.Bottom, strip.Dock);
            // Padding is (1,0,14,0): one pixel at the left, the sizing grip's room at the right.
            Assert.Equal(1, fixedLabel.Bounds.Left);
            Assert.Equal(fixedLabel.Bounds.Right, spring.Bounds.Left);
            Assert.Equal(400 - 14, spring.Bounds.Right);
            Assert.True(spring.Width > fixedLabel.Width);
        }
    }

    [Fact]
    public void MenuStripItemsAreSizedByTheirCaptionPlusPaddingAndBorder()
    {
        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("&File");
        var edit = new ToolStripMenuItem("&Edit");
        menu.Items.AddRange(new ToolStripItem[] { file, edit });
        var (_, form, _) = ShowForm(menu);
        using (form)
        {
            Assert.Equal(24, menu.Height);
            // MenuStrip padding is (6,2,0,2), the items carry no margin of their own.
            Assert.Equal(6, file.Bounds.Left);
            Assert.Equal(2, file.Bounds.Top);
            Assert.Equal(20, file.Bounds.Height);
            Assert.Equal(file.Bounds.Right, edit.Bounds.Left);
            Assert.Equal(new Padding(4, 0, 4, 0), file.Padding);
            // The ampersand is a mnemonic marker, not part of the measured text; the item adds
            // its 4+4 padding and the 2px border on each side, as WinForms does.
            Assert.Equal(TextRenderer.MeasureText("File", file.Font).Width + 8 + 4, file.Width);
        }
    }

    [Fact]
    public void DropDownMenuStacksFullWidthRowsWithRoomForCheckAndShortcut()
    {
        var item = new ToolStripMenuItem("&File");
        var open = new ToolStripMenuItem("Open") { ShortcutKeys = Keys.Control | Keys.O };
        var separator = new ToolStripSeparator();
        var exit = new ToolStripMenuItem("Exit");
        item.DropDownItems.AddRange(new ToolStripItem[] { open, separator, exit });

        var menu = new MenuStrip();
        menu.Items.Add(item);
        var (_, form, window) = ShowForm(menu);
        using (form)
        {
            window.Click(new Point(item.Bounds.Left + 5, 10));
            Assert.True(item.DropDownVisible);

            var dropDown = item.DropDown;
            // Rows are the full width of the menu, 22px tall; a separator is 6px.
            Assert.Equal(22, open.Height);
            Assert.Equal(6, separator.Height);
            Assert.Equal(open.Bounds.Left, exit.Bounds.Left);
            Assert.Equal(open.Width, exit.Width);
            Assert.Equal(open.Bounds.Bottom, separator.Bounds.Top);

            // The text starts past the image margin, and the shortcut is measured into the width.
            var (image, text, shortcut, arrow) = open.LayoutMenuItem();
            Assert.Equal(ToolStripDropDownMenu.DefaultImageMarginWidth + 8, text.Left);
            Assert.True(image.Right <= ToolStripDropDownMenu.DefaultImageMarginWidth);
            Assert.False(shortcut.IsEmpty);
            Assert.True(arrow.IsEmpty);
            Assert.Equal("Ctrl+O", open.ShortcutText);
            Assert.True(dropDown.Width > text.Left + shortcut.Width);
        }
    }

    // --- drop-down behaviour --------------------------------------------------------------

    [Fact]
    public void ClickingAMenuOpensItAndClickingAnEntryClosesTheWholeChain()
    {
        var file = new ToolStripMenuItem("&File");
        var exit = new ToolStripMenuItem("E&xit");
        file.DropDownItems.Add(exit);
        var menu = new MenuStrip();
        menu.Items.Add(file);

        var (platform, form, window) = ShowForm(menu);
        using (form)
        {
            int clicked = 0, itemClicked = 0;
            exit.Click += (_, _) => clicked++;
            file.DropDownItemClicked += (_, _) => itemClicked++;

            window.Click(new Point(file.Bounds.Left + 5, 10));
            Assert.True(file.DropDownVisible);
            Assert.True(file.Pressed);

            // The drop-down lives in its own window, so it can extend past the form.
            var popup = platform.Windows.Last();
            Assert.NotSame(window, popup);
            popup.Click(new Point(exit.Bounds.Left + 5, exit.Bounds.Top + 5));

            Assert.Equal(1, clicked);
            Assert.Equal(1, itemClicked);
            Assert.False(file.DropDownVisible);
        }
    }

    [Fact]
    public void HoveringASubmenuOpensItAndMovingAwayClosesIt()
    {
        var file = new ToolStripMenuItem("&File");
        var recent = new ToolStripMenuItem("Recent");
        var other = new ToolStripMenuItem("Other");
        recent.DropDownItems.Add(new ToolStripMenuItem("a.txt"));
        file.DropDownItems.AddRange(new ToolStripItem[] { recent, other });
        var menu = new MenuStrip();
        menu.Items.Add(file);

        var (platform, form, window) = ShowForm(menu);
        using (form)
        {
            window.Click(new Point(file.Bounds.Left + 5, 10));
            var popup = platform.Windows.Last();

            popup.Host.MouseMove(new Point(recent.Bounds.Left + 5, recent.Bounds.Top + 5), MouseButton.None, InputModifiers.None);
            Assert.True(recent.DropDownVisible);

            popup.Host.MouseMove(new Point(other.Bounds.Left + 5, other.Bounds.Top + 5), MouseButton.None, InputModifiers.None);
            Assert.False(recent.DropDownVisible);
            Assert.True(file.DropDownVisible);
        }
    }

    [Fact]
    public void EscapeClosesTheInnermostMenuAndThenTheWholeChain()
    {
        var file = new ToolStripMenuItem("&File");
        var recent = new ToolStripMenuItem("Recent");
        recent.DropDownItems.Add(new ToolStripMenuItem("a.txt"));
        file.DropDownItems.Add(recent);
        var menu = new MenuStrip();
        menu.Items.Add(file);

        var (platform, form, window) = ShowForm(menu);
        using (form)
        {
            window.Click(new Point(file.Bounds.Left + 5, 10));
            platform.Windows.Last().Host.MouseMove(new Point(recent.Bounds.Left + 5, recent.Bounds.Top + 5), MouseButton.None, InputModifiers.None);
            Assert.True(recent.DropDownVisible);

            Key(window, Keys.Escape);
            Assert.False(recent.DropDownVisible);
            Assert.True(file.DropDownVisible);

            Key(window, Keys.Escape);
            Assert.False(file.DropDownVisible);
        }
    }

    [Fact]
    public void ArrowKeysWalkTheOpenMenuAndEnterActivatesTheSelection()
    {
        var file = new ToolStripMenuItem("&File");
        var first = new ToolStripMenuItem("First");
        var second = new ToolStripMenuItem("Second");
        file.DropDownItems.AddRange(new ToolStripItem[] { first, second });
        var menu = new MenuStrip();
        menu.Items.Add(file);

        var (_, form, window) = ShowForm(menu);
        using (form)
        {
            int clicks = 0;
            second.Click += (_, _) => clicks++;

            window.Click(new Point(file.Bounds.Left + 5, 10));
            Key(window, Keys.Down);
            Assert.True(first.Selected);
            Key(window, Keys.Down);
            Assert.True(second.Selected);

            Key(window, Keys.Return);
            Assert.Equal(1, clicks);
            Assert.False(file.DropDownVisible);
        }
    }

    [Fact]
    public void AltOpensTheMatchingTopLevelMenu()
    {
        var file = new ToolStripMenuItem("&File");
        file.DropDownItems.Add(new ToolStripMenuItem("Exit"));
        var menu = new MenuStrip();
        menu.Items.Add(file);

        var (_, form, window) = ShowForm(menu);
        using (form)
        {
            Assert.Same(menu, form.MainMenuStrip);
            window.Host.KeyDown((int)Keys.F, InputModifiers.Alt);
            Assert.True(file.DropDownVisible);
        }
    }

    [Fact]
    public void MenuShortcutFiresTheItemFromAnywhereInTheForm()
    {
        var file = new ToolStripMenuItem("&File");
        var save = new ToolStripMenuItem("Save") { ShortcutKeys = Keys.Control | Keys.S };
        file.DropDownItems.Add(save);
        var menu = new MenuStrip();
        menu.Items.Add(file);
        var box = new TextBox { Bounds = new Rectangle(10, 50, 100, 23) };

        var (_, form, window) = ShowForm(menu, box);
        using (form)
        {
            int saves = 0;
            save.Click += (_, _) => saves++;

            box.Focus();
            window.Host.KeyDown((int)Keys.S, InputModifiers.Control);
            Assert.Equal(1, saves);
            Assert.True(ToolStripManager.IsShortcutDefined(Keys.Control | Keys.S));
            Assert.False(ToolStripManager.IsValidShortcut(Keys.S));
            Assert.True(ToolStripManager.IsValidShortcut(Keys.F5));
        }
    }

    [Fact]
    public void RightClickShowsTheContextMenuStripOfTheControlOrItsParent()
    {
        var menu = new ContextMenuStrip();
        var cut = new ToolStripMenuItem("Cut");
        menu.Items.Add(cut);
        var panel = new Panel { Bounds = new Rectangle(0, 0, 200, 200), ContextMenuStrip = menu };
        var label = new Label { Bounds = new Rectangle(10, 10, 80, 20), Text = "Right-click" };
        panel.Controls.Add(label);

        var (_, form, window) = ShowForm(panel);
        using (form)
        {
            window.Host.MouseDown(MouseButton.Right, new Point(20, 15), 1, InputModifiers.None);
            window.Host.MouseUp(MouseButton.Right, new Point(20, 15), InputModifiers.None);

            Assert.True(menu.Visible);
            // The menu belongs to the panel, but it remembers which control was clicked.
            Assert.Same(label, menu.SourceControl);
            menu.Close();
            Assert.False(menu.Visible);
        }
    }

    // --- items ---------------------------------------------------------------------------

    [Fact]
    public void CheckOnClickTogglesAButtonAndRaisesCheckedChangedOnce()
    {
        var strip = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };
        var bold = new ToolStripButton("B") { CheckOnClick = true };
        strip.Items.Add(bold);
        var (_, form, window) = ShowForm(strip);
        using (form)
        {
            int changes = 0;
            bold.CheckedChanged += (_, _) => changes++;

            window.Click(new Point(bold.Bounds.Left + 3, bold.Bounds.Top + 3));
            Assert.True(bold.Checked);
            Assert.Equal(CheckState.Checked, bold.CheckState);
            Assert.Equal(1, changes);

            window.Click(new Point(bold.Bounds.Left + 3, bold.Bounds.Top + 3));
            Assert.False(bold.Checked);
            Assert.Equal(2, changes);
        }
    }

    [Fact]
    public void AHostedControlBecomesAChildOfTheStripAndFollowsItsItem()
    {
        var strip = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };
        var combo = new ToolStripComboBox();
        var (_, form, _) = ShowForm(strip);
        using (form)
        {
            // Add the items once the strip is docked and 400 wide, so nothing overflows.
            strip.Items.Add(new ToolStripButton("New"));
            strip.Items.Add(combo);

            Assert.Equal(ToolStripItemPlacement.Main, combo.Placement);
            Assert.Contains(combo.Control, strip.Controls);
            Assert.Equal(combo.Bounds.Left, combo.Control.Left);
            // A combo keeps its own height and is centred in the strip.
            Assert.True(combo.Control.Height <= strip.Height);
            Assert.True(combo.Control.Top > 0);
        }
    }

    [Fact]
    public void ItemsCollectionKeepsOwnershipAndKeyLookupInSync()
    {
        var strip = new ToolStrip();
        var a = new ToolStripButton("A") { Name = "a" };
        var b = new ToolStripSeparator();
        strip.Items.AddRange(new ToolStripItem[] { a, b });

        Assert.Same(strip, a.Owner);
        Assert.Same(a, strip.Items["A"]);
        Assert.Equal(0, strip.Items.IndexOfKey("a"));
        Assert.False(b.CanSelect);

        // Adding an item to another strip moves it.
        var other = new ToolStrip();
        other.Items.Add(a);
        Assert.Same(other, a.Owner);
        Assert.DoesNotContain(a, strip.Items);

        var created = strip.Items.Add("-");
        Assert.IsType<ToolStripSeparator>(created);
        strip.Dispose();
        other.Dispose();
    }

    /// <summary>
    /// The Strips sample is designer-shaped code (MainForm.cs + MainForm.Designer.cs); it is
    /// checked for its tree and its docking, not pixel by pixel - the golden references with
    /// text are a known open question (docs/PLAN.md, "Открытые вопросы Ф4").
    /// </summary>
    [Fact]
    public void StripsSampleDocksItsBarsAndWiresItsMenus()
    {
        TestPlatform.Install();
        using var form = new global::Strips.MainForm();
        form.Show();

        var menu = form.MainMenuStrip!;
        var tools = (ToolStrip)form.Controls["toolStrip1"]!;
        var status = (StatusStrip)form.Controls["statusStrip1"]!;
        var editor = (TextBox)form.Controls["editor"]!;

        Assert.Equal("menuStrip1", menu.Name);
        Assert.Equal(3, menu.Items.Count);

        // Menu bar on top, tool bar under it, status bar at the bottom, editor filling the rest.
        Assert.Equal(0, menu.Top);
        Assert.Equal(menu.Bottom, tools.Top);
        Assert.Equal(form.ClientSize.Height, status.Bottom);
        Assert.Equal(tools.Bottom, editor.Top);
        Assert.Equal(status.Top, editor.Bottom);

        var file = (ToolStripMenuItem)menu.Items[0];
        Assert.Equal("&File", file.Text);
        Assert.Equal(5, file.DropDownItems.Count);
        Assert.True(((ToolStripMenuItem)file.DropDownItems[2]).HasDropDownItems);
        Assert.Equal(Keys.Control | Keys.O, ((ToolStripMenuItem)file.DropDownItems[1]).ShortcutKeys);

        // The context menu is wired to the editor and the check states came from the designer.
        Assert.NotNull(editor.ContextMenuStrip);
        Assert.True(((ToolStripMenuItem)((ToolStripMenuItem)menu.Items[2]).DropDownItems[0]).Checked);
        Assert.Equal(3, status.Items.Count);
        Assert.True(((ToolStripStatusLabel)status.Items[1]).Spring);
        Assert.Equal(9, tools.Items.Count);

        // Saved for eyeballing (render-out/strips-sample.png), not compared: see the golden
        // question in docs/PLAN.md.
        var outDir = Path.Combine(AppContext.BaseDirectory, "render-out");
        Directory.CreateDirectory(outDir);
        using var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.ClientSize));
        bitmap.Save(Path.Combine(outDir, "strips-sample.png"));
    }

    [Fact]
    public void EveryStripKindPaintsWithoutThrowing()
    {
        var tools = new ToolStrip();
        tools.Items.AddRange(new ToolStripItem[]
        {
            new ToolStripButton("New"),
            new ToolStripSeparator(),
            new ToolStripButton("Bold") { CheckOnClick = true, Checked = true },
            new ToolStripDropDownButton("View", null, new ToolStripMenuItem("Details")),
            new ToolStripSplitButton("Undo", null, new ToolStripMenuItem("Undo all")),
            new ToolStripLabel("Label") { IsLink = true },
        });

        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("&File");
        file.DropDownItems.AddRange(new ToolStripItem[]
        {
            new ToolStripMenuItem("Open") { ShortcutKeys = Keys.Control | Keys.O },
            new ToolStripMenuItem("Checked") { Checked = true },
            new ToolStripSeparator(),
            new ToolStripMenuItem("Submenu", null, new ToolStripMenuItem("Child")),
            new ToolStripMenuItem("Disabled") { Enabled = false },
        });
        menu.Items.Add(file);

        var status = new StatusStrip();
        status.Items.AddRange(new ToolStripItem[]
        {
            new ToolStripStatusLabel("Ready"),
            new ToolStripStatusLabel("Spring") { Spring = true, BorderSides = ToolStripStatusLabelBorderSides.Left },
            new ToolStripProgressBar { Value = 40 },
        });

        var (platform, form, window) = ShowForm(menu, tools, status);
        using (form)
        {
            tools.Dock = DockStyle.Top;
            form.PerformLayout();
            using var bitmap = window.Paint();
            Assert.Equal(400, bitmap.Width);

            // ...and so does an open menu, drawn in its own window.
            window.Click(new Point(file.Bounds.Left + 5, 10));
            using var popupBitmap = platform.Windows.Last().Paint();
            Assert.True(popupBitmap.Width > 0);
        }
    }
}
