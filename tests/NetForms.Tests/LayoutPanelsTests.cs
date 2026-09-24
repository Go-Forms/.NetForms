using NetForms.Platform;
using Xunit;

namespace NetForms.Tests;

/// <summary>Ф3: FlowLayoutPanel, TableLayoutPanel, AutoScroll, AutoSize containers.</summary>
public class LayoutPanelsTests
{
    private static Control Box(int w, int h) => new Control { Size = new Size(w, h), Margin = new Padding(3) };

    [Fact]
    public void FlowLeftToRightWrapsAndHonoursMargins()
    {
        var flow = new FlowLayoutPanel { Size = new Size(100, 200) };
        var a = Box(40, 20);
        var b = Box(40, 30);
        var c = Box(40, 20);
        flow.Controls.AddRange(new[] { a, b, c });
        flow.PerformLayout();

        Assert.Equal(new Rectangle(3, 3, 40, 20), a.Bounds);
        Assert.Equal(new Rectangle(49, 3, 40, 30), b.Bounds);
        // c does not fit on the first row (3+46+46 = 95 + 46 > 100): next row starts below the tallest item.
        Assert.Equal(new Rectangle(3, 39, 40, 20), c.Bounds);
        Assert.Equal(new Size(92, 62), flow.GetPreferredSize(Size.Empty) - new Size(0, 0));
    }

    [Fact]
    public void FlowDirectionsAndFlowBreak()
    {
        var flow = new FlowLayoutPanel { Size = new Size(200, 100), FlowDirection = FlowDirection.TopDown };
        var a = Box(40, 20);
        var b = Box(40, 20);
        var c = Box(40, 20);
        flow.Controls.AddRange(new[] { a, b, c });
        flow.SetFlowBreak(a, true);
        flow.PerformLayout();
        Assert.Equal(new Point(3, 3), a.Location);
        Assert.Equal(new Point(49, 3), b.Location); // flow break: b starts a new column
        Assert.Equal(new Point(49, 29), c.Location);

        flow.FlowDirection = FlowDirection.RightToLeft;
        flow.SetFlowBreak(a, false);
        Assert.Equal(new Point(200 - 3 - 40, 3), a.Location);
        Assert.Equal(new Point(200 - 3 - 40 - 46, 3), b.Location);

        flow.FlowDirection = FlowDirection.LeftToRight;
        flow.WrapContents = false;
        flow.Width = 50;
        Assert.Equal(new Point(95, 3), c.Location); // no wrapping: keeps flowing past the edge
    }

    [Fact]
    public void FlowRowUsesAnchorsAcrossTheRow()
    {
        var flow = new FlowLayoutPanel { Size = new Size(300, 100) };
        var tall = Box(40, 50);
        var top = Box(40, 20);
        var bottom = Box(40, 20);
        bottom.Anchor = AnchorStyles.Bottom;
        var stretch = Box(40, 20);
        stretch.Anchor = AnchorStyles.Top | AnchorStyles.Bottom;
        var centred = Box(40, 20);
        centred.Anchor = AnchorStyles.None;
        flow.Controls.AddRange(new[] { tall, top, bottom, stretch, centred });
        flow.PerformLayout();
        Assert.Equal(3, top.Top);
        Assert.Equal(56 - 3 - 20, bottom.Top);
        Assert.Equal(new Rectangle(141, 3, 40, 50), stretch.Bounds);
        Assert.Equal(3 + (50 - 20) / 2, centred.Top);
    }

    [Fact]
    public void TableTracksAbsolutePercentAndAutoSize()
    {
        var table = new TableLayoutPanel { Size = new Size(300, 100), ColumnCount = 3, RowCount = 1 };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 75));
        var a = Box(10, 10);
        var b = Box(10, 10);
        var c = Box(10, 10);
        table.Controls.Add(a, 0, 0);
        table.Controls.Add(b, 1, 0);
        table.Controls.Add(c, 2, 0);
        table.PerformLayout();

        Assert.Equal(new[] { 50, 62, 188 }, table.GetColumnWidths());
        Assert.Equal(new Point(3, 3), a.Location);
        Assert.Equal(new Point(53, 3), b.Location);
        Assert.Equal(new Point(115, 3), c.Location);

        // An AutoSize column fits its widest child; the leftover goes to the last AutoSize column.
        table.ColumnStyles.Clear();
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        b.Width = 60;
        table.PerformLayout();
        Assert.Equal(new[] { 16, 66, 300 - 16 - 66 }, table.GetColumnWidths());
    }

    [Fact]
    public void TableCellsFlowSpanAndGrow()
    {
        var table = new TableLayoutPanel { Size = new Size(200, 200), ColumnCount = 2, RowCount = 2 };
        var a = Box(10, 10);
        var b = Box(10, 10);
        var c = Box(10, 10);
        var d = Box(10, 10);
        var e = Box(10, 10);
        table.Controls.AddRange(new[] { a, b, c, d });
        table.SetColumnSpan(c, 2);
        table.PerformLayout();

        Assert.Equal(new TableLayoutPanelCellPosition(0, 0), table.GetPositionFromControl(a));
        Assert.Equal(new TableLayoutPanelCellPosition(1, 0), table.GetPositionFromControl(b));
        Assert.Equal(new TableLayoutPanelCellPosition(0, 1), table.GetPositionFromControl(c));
        // d cannot fit in row 1 (c spans both columns): GrowStyle.AddRows adds a third row.
        Assert.Equal(new TableLayoutPanelCellPosition(0, 2), table.GetPositionFromControl(d));
        Assert.Equal(3, table.GetRowHeights().Length);
        Assert.Same(c, table.GetControlFromPosition(1, 1));

        table.Controls.Add(e, 1, 0); // explicit cell already taken by b: both share it (WinForms lets them overlap)
        Assert.Equal(new TableLayoutPanelCellPosition(1, 0), table.GetPositionFromControl(e));

        table.Controls.Remove(e);
        table.RowCount = 3;
        table.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
        Assert.Equal(new TableLayoutPanelCellPosition(0, 2), table.GetPositionFromControl(d));
        var last = Box(5, 5);
        table.Controls.Add(last); // the one free cell left
        Assert.Equal(new TableLayoutPanelCellPosition(1, 2), table.GetPositionFromControl(last));
        Assert.Throws<ArgumentException>(() => table.Controls.Add(Box(5, 5)));
    }

    [Fact]
    public void TableChildFollowsAnchorAndDockInsideItsCell()
    {
        var table = new TableLayoutPanel { Size = new Size(200, 100), ColumnCount = 2, RowCount = 1 };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        var fill = Box(10, 10);
        fill.Dock = DockStyle.Fill;
        var corner = Box(10, 10);
        corner.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        table.Controls.Add(fill, 0, 0);
        table.Controls.Add(corner, 1, 0);
        table.PerformLayout();
        Assert.Equal(new Rectangle(3, 3, 94, 94), fill.Bounds);
        Assert.Equal(new Rectangle(200 - 3 - 10, 100 - 3 - 10, 10, 10), corner.Bounds);

        var label = new Label { AutoSize = true, Text = "Hi", Font = Golden.Font, Anchor = AnchorStyles.None };
        table.Controls.Add(label, 1, 0);
        table.PerformLayout();
        var cell = new Rectangle(100, 0, 100, 100);
        Assert.Equal(cell.X + (cell.Width - label.Width) / 2, label.Left);
    }

    [Fact]
    public void AutoScrollShowsBarsAndMovesChildren()
    {
        var platform = TestPlatform.Install();
        using var form = new Form { ClientSize = new Size(300, 300) };
        var panel = new Panel { Bounds = new Rectangle(0, 0, 100, 100), AutoScroll = true };
        var far = new Control { Bounds = new Rectangle(10, 250, 50, 30) };
        var near = new Control { Bounds = new Rectangle(10, 10, 50, 30) };
        panel.Controls.Add(far);
        panel.Controls.Add(near);
        form.Controls.Add(panel);
        form.Show();

        Assert.True(panel.VerticalScroll.Visible);
        Assert.False(panel.HorizontalScroll.Visible);
        Assert.Equal(new Rectangle(0, 0, 100 - 17, 280), panel.DisplayRectangle);

        panel.AutoScrollPosition = new Point(0, 100);
        Assert.Equal(new Point(0, -100), panel.AutoScrollPosition);
        Assert.Equal(new Point(10, -90), near.Location);
        Assert.Equal(new Point(10, 150), far.Location);
        Assert.Equal(-100, panel.DisplayRectangle.Y);

        // The bar occupies the client's right edge: a click there scrolls, not the child under it.
        var window = platform.Windows.Last();
        var events = new List<ScrollEventType>();
        panel.Scroll += (_, e) => events.Add(e.Type);
        window.Click(new Point(100 - 8, 100 - 5)); // bottom arrow of the vertical bar
        Assert.Contains(ScrollEventType.SmallIncrement, events);
        Assert.True(panel.AutoScrollPosition.Y < -100);

        panel.ScrollControlIntoView(near);
        Assert.Equal(new Point(10, 0), near.Location); // scrolled just enough to reveal it

        panel.AutoScrollPosition = new Point(0, 500);
        Assert.Equal(280 - 100, -panel.AutoScrollPosition.Y); // clamped to the content
        window.Host.MouseWheel(new Point(50, 50), 120, InputModifiers.None);
        Assert.True(-panel.AutoScrollPosition.Y < 180);

        far.Top = 40 + panel.AutoScrollPosition.Y;
        panel.PerformLayout();
        Assert.False(panel.VerticalScroll.Visible);
        Assert.Equal(new Point(0, 0), panel.AutoScrollPosition);
    }

    [Fact]
    public void AutoScrollMinSizeForcesBarsAndPaintsThem()
    {
        var panel = new Panel { Size = new Size(120, 80), AutoScrollMinSize = new Size(300, 300), BackColor = Color.White };
        panel.PerformLayout();
        Assert.True(panel.AutoScroll);
        Assert.True(panel.VerticalScroll.Visible && panel.HorizontalScroll.Visible);
        using var bmp = new Bitmap(120, 80);
        panel.DrawToBitmap(bmp, new Rectangle(0, 0, 120, 80));
        Assert.Equal(Theme.ScrollTrack.ToArgb(), bmp.GetPixel(120 - 8, 40).ToArgb());
        Assert.Equal(Theme.ScrollTrack.ToArgb(), bmp.GetPixel(40, 80 - 8).ToArgb());
        Assert.Equal(Color.White.ToArgb(), bmp.GetPixel(20, 20).ToArgb());
    }

    [Fact]
    public void AutoSizeContainersGrowAroundChildren()
    {
        // Inside a parent: a container's AutoSize is applied from its parent's layout, as in WinForms
        // (a parentless panel keeps its size - CompatScenarios autosize/panel-*).
        var host = new Control { Size = new Size(300, 300) };
        var panel = new Panel { Size = new Size(50, 50), AutoSize = true, Padding = new Padding(5) };
        host.Controls.Add(panel);
        var child = new Control { Bounds = new Rectangle(10, 10, 100, 40), Margin = new Padding(3) };
        panel.Controls.Add(child);
        Assert.Equal(new Size(10 + 100 + 3 + 5, 10 + 40 + 3 + 5), panel.Size);

        child.Width = 20;
        // GrowOnly: never below the size user code set (50x50).
        Assert.Equal(new Size(50, 58), panel.Size);

        panel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Assert.Equal(new Size(10 + 20 + 3 + 5, 58), panel.Size);

        var form = new Form { ClientSize = new Size(100, 100), AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        form.Controls.Add(new Button { Bounds = new Rectangle(0, 0, 200, 30) });
        Assert.Equal(new Size(203, 33), form.ClientSize);
    }
}
