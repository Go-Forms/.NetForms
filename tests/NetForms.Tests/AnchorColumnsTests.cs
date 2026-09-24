using Xunit;

namespace NetForms.Tests;

/// <summary>Port of ../GoForms/GoForms/anchor_columns_test.go.</summary>
public class AnchorColumnsTests
{
    private static Control Column(int x, int y, int w, int h, AnchorStyles a) =>
        new Control { Bounds = new Rectangle(x, y, w, h), Anchor = a };

    private static Control Parent(int w, int h, params Control[] children)
    {
        var parent = new Control { Size = new Size(w, h) };
        parent.SuspendLayout();
        foreach (var c in children) parent.Controls.Add(c);
        parent.ResumeLayout(false);
        parent.PerformLayout();
        return parent;
    }

    /// <summary>
    /// Documents the trap, not a defect: Left|Right keeps both margins fixed, so two
    /// side-by-side controls that both do this grow through each other - in WinForms
    /// exactly as here. The fix is the anchors, not the engine.
    /// </summary>
    [Fact]
    public void AnchoringBothColumnsLeftAndRightOverlaps()
    {
        var left = Column(12, 40, 520, 260, AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right);
        var right = Column(544, 40, 520, 260, AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right);
        var parent = Parent(1076, 800, left, right);

        parent.Size = new Size(1376, 800);

        var lb = left.Bounds;
        var rb = right.Bounds;
        Assert.True(lb.Right > rb.X, $"expected the documented overlap: left ends at {lb.Right}, right starts at {rb.X}");
        // Each keeps its own right margin, which is what WinForms guarantees.
        Assert.Equal(12, 1376 - rb.Right);
        Assert.Equal(544, 1376 - lb.Right);
    }

    /// <summary>The arrangement the showcase uses: the left column keeps its width, the right one takes the space that opens up.</summary>
    [Fact]
    public void FixedLeftStretchingRightColumnsNeverOverlap()
    {
        var left = Column(12, 40, 520, 260, AnchorStyles.Top | AnchorStyles.Left);
        var right = Column(544, 40, 520, 260, AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right);
        var parent = Parent(1076, 800, left, right);

        foreach (var size in new[] { new Size(1076, 800), new Size(1376, 900), new Size(1920, 1080), new Size(900, 700) })
        {
            parent.Size = size;
            var lb = left.Bounds;
            var rb = right.Bounds;
            Assert.True(lb.Right <= rb.X, $"at {size} the columns overlap: left ends at {lb.Right}, right starts at {rb.X}");
            Assert.Equal(520, lb.Width);
        }

        parent.Size = new Size(1376, 800);
        Assert.True(right.Width > 520, $"the stretching column stayed at {right.Width}");
        Assert.Equal(1376 - 12 - 544, right.Width);
    }

    /// <summary>A docked strip takes its slab first; an anchored control above it never walks over it.</summary>
    [Fact]
    public void DockedStripKeepsAnchoredControlsOffIt()
    {
        var strip = new Control { Bounds = new Rectangle(12, 544, 1052, 200), Dock = DockStyle.Bottom };
        var box = Column(12, 40, 520, 260, AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom);
        var parent = Parent(1076, 760, box, strip);

        parent.Size = new Size(1076, 1000);

        var sb = strip.Bounds;
        var bb = box.Bounds;
        Assert.True(bb.Bottom <= sb.Y, $"anchored box reaches {bb.Bottom}, over the docked strip that starts at {sb.Y}");
        Assert.Equal(200, sb.Height);
        Assert.Equal(new Rectangle(0, 800, 1076, 200), sb);
        Assert.Equal(new Rectangle(12, 40, 520, 500), bb);
    }

    [Fact]
    public void BottomRightAnchorSlidesWithTheParent()
    {
        var button = Column(197, 105, 75, 23, AnchorStyles.Bottom | AnchorStyles.Right);
        var parent = Parent(284, 140, button);

        parent.Size = new Size(484, 240);
        Assert.Equal(new Rectangle(397, 205, 75, 23), button.Bounds);

        parent.Size = new Size(284, 140);
        Assert.Equal(new Rectangle(197, 105, 75, 23), button.Bounds);
    }

    [Fact]
    public void NoAnchorMovesByHalfTheDelta()
    {
        // WinForms centres an unanchored control with integer arithmetic.
        var c = Column(100, 50, 40, 20, AnchorStyles.None);
        var parent = Parent(300, 200, c);

        parent.Size = new Size(400, 300);
        Assert.Equal(new Rectangle(150, 100, 40, 20), c.Bounds);
    }

    [Fact]
    public void ResumeLayoutFalseRecapturesAnchorsAgainstTheFinalClientSize()
    {
        // Designer order: bounds set, controls added, ClientSize set, ResumeLayout(false), PerformLayout().
        var parent = new Control();
        parent.SuspendLayout();
        var button = Column(197, 105, 75, 23, AnchorStyles.Bottom | AnchorStyles.Right);
        parent.Controls.Add(button);
        parent.ClientSize = new Size(284, 140);
        parent.ResumeLayout(false);
        parent.PerformLayout();

        Assert.Equal(new Point(197, 105), button.Location);
        parent.ClientSize = new Size(384, 240);
        Assert.Equal(new Point(297, 205), button.Location);
    }
}
