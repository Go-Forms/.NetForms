using Xunit;

namespace NetForms.Tests;

/// <summary>
/// The docking matrix from ../GoForms/GoForms/golden_dock_test.go, case for case. GoForms
/// lists children in docking order; WinForms docks from the bottom of the z-order up, so
/// the children are added in reverse (which is exactly what the designer emits).
/// </summary>
public class DockLayoutGoldenTests
{
    private sealed record Child(string Name, DockStyle Dock, int X, int Y, int W, int H, int OutX, int OutY, int OutW, int OutH);

    private static void Run(int clientW, int clientH, params Child[] children)
    {
        var parent = new Control { Size = new Size(clientW, clientH) };
        var controls = new Dictionary<string, Control>();
        foreach (var ch in children)
        {
            controls[ch.Name] = new Control { Name = ch.Name, Bounds = new Rectangle(ch.X, ch.Y, ch.W, ch.H), Dock = ch.Dock };
        }

        parent.SuspendLayout();
        // Reverse: the first GoForms child must dock first, i.e. sit at the bottom of the z-order.
        foreach (var ch in children.Reverse())
        {
            parent.Controls.Add(controls[ch.Name]);
        }
        parent.ResumeLayout(false);
        parent.PerformLayout();

        foreach (var ch in children)
        {
            var b = controls[ch.Name].Bounds;
            Assert.True(b == new Rectangle(ch.OutX, ch.OutY, ch.OutW, ch.OutH),
                $"{ch.Name}: expected ({ch.OutX},{ch.OutY},{ch.OutW},{ch.OutH}) got ({b.X},{b.Y},{b.Width},{b.Height})");
        }
    }

    [Fact]
    public void NoDockingLeavesEverythingAlone() => Run(400, 300,
        new Child("a", DockStyle.None, 10, 20, 90, 30, 10, 20, 90, 30),
        new Child("b", DockStyle.None, 200, 100, 120, 40, 200, 100, 120, 40));

    [Fact]
    public void DockLeftTakesTheFullHeight() => Run(523, 200,
        new Child("tabs", DockStyle.Left, 11, 7, 500, 190, 0, 0, 500, 200));

    [Fact]
    public void DockTopThenBottomThenFill() => Run(600, 400,
        new Child("toolbar", DockStyle.Top, 0, 0, 100, 34, 0, 0, 600, 34),
        new Child("status", DockStyle.Bottom, 0, 0, 100, 24, 0, 376, 600, 24),
        new Child("body", DockStyle.Fill, 5, 5, 10, 10, 0, 34, 600, 342));

    [Fact]
    public void LeftAndRightCarveTheSidesBeforeFill() => Run(600, 400,
        new Child("nav", DockStyle.Left, 0, 0, 150, 999, 0, 0, 150, 400),
        new Child("aside", DockStyle.Right, 0, 0, 100, 999, 500, 0, 100, 400),
        new Child("body", DockStyle.Fill, 0, 0, 10, 10, 150, 0, 350, 400));

    [Fact]
    public void DockingOrderDecidesWhoGetsTheCorner() => Run(500, 300,
        new Child("side", DockStyle.Left, 0, 0, 120, 0, 0, 0, 120, 300),
        new Child("head", DockStyle.Top, 0, 0, 0, 40, 120, 0, 380, 40));

    /// <summary>
    /// GoForms let only the first Fill child win and left the second at its own bounds.
    /// WinForms gives every Fill child the remaining area, and WinForms is the reference:
    /// a deliberate divergence from the Go fixture.
    /// </summary>
    [Fact]
    public void EveryFillChildGetsTheRemainingArea() => Run(400, 200,
        new Child("first", DockStyle.Fill, 0, 0, 10, 10, 0, 0, 400, 200),
        new Child("second", DockStyle.Fill, 3, 4, 20, 20, 0, 0, 400, 200));

    [Fact]
    public void DockedAndFloatingSiblingsTogether() => Run(400, 300,
        new Child("top", DockStyle.Top, 0, 0, 0, 50, 0, 0, 400, 50),
        new Child("loose", DockStyle.None, 20, 20, 80, 25, 20, 20, 80, 25));

    [Fact]
    public void ADockBiggerThanTheClientClampsToNothingLeft() => Run(200, 100,
        new Child("huge", DockStyle.Left, 0, 0, 300, 0, 0, 0, 300, 100),
        new Child("body", DockStyle.Fill, 0, 0, 10, 10, 300, 0, 0, 100));

    [Fact]
    public void ReverseZOrderIsTheDockingOrder()
    {
        // Same two controls as the corner case, with the Top-docked one added last: it is
        // now at the bottom of the z-order, docks first and gets the corner.
        var parent = new Control { Size = new Size(500, 300) };
        var side = new Control { Size = new Size(120, 0), Dock = DockStyle.Left };
        var head = new Control { Size = new Size(0, 40), Dock = DockStyle.Top };
        parent.Controls.Add(side);
        parent.Controls.Add(head);
        parent.PerformLayout();

        Assert.Equal(new Rectangle(0, 40, 120, 260), side.Bounds);
        Assert.Equal(new Rectangle(0, 0, 500, 40), head.Bounds);
    }

    [Fact]
    public void InvisibleDockedChildrenTakeNoSpace()
    {
        var parent = new Control { Size = new Size(600, 400) };
        var top = new Control { Height = 34, Dock = DockStyle.Top, Visible = false };
        var body = new Control { Dock = DockStyle.Fill };
        parent.Controls.Add(body);
        parent.Controls.Add(top);
        parent.PerformLayout();

        Assert.Equal(new Rectangle(0, 0, 600, 400), body.Bounds);

        top.Visible = true;
        Assert.Equal(new Rectangle(0, 34, 600, 366), body.Bounds);
    }
}
