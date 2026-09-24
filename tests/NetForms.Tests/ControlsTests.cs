using NetForms.Platform;
using Xunit;

namespace NetForms.Tests;

/// <summary>Behaviour of the Ф2 controls, driven through the test platform (mouse, keys, text input).</summary>
public class ControlsTests
{
    private static (TestPlatform platform, Form form, TestWindow window) ShowForm(params Control[] controls)
    {
        var platform = TestPlatform.Install();
        var form = new Form { ClientSize = new Size(400, 300) };
        foreach (var c in controls) form.Controls.Add(c);
        form.Show();
        return (platform, form, platform.Windows.Last());
    }

    private static void Type(TestWindow w, string text) => w.Host.TextInput(text);

    private static void Key(TestWindow w, Keys key, InputModifiers mods = InputModifiers.None)
    {
        w.Host.KeyDown((int)(key & Keys.KeyCode), mods);
        w.Host.KeyUp((int)(key & Keys.KeyCode), mods);
    }

    [Fact]
    public void CheckBoxTogglesOnClickAndCyclesThreeStates()
    {
        var box = new CheckBox { Bounds = new Rectangle(10, 10, 120, 24), Text = "Check" };
        var (_, form, window) = ShowForm(box);
        using (form)
        {
            int changes = 0;
            box.CheckedChanged += (_, _) => changes++;
            window.Click(new Point(20, 20));
            Assert.True(box.Checked);
            Assert.Equal(1, changes);

            box.ThreeState = true;
            window.Click(new Point(20, 20));
            Assert.Equal(CheckState.Indeterminate, box.CheckState);
            Assert.True(box.Checked);
            window.Click(new Point(20, 20));
            Assert.Equal(CheckState.Unchecked, box.CheckState);
            Assert.Equal(2, changes);

            // Space toggles the focused box.
            Key(window, Keys.Space);
            Assert.Equal(CheckState.Checked, box.CheckState);
        }
    }

    [Fact]
    public void RadioButtonsInOneContainerAreExclusive()
    {
        var a = new RadioButton { Bounds = new Rectangle(10, 10, 100, 24), Text = "A", Checked = true };
        var b = new RadioButton { Bounds = new Rectangle(10, 40, 100, 24), Text = "B" };
        var other = new RadioButton { Bounds = new Rectangle(10, 10, 100, 24), Text = "C", Checked = true };
        var panel = new Panel { Bounds = new Rectangle(200, 10, 150, 100) };
        panel.Controls.Add(other);
        var (_, form, window) = ShowForm(a, b, panel);
        using (form)
        {
            window.Click(new Point(20, 50));
            Assert.True(b.Checked);
            Assert.False(a.Checked);
            Assert.True(other.Checked); // a different group
            Assert.True(b.TabStop);
            Assert.False(a.TabStop);
        }
    }

    [Fact]
    public void GroupBoxInsetsItsDisplayRectangleAndDocksChildrenInsideIt()
    {
        var group = new GroupBox { Size = new Size(200, 100), Text = "G" };
        var fill = new Control { Dock = DockStyle.Fill };
        group.Controls.Add(fill);
        group.PerformLayout();
        var display = group.DisplayRectangle;
        Assert.Equal(3, display.X);
        Assert.Equal(group.Font.Height + 3, display.Y);
        Assert.Equal(display, fill.Bounds);
    }

    [Fact]
    public void TabControlShowsOnlyTheSelectedPageAndCanCancelSelection()
    {
        var tabs = new TabControl { Bounds = new Rectangle(0, 0, 300, 200) };
        var p1 = new TabPage("One");
        var p2 = new TabPage("Two");
        var p3 = new TabPage("Three");
        tabs.TabPages.AddRange(new[] { p1, p2, p3 });
        var (_, form, window) = ShowForm(tabs);
        using (form)
        {
            Assert.Equal(0, tabs.SelectedIndex);
            Assert.True(p1.Visible);
            Assert.False(p2.Visible);
            Assert.Equal(tabs.DisplayRectangle, p1.Bounds);

            var events = new List<string>();
            tabs.Selecting += (_, e) => events.Add("Selecting:" + e.TabPageIndex);
            tabs.Selected += (_, e) => events.Add("Selected:" + e.TabPageIndex);
            tabs.Deselected += (_, e) => events.Add("Deselected:" + e.TabPageIndex);
            tabs.SelectedIndexChanged += (_, _) => events.Add("Changed");

            var rect = tabs.GetTabRect(1);
            window.Click(new Point(rect.X + 5, rect.Y + 5));
            Assert.Equal(1, tabs.SelectedIndex);
            Assert.True(p2.Visible);
            Assert.False(p1.Visible);
            Assert.Equal(new[] { "Selecting:1", "Deselected:0", "Selected:1", "Changed" }, events);

            tabs.Selecting += (_, e) => e.Cancel = e.TabPageIndex == 2;
            tabs.SelectedIndex = 2;
            Assert.Equal(1, tabs.SelectedIndex);

            Key(window, Keys.Tab, InputModifiers.Control);
            Assert.Equal(1, tabs.SelectedIndex); // page 2 is vetoed, the selection stays
            tabs.TabPages.Remove(p3);
            Key(window, Keys.Tab, InputModifiers.Control);
            Assert.Equal(0, tabs.SelectedIndex);
        }
    }

    [Fact]
    public void SplitContainerLaysOutPanelsAndHonoursFixedPanel()
    {
        var split = new SplitContainer { Size = new Size(300, 100), SplitterDistance = 100 };
        Assert.Equal(new Rectangle(0, 0, 100, 100), split.Panel1.Bounds);
        Assert.Equal(new Rectangle(104, 0, 196, 100), split.Panel2.Bounds);

        split.FixedPanel = FixedPanel.Panel2;
        split.Width = 400;
        Assert.Equal(200, split.SplitterDistance);
        Assert.Equal(new Rectangle(204, 0, 196, 100), split.Panel2.Bounds);

        split.FixedPanel = FixedPanel.Panel1;
        split.Width = 500;
        Assert.Equal(200, split.SplitterDistance);
        Assert.Equal(296, split.Panel2.Width);

        split.SplitterDistance = 5;
        Assert.Equal(split.Panel1MinSize, split.SplitterDistance);
        split.SplitterDistance = 1000;
        Assert.Equal(500 - 4 - split.Panel2MinSize, split.SplitterDistance);

        split.Panel1Collapsed = true;
        Assert.Equal(new Rectangle(0, 0, 500, 100), split.Panel2.Bounds);
    }

    [Fact]
    public void SplitterDragsWithTheMouse()
    {
        var split = new SplitContainer { Bounds = new Rectangle(0, 0, 300, 100), SplitterDistance = 100 };
        var (_, form, window) = ShowForm(split);
        using (form)
        {
            int moved = 0;
            split.SplitterMoved += (_, _) => moved++;
            window.Host.MouseDown(MouseButton.Left, new Point(102, 50), 1, InputModifiers.None);
            window.Host.MouseMove(new Point(152, 50), MouseButton.Left, InputModifiers.None);
            window.Host.MouseUp(MouseButton.Left, new Point(152, 50), InputModifiers.None);
            Assert.Equal(150, split.SplitterDistance);
            Assert.Equal(1, moved);
            Assert.Equal(154, split.Panel2.Left);
        }
    }

    [Fact]
    public void ListBoxSelectsByMouseAndKeyboardAndScrolls()
    {
        var list = new ListBox { Bounds = new Rectangle(10, 10, 120, 62), ItemHeight = 15 };
        for (int i = 0; i < 20; i++) list.Items.Add("Item " + i);
        var (_, form, window) = ShowForm(list);
        using (form)
        {
            int changes = 0;
            list.SelectedIndexChanged += (_, _) => changes++;
            var rect = list.GetItemRectangle(2);
            window.Click(new Point(10 + rect.X + 5, 10 + rect.Y + 5));
            Assert.Equal(2, list.SelectedIndex);
            Assert.Equal("Item 2", list.Text);
            Assert.Equal(1, changes);

            Key(window, Keys.Down);
            Assert.Equal(3, list.SelectedIndex);
            Key(window, Keys.End);
            Assert.Equal(19, list.SelectedIndex);
            Assert.True(list.TopIndex > 0, "list should have scrolled to show the last item");
            Assert.Equal(19, list.IndexFromPoint(list.GetItemRectangle(19).Location));

            list.SelectionMode = SelectionMode.MultiExtended;
            list.SelectedIndex = 5;
            Key(window, Keys.Down, InputModifiers.Shift);
            Key(window, Keys.Down, InputModifiers.Shift);
            Assert.Equal(new[] { 5, 6, 7 }, list.SelectedIndices.ToArray());
            Assert.Equal(3, list.SelectedItems.Count);

            list.Items.RemoveAt(6);
            Assert.Equal(new[] { 5, 6 }, list.SelectedIndices.ToArray());
        }
    }

    [Fact]
    public void ListBoxSortedAndDisplayMember()
    {
        var list = new ListBox { Sorted = true, DisplayMember = "Name" };
        list.Items.Add(new { Name = "Pear" });
        list.Items.Add(new { Name = "Apple" });
        list.Items.Add(new { Name = "Mango" });
        Assert.Equal("Apple", list.GetItemText(list.Items[0]));
        Assert.Equal("Pear", list.GetItemText(list.Items[2]));
        Assert.Equal(1, list.FindString("ma"));
    }

    [Fact]
    public void TextBoxEditsThroughKeyboardAndClipboard()
    {
        var box = new TextBox { Bounds = new Rectangle(10, 10, 200, 23) };
        var (platform, form, window) = ShowForm(box);
        using (form)
        {
            Assert.True(box.Focused);
            int changed = 0;
            box.TextChanged += (_, _) => changed++;

            Type(window, "Hello");
            Assert.Equal("Hello", box.Text);
            Assert.Equal(5, box.SelectionStart);
            Assert.Equal(5, changed);

            Key(window, Keys.Back);
            Assert.Equal("Hell", box.Text);
            Type(window, "o world");
            Key(window, Keys.Home);
            Key(window, Keys.Right, InputModifiers.Shift);
            Key(window, Keys.Right, InputModifiers.Shift);
            Assert.Equal("He", box.SelectedText);
            Type(window, "J");
            Assert.Equal("Jllo world", box.Text);

            Key(window, Keys.A, InputModifiers.Control);
            Assert.Equal(box.Text.Length, box.SelectionLength);
            Key(window, Keys.C, InputModifiers.Control);
            Assert.Equal("Jllo world", platform.ClipboardText);
            Key(window, Keys.End);
            Key(window, Keys.V, InputModifiers.Control);
            Assert.Equal("Jllo worldJllo world", box.Text);

            Key(window, Keys.Z, InputModifiers.Control);
            Assert.Equal("Jllo world", box.Text);
            Key(window, Keys.Y, InputModifiers.Control);
            Assert.Equal("Jllo worldJllo world", box.Text);
            Assert.True(box.Modified);

            box.MaxLength = 22;
            Key(window, Keys.End);
            Type(window, "abcdef");
            Assert.Equal(22, box.TextLength);

            box.ReadOnly = true;
            Type(window, "x");
            Assert.Equal(22, box.TextLength);
        }
    }

    [Fact]
    public void TextBoxMultilineWrapsAndReportsPositions()
    {
        var box = new TextBox { Bounds = new Rectangle(10, 10, 120, 80), Multiline = true, AcceptsReturn = true, Font = Golden.Font };
        var (_, form, window) = ShowForm(box);
        using (form)
        {
            Type(window, "first line");
            Key(window, Keys.Return);
            Type(window, "a second line that is long enough to wrap around");
            Assert.Equal(2, box.Lines.Length);
            Assert.True(box.GetLineFromCharIndex(box.TextLength) >= 2, "long line should wrap into visual lines");
            Assert.Equal(0, box.GetLineFromCharIndex(3));
            // "first line" + "\r\n" - line breaks are two characters, as in the Win32 edit control.
            Assert.Equal(12, box.GetFirstCharIndexFromLine(1));
            Assert.Equal("first line\r\na second line that is long enough to wrap around", box.Text);

            var p0 = box.GetPositionFromCharIndex(0);
            var p5 = box.GetPositionFromCharIndex(5);
            Assert.True(p5.X > p0.X);
            Assert.Equal(p0.Y, p5.Y);
            Assert.Equal(box.LineHeight, box.GetPositionFromCharIndex(12).Y - p0.Y);
            Assert.Equal(5, box.GetCharIndexFromPosition(p5));

            Key(window, Keys.Home, InputModifiers.Control);
            Key(window, Keys.Down);
            Assert.Equal(12, box.SelectionStart);
            Key(window, Keys.Left);
            Assert.Equal(10, box.SelectionStart); // steps over the whole "\r\n"
        }
    }

    [Fact]
    public void TextBoxPasswordCharHidesText()
    {
        var box = new TextBox { Bounds = new Rectangle(10, 10, 100, 23), PasswordChar = '*', Text = "abc" };
        Assert.Equal("***", box.DisplayText("abc".AsSpan()));
        using var bmp = new Bitmap(100, 23);
        box.DrawToBitmap(bmp, new Rectangle(0, 0, 100, 23));
    }

    [Fact]
    public void ComboBoxDropDownListNavigatesAndCommits()
    {
        var combo = new ComboBox { Bounds = new Rectangle(10, 10, 150, 23), DropDownStyle = ComboBoxStyle.DropDownList };
        combo.Items.AddRange(new object[] { "One", "Two", "Three" });
        combo.SelectedIndex = 0;
        var (platform, form, window) = ShowForm(combo);
        using (form)
        {
            int committed = 0, changed = 0;
            combo.SelectionChangeCommitted += (_, _) => committed++;
            combo.SelectedIndexChanged += (_, _) => changed++;

            Key(window, Keys.Down);
            Assert.Equal(1, combo.SelectedIndex);
            Assert.Equal("Two", combo.Text);
            Assert.Equal(1, committed);

            // Open the list: a second, non-activating window appears; Down moves the highlight; Enter commits.
            int windows = platform.Windows.Count;
            window.Click(new Point(100, 20));
            Assert.True(combo.DroppedDown);
            Assert.Equal(windows + 1, platform.Windows.Count);
            var popup = platform.Windows.Last();
            Assert.False(popup.ShowActivated);
            Assert.True(popup.IsVisible);

            Key(window, Keys.Down);
            Key(window, Keys.Return);
            Assert.False(combo.DroppedDown);
            Assert.Equal(2, combo.SelectedIndex);
            Assert.Equal(2, committed);
            Assert.False(popup.IsVisible);

            // Escape closes without changing.
            window.Click(new Point(100, 20));
            Key(window, Keys.Up);
            Key(window, Keys.Escape);
            Assert.Equal(2, combo.SelectedIndex);
            Assert.False(combo.DroppedDown);
        }
    }

    [Fact]
    public void ComboBoxEditableTextFollowsSelectionAndTyping()
    {
        var combo = new ComboBox { Bounds = new Rectangle(10, 10, 150, 23) };
        combo.Items.AddRange(new object[] { "Red", "Green" });
        var (_, form, window) = ShowForm(combo);
        using (form)
        {
            combo.SelectedIndex = 1;
            Assert.Equal("Green", combo.Text);
            combo.Text = "Custom";
            Assert.Equal(-1, combo.SelectedIndex);
            Assert.Equal("Custom", combo.Text);
            combo.Text = "red";
            Assert.Equal(0, combo.SelectedIndex);
        }
    }

    [Fact]
    public void NumericUpDownSpinsClampsAndParses()
    {
        var spin = new NumericUpDown { Bounds = new Rectangle(10, 10, 80, 23), Minimum = 0, Maximum = 5, Increment = 2 };
        var (_, form, window) = ShowForm(spin);
        using (form)
        {
            int changes = 0;
            spin.ValueChanged += (_, _) => changes++;
            spin.UpButton();
            spin.UpButton();
            spin.UpButton();
            Assert.Equal(5m, spin.Value);
            Assert.Equal(3, changes);
            spin.DownButton();
            Assert.Equal(3m, spin.Value);

            spin.Text = "4";
            Assert.Equal(4m, spin.Value);
            spin.Text = "99";
            Assert.Equal(5m, spin.Value);
            Assert.Throws<ArgumentOutOfRangeException>(() => spin.Value = 6);

            // Clicking the upper half of the spin buttons increments.
            window.Click(new Point(10 + spin.Width - 5, 10 + 4));
            Assert.Equal(5m, spin.Value);
            spin.Value = 1;
            window.Click(new Point(10 + spin.Width - 5, 10 + spin.Height - 4));
            Assert.Equal(0m, spin.Value);
            spin.DecimalPlaces = 2;
            Assert.Equal("0.00".Replace('.', System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0]), spin.Text);
        }
    }

    [Fact]
    public void TrackBarRespondsToKeysAndClicks()
    {
        var bar = new TrackBar { Bounds = new Rectangle(10, 10, 200, 45), Maximum = 10, Value = 5 };
        var (_, form, window) = ShowForm(bar);
        using (form)
        {
            int scrolls = 0;
            bar.Scroll += (_, _) => scrolls++;
            Key(window, Keys.Right);
            Assert.Equal(6, bar.Value);
            Key(window, Keys.End);
            Assert.Equal(10, bar.Value);
            Key(window, Keys.PageDown);
            Assert.Equal(5, bar.Value);
            Assert.Equal(3, scrolls);

            // A click left of the thumb pages down.
            window.Click(new Point(10 + 20, 10 + 18));
            Assert.Equal(0, bar.Value);
            window.Host.MouseWheel(new Point(100, 30), 120, InputModifiers.None);
            Assert.Equal(1, bar.Value);
        }
    }

    [Fact]
    public void ScrollBarArrowsAndTrackChangeTheValue()
    {
        var bar = new VScrollBar { Bounds = new Rectangle(10, 10, 17, 200), Minimum = 0, Maximum = 100, LargeChange = 10, SmallChange = 2 };
        var (_, form, window) = ShowForm(bar);
        using (form)
        {
            var events = new List<ScrollEventType>();
            bar.Scroll += (_, e) => events.Add(e.Type);
            window.Click(new Point(18, 10 + 200 - 5)); // bottom arrow
            Assert.Equal(2, bar.Value);
            window.Click(new Point(18, 10 + 150)); // track below the thumb
            Assert.Equal(12, bar.Value);
            Assert.Contains(ScrollEventType.SmallIncrement, events);
            Assert.Contains(ScrollEventType.LargeIncrement, events);
            Assert.Equal(ScrollEventType.EndScroll, events.Last());
            Assert.Equal(91, bar.Maximum - bar.LargeChange + 1);
            bar.Value = 91;
            window.Click(new Point(18, 10 + 200 - 5));
            Assert.Equal(91, bar.Value);
        }
    }

    [Fact]
    public void ToolTipAppearsAfterTheDelayAndHidesOnLeave()
    {
        var button = new Button { Bounds = new Rectangle(10, 10, 100, 30), Text = "Hover" };
        var (platform, form, window) = ShowForm(button);
        using (form)
        using (var tip = new ToolTip { InitialDelay = 300 })
        {
            tip.SetToolTip(button, "Helpful text");
            Assert.Equal("Helpful text", tip.GetToolTip(button));
            int windows = platform.Windows.Count;

            window.Host.MouseMove(new Point(20, 20), MouseButton.None, InputModifiers.None);
            var timer = platform.Timers.Single(t => t.IsRunning && t.Interval == TimeSpan.FromMilliseconds(300));
            timer.Fire();
            Assert.Equal(windows + 1, platform.Windows.Count);
            var popup = platform.Windows.Last();
            Assert.True(popup.IsVisible);
            Assert.False(popup.ShowActivated);
            Assert.True(popup.ClientSize.Width > 40 && popup.ClientSize.Height > 10);

            window.Host.MouseMove(new Point(300, 200), MouseButton.None, InputModifiers.None);
            Assert.False(popup.IsVisible);
        }
    }

    [Fact]
    public void ProgressBarPaintsItsFraction()
    {
        var bar = new ProgressBar { Size = new Size(102, 20), Value = 50 };
        using var bmp = new Bitmap(102, 20);
        bar.DrawToBitmap(bmp, new Rectangle(0, 0, 102, 20));
        Assert.Equal(Theme.ProgressFill.ToArgb(), bmp.GetPixel(30, 10).ToArgb());
        Assert.Equal(Theme.ProgressTrack.ToArgb(), bmp.GetPixel(80, 10).ToArgb());
        bar.PerformStep();
        Assert.Equal(60, bar.Value);
        bar.Increment(100);
        Assert.Equal(100, bar.Value);
    }

    [Fact]
    public void LinkLabelRaisesLinkClicked()
    {
        var link = new LinkLabel { Bounds = new Rectangle(10, 10, 100, 20), Text = "Click here" };
        var (_, form, window) = ShowForm(link);
        using (form)
        {
            LinkLabel.Link? clicked = null;
            link.LinkClicked += (_, e) => clicked = e.Link;
            window.Click(new Point(15, 18));
            Assert.NotNull(clicked);
            Assert.Equal(0, clicked!.Start);
            Assert.Equal(link.Text.Length, clicked.Length);
            Assert.Equal("Hand", window.CursorName);
        }
    }

    [Fact]
    public void PictureBoxSizeModes()
    {
        using var image = new Bitmap(40, 20);
        var box = new PictureBox { Size = new Size(100, 100), Image = image };
        Assert.Equal(new Rectangle(0, 0, 40, 20), box.ImageRectangle);
        box.SizeMode = PictureBoxSizeMode.CenterImage;
        Assert.Equal(new Rectangle(30, 40, 40, 20), box.ImageRectangle);
        box.SizeMode = PictureBoxSizeMode.Zoom;
        Assert.Equal(new Rectangle(0, 25, 100, 50), box.ImageRectangle);
        box.SizeMode = PictureBoxSizeMode.StretchImage;
        Assert.Equal(new Rectangle(0, 0, 100, 100), box.ImageRectangle);
        box.SizeMode = PictureBoxSizeMode.AutoSize;
        Assert.Equal(new Size(40, 20), box.Size);
    }
}
