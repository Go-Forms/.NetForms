using System.Drawing;
using NetForms.Platform;
using Xunit;

namespace NetForms.Tests;

/// <summary>
/// ToolStripContainer, ToolStripPanel and its rows (decision 178): strips join the row their location falls in,
/// the panels size themselves to their rows, the content panel fills the rest, and a strip dragged by its grip
/// moves along its row, to another row or to another panel of the container.
/// </summary>
public sealed class ToolStripContainerTests
{
    private static (Form Form, TestWindow Window) Show(Control control)
    {
        var platform = TestPlatform.Install();
        var form = new Form { ClientSize = new Size(400, 300) };
        form.Controls.Add(control);
        form.Show();
        return (form, platform.Windows.Last());
    }

    private static ToolStrip Strip(int buttons = 2)
    {
        var strip = new ToolStrip { Dock = DockStyle.None };
        for (int i = 0; i < buttons; i++) strip.Items.Add(new ToolStripButton("B" + i));
        return strip;
    }

    [Fact]
    public void AnEmptyContainerIsAllContent()
    {
        TestPlatform.Install();
        using var container = new ToolStripContainer();
        Assert.Equal(new Size(150, 175), container.Size);
        Assert.Equal(new Rectangle(0, 0, 150, 175), container.ContentPanel.Bounds);
        Assert.Equal(DockStyle.Top, container.TopToolStripPanel.Dock);
        Assert.Equal(DockStyle.Left, container.LeftToolStripPanel.Dock);
        Assert.Equal(Orientation.Vertical, container.LeftToolStripPanel.Orientation);
        Assert.Equal(Orientation.Horizontal, container.BottomToolStripPanel.Orientation);
        Assert.Equal(0, container.TopToolStripPanel.Height);
        Assert.Empty(container.TopToolStripPanel.Rows);
        Assert.Equal(new Padding(3, 0, 0, 0), container.TopToolStripPanel.RowMargin);
        Assert.Throws<NotSupportedException>(() => container.Controls.Add(new Button()));
    }

    [Fact]
    public void AStripInTheTopPanelPushesTheContentDown()
    {
        TestPlatform.Install();
        using var container = new ToolStripContainer { Size = new Size(300, 200) };
        var strip = Strip();
        container.TopToolStripPanel.Controls.Add(strip);
        Assert.Same(container.TopToolStripPanel, strip.Parent);
        Assert.Equal(new Point(3, 0), strip.Location);
        Assert.Equal(strip.Height, container.TopToolStripPanel.Height);
        Assert.Equal(new Rectangle(0, strip.Height, 300, 200 - strip.Height), container.ContentPanel.Bounds);
        var row = Assert.Single(container.TopToolStripPanel.Rows);
        Assert.Equal(new[] { (Control)strip }, row.Controls);

        // A second one goes into the same row, after the first.
        var second = Strip();
        container.TopToolStripPanel.Controls.Add(second);
        Assert.Single(container.TopToolStripPanel.Rows);
        Assert.Equal(new Point(strip.Right, 0), second.Location);

        // Removing them collapses the panel again.
        container.TopToolStripPanel.Controls.Remove(strip);
        container.TopToolStripPanel.Controls.Remove(second);
        Assert.Equal(0, container.TopToolStripPanel.Height);
        Assert.Empty(container.TopToolStripPanel.Rows);
        Assert.Equal(new Rectangle(0, 0, 300, 200), container.ContentPanel.Bounds);
    }

    [Fact]
    public void DesignerCodeJoinsTheStripsByTheirLocations()
    {
        TestPlatform.Install();
        using var container = new ToolStripContainer { Size = new Size(300, 200) };
        var (a, b, c) = (Strip(), Strip(), Strip());
        var top = container.TopToolStripPanel;
        // What InitializeComponent does: everything added while the layout is suspended, the locations after.
        top.SuspendLayout();
        container.SuspendLayout();
        top.Controls.Add(c);
        top.Controls.Add(b);
        top.Controls.Add(a);
        a.Location = new Point(3, 0);
        b.Location = new Point(3, 25);
        c.Location = new Point(120, 0);
        top.ResumeLayout(false);
        top.PerformLayout();
        container.ResumeLayout(false);
        container.PerformLayout();

        Assert.Equal(2, top.Rows.Length);
        Assert.Equal(new Control[] { a, c }, top.Rows[0].Controls.OrderBy(x => x.Left));
        Assert.Equal(new Control[] { b }, top.Rows[1].Controls);
        Assert.Equal(new Point(3, 0), a.Location);
        Assert.Equal(new Point(120, 0), c.Location);
        Assert.Equal(new Point(3, 25), b.Location);
        Assert.Equal(50, top.Height);
        Assert.Equal(50, container.ContentPanel.Top);
    }

    [Fact]
    public void SettingTheLocationOfAStripMovesItToTheRowThere()
    {
        TestPlatform.Install();
        using var container = new ToolStripContainer { Size = new Size(300, 200) };
        var top = container.TopToolStripPanel;
        var (a, b) = (Strip(), Strip());
        top.Controls.Add(a);
        top.Controls.Add(b);
        Assert.Single(top.Rows);
        b.SetBounds(3, 28, b.Width, b.Height);
        Assert.Equal(2, top.Rows.Length);
        Assert.Equal(new Point(3, 25), b.Location);
        b.Location = new Point(150, 3);
        Assert.Single(top.Rows);
        Assert.Equal(new Point(150, 0), b.Location);
    }

    [Fact]
    public void ASidePanelStandsItsStripsUp()
    {
        TestPlatform.Install();
        using var container = new ToolStripContainer { Size = new Size(300, 200) };
        var strip = Strip();
        container.LeftToolStripPanel.Controls.Add(strip);
        Assert.Equal(Orientation.Vertical, strip.Orientation);
        Assert.True(strip.Height > strip.Width);
        Assert.Equal(new Point(0, 3), strip.Location);
        Assert.Equal(strip.Width, container.LeftToolStripPanel.Width);
        Assert.Equal(strip.Width, container.ContentPanel.Left);
    }

    [Fact]
    public void JoinPutsAStripIntoARowOrANewOne()
    {
        TestPlatform.Install();
        using var container = new ToolStripContainer { Size = new Size(400, 200) };
        var top = container.TopToolStripPanel;
        var (a, b, c) = (Strip(), Strip(), Strip());
        top.Join(a);
        top.Join(b, 1);
        Assert.Equal(2, top.Rows.Length);
        top.Join(c, 0);
        Assert.Equal(2, top.Rows.Length);
        Assert.Contains(c, top.Rows[0].Controls);
        Assert.Same(top.Rows[1], top.PointToRow(new Point(10, b.Top + 2)));
        Assert.Null(top.PointToRow(new Point(10, top.Height + 5)));
        Assert.True(top.Rows[0].CanMove(Strip()));
        top.Locked = true;
        Assert.False(top.Rows[0].CanMove(Strip()));
    }

    [Fact]
    public void AMenuStripStretchesOverItsRow()
    {
        TestPlatform.Install();
        using var container = new ToolStripContainer { Size = new Size(300, 200) };
        var menu = new MenuStrip { Dock = DockStyle.None };
        menu.Items.Add(new ToolStripMenuItem("File"));
        container.TopToolStripPanel.Controls.Add(menu);
        Assert.True(menu.Stretch);
        Assert.Equal(300 - 3, menu.Width);
    }

    [Fact]
    public void AStripDraggedByItsGripMovesAlongItsRowAndToAnotherPanel()
    {
        var container = new ToolStripContainer { Dock = DockStyle.Fill };
        var strip = Strip();
        container.TopToolStripPanel.Controls.Add(strip);
        var (form, window) = Show(container);
        using (form)
        {
            var grip = strip.GripRectangle;
            var start = new Point(strip.Left + grip.X + grip.Width / 2, strip.Top + grip.Y + grip.Height / 2);
            window.Host.MouseDown(MouseButton.Left, start, 1, InputModifiers.None);
            window.Host.MouseMove(new Point(start.X + 60, start.Y), MouseButton.Left, InputModifiers.None);
            Assert.Equal(63, strip.Left);
            Assert.Same(container.TopToolStripPanel, strip.Parent);

            // Down to the left edge: into the left panel, standing up.
            window.Host.MouseMove(new Point(4, 150), MouseButton.Left, InputModifiers.None);
            Assert.Same(container.LeftToolStripPanel, strip.Parent);
            Assert.Equal(Orientation.Vertical, strip.Orientation);
            Assert.Equal(0, container.TopToolStripPanel.Height);
            Assert.Equal(strip.Width, container.ContentPanel.Left);

            // And it still follows the mouse there: where it was taken stays under the pointer.
            window.Host.MouseMove(new Point(4, 180), MouseButton.Left, InputModifiers.None);
            Assert.Equal(180 - start.Y, container.LeftToolStripPanel.Top + strip.Top);
            window.Host.MouseUp(MouseButton.Left, new Point(4, 180), InputModifiers.None);

            // Let go, it stays.
            window.Host.MouseMove(new Point(4, 250), MouseButton.None, InputModifiers.None);
            Assert.Equal(180 - start.Y, container.LeftToolStripPanel.Top + strip.Top);
        }
    }

    [Fact]
    public void ALockedPanelKeepsItsStrips()
    {
        var container = new ToolStripContainer { Dock = DockStyle.Fill };
        var strip = Strip();
        container.TopToolStripPanel.Controls.Add(strip);
        container.TopToolStripPanel.Locked = true;
        var (form, window) = Show(container);
        using (form)
        {
            var grip = strip.GripRectangle;
            var start = new Point(strip.Left + grip.X + 1, strip.Top + grip.Y + grip.Height / 2);
            window.Host.MouseDown(MouseButton.Left, start, 1, InputModifiers.None);
            window.Host.MouseMove(new Point(start.X + 60, start.Y), MouseButton.Left, InputModifiers.None);
            window.Host.MouseUp(MouseButton.Left, new Point(start.X + 60, start.Y), InputModifiers.None);
            Assert.Equal(3, strip.Left);
        }
    }
}
