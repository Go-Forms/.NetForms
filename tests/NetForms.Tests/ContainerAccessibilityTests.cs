using NetForms.Platform;
using Xunit;

namespace NetForms.Tests;

/// <summary>
/// The Panel/GroupBox/ScrollableControl surface that was missing (DockPadding, the scroll state, ScrollToControl,
/// GroupBoxRenderer) and the accessibility model behind CreateAccessibilityInstance (decision 154).
/// </summary>
public class ContainerAccessibilityTests
{
    [Fact]
    public void DockPaddingIsThePaddingEdgeByEdge()
    {
        var panel = new Panel();
        panel.DockPadding.All = 7;
        Assert.Equal(new Padding(7), panel.Padding);
        panel.DockPadding.Left = 2;
        Assert.Equal(new Padding(2, 7, 7, 7), panel.Padding);
        Assert.Equal(0, panel.DockPadding.All);
        Assert.Equal("[DockPaddingEdges: All=0, Top=7, Left=2, Bottom=7, Right=7]", panel.DockPadding.ToString());
        var props = System.ComponentModel.TypeDescriptor.GetConverter(panel.DockPadding).GetProperties(panel.DockPadding)!;
        Assert.Equal("All", props[0].Name);
    }

    private sealed class NoJumpPanel : Panel
    {
        public int Asked;

        // The well-known fix for a panel that scrolls to the focused control: stay where it is.
        protected override Point ScrollToControl(Control activeControl)
        {
            Asked++;
            return DisplayRectangle.Location;
        }

        public bool HScrollState => GetScrollState(ScrollStateHScrollVisible);

        public bool AutoScrollState => GetScrollState(ScrollStateAutoScrolling);

        public void SetUserScrolled(bool value) => SetScrollState(ScrollStateUserHasScrolled, value);

        public bool UserScrolled => GetScrollState(ScrollStateUserHasScrolled);
    }

    [Fact]
    public void ScrollControlIntoViewGoesThroughScrollToControl()
    {
        var platform = TestPlatform.Install();
        using var form = new Form { ClientSize = new Size(300, 200) };
        var panel = new NoJumpPanel { Bounds = new Rectangle(0, 0, 200, 100), AutoScroll = true };
        var far = new TextBox { Location = new Point(10, 400) };
        panel.Controls.Add(far);
        form.Controls.Add(panel);
        form.Show();

        Assert.True(panel.AutoScrollState);
        panel.ScrollControlIntoView(far);
        Assert.Equal(1, panel.Asked);
        Assert.Equal(Point.Empty, panel.AutoScrollPosition);

        panel.SetUserScrolled(true);
        Assert.True(panel.UserScrolled);
        panel.SetAutoScrollMargin(-5, 8);
        Assert.Equal(new Size(0, 8), panel.AutoScrollMargin);
    }

    [Fact]
    public void GroupBoxRendererDrawsTheFrameGroupBoxDraws()
    {
        TestPlatform.Install();
        using var bitmap = new Bitmap(120, 60);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.White);
            GroupBoxRenderer.DrawGroupBox(g, new Rectangle(0, 0, 120, 60), "Caption", Control.DefaultFont, System.Windows.Forms.VisualStyles.GroupBoxState.Normal);
        }
        // The bottom edge is a line; the top line is interrupted under the caption.
        Assert.NotEqual(Color.White.ToArgb(), bitmap.GetPixel(60, 59).ToArgb());
        int top = Control.DefaultFont.Height / 2;
        Assert.NotEqual(Color.White.ToArgb(), bitmap.GetPixel(1, top).ToArgb());
        Assert.True(GroupBoxRenderer.IsBackgroundPartiallyTransparent(System.Windows.Forms.VisualStyles.GroupBoxState.Normal));
    }

    [Fact]
    public void ControlsDescribeThemselvesToAccessibilityClients()
    {
        var platform = TestPlatform.Install();
        using var form = new Form { ClientSize = new Size(300, 200) };
        var group = new GroupBox { Text = "&Options", Bounds = new Rectangle(10, 10, 200, 150) };
        var label = new Label { Text = "&Name:", Location = new Point(10, 25), AutoSize = true, TabIndex = 0 };
        var box = new TextBox { Location = new Point(80, 22), TabIndex = 1 };
        var check = new CheckBox { Text = "E&nabled", Location = new Point(10, 60), TabIndex = 2, Checked = true };
        var ok = new Button { Text = "OK", Location = new Point(10, 100), TabIndex = 3 };
        group.Controls.AddRange(new Control[] { label, box, check, ok });
        var panel = new Panel { Bounds = new Rectangle(220, 10, 60, 60), AccessibleName = "Side" };
        form.Controls.Add(group);
        form.Controls.Add(panel);
        form.Show();

        var groupAo = group.AccessibilityObject;
        Assert.Same(groupAo, group.AccessibilityObject);
        Assert.Equal(AccessibleRole.Grouping, groupAo.Role);
        Assert.Equal("Options", groupAo.Name);
        Assert.Equal(4, groupAo.GetChildCount());
        Assert.Same(form.AccessibilityObject, groupAo.Parent);

        // A text box is named by the label before it, with that label's mnemonic.
        Assert.Equal(AccessibleRole.Text, box.AccessibilityObject.Role);
        Assert.Equal("Name:", box.AccessibilityObject.Name);
        Assert.Equal("Alt+N", box.AccessibilityObject.KeyboardShortcut);

        Assert.Equal(AccessibleRole.CheckButton, check.AccessibilityObject.Role);
        Assert.True((check.AccessibilityObject.State & AccessibleStates.Checked) != 0);
        Assert.Equal("Uncheck", check.AccessibilityObject.DefaultAction);

        int clicks = 0;
        ok.Click += (_, _) => clicks++;
        Assert.Equal(AccessibleRole.PushButton, ok.AccessibilityObject.Role);
        ok.AccessibilityObject.DoDefaultAction();
        Assert.Equal(1, clicks);

        Assert.Equal(AccessibleRole.Client, panel.AccessibilityObject.Role);
        Assert.Equal("Side", panel.AccessibilityObject.Name);
        panel.AccessibleRole = AccessibleRole.Pane;
        Assert.Equal(AccessibleRole.Pane, panel.AccessibilityObject.Role);

        box.Enabled = false;
        Assert.True((box.AccessibilityObject.State & AccessibleStates.Unavailable) != 0);
        Assert.Equal(group.RectangleToScreen(new Rectangle(ok.Location, ok.Size)), ok.AccessibilityObject.Bounds);

        string? help = null;
        ok.QueryAccessibilityHelp += (_, e) => e.HelpString = help = "Accepts the dialog";
        Assert.Equal("Accepts the dialog", ok.AccessibilityObject.Help);
        Assert.Equal("ControlAccessibleObject: Owner = " + ok, ok.AccessibilityObject.ToString());
    }

    private sealed class Gauge : Control
    {
        protected override AccessibleObject CreateAccessibilityInstance() => new GaugeAccessibleObject(this);

        private sealed class GaugeAccessibleObject(Gauge owner) : ControlAccessibleObject(owner)
        {
            public override AccessibleRole Role => AccessibleRole.ProgressBar;

            public override string? Value => "42%";
        }
    }

    [Fact]
    public void ACustomControlProvidesItsOwnAccessibleObject()
    {
        var gauge = new Gauge();
        Assert.Equal(AccessibleRole.ProgressBar, gauge.AccessibilityObject.Role);
        Assert.Equal("42%", gauge.AccessibilityObject.Value);
    }
}
