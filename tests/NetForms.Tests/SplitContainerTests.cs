using System.Drawing;
using NetForms.Platform;
using Xunit;

namespace NetForms.Tests;

/// <summary>
/// SplitContainer as dotnet/winforms lays it out (decision 180): the splitter keeps its ratio across resizes (a window
/// shrunk and grown back puts it back), a fixed panel keeps its size, BeginInit/EndInit hold back the minimum sizes,
/// the border is drawn by the panels, and a drag moves the panels when it is let go.
/// </summary>
public sealed class SplitContainerTests
{
    public SplitContainerTests() => TestPlatform.Install();

    [Fact]
    public void ShrunkAndGrownBackTheSplitterIsWhereItWas()
    {
        using var split = new SplitContainer { Size = new Size(300, 100), SplitterDistance = 100 };
        split.Width = 120;
        Assert.True(split.SplitterDistance < 100);
        split.Width = 300;
        Assert.Equal(100, split.SplitterDistance);
        Assert.Equal(100, split.Panel1.Width);
        Assert.Equal(new Rectangle(104, 0, 196, 100), split.Panel2.Bounds);

        // And in proportion when bigger.
        split.Width = 600;
        Assert.Equal(200, split.SplitterDistance);
    }

    [Fact]
    public void AFixedPanelKeepsItsSize()
    {
        using var one = new SplitContainer { Size = new Size(300, 100), SplitterDistance = 100, FixedPanel = FixedPanel.Panel1 };
        one.Width = 500;
        Assert.Equal(100, one.SplitterDistance);

        using var two = new SplitContainer { Size = new Size(300, 100), SplitterDistance = 100, FixedPanel = FixedPanel.Panel2 };
        two.Width = 500;
        Assert.Equal(196, two.Panel2.Width);
        Assert.Equal(300, two.SplitterDistance);
        two.Width = 300;
        Assert.Equal(100, two.SplitterDistance);
    }

    [Fact]
    public void BeginInitHoldsBackTheMinimumSizes()
    {
        // What designer code does: the minimum sizes come before the size (alphabetically), the distance after.
        using var split = new SplitContainer();
        split.BeginInit();
        split.Panel2MinSize = 200;
        split.SplitterWidth = 6;
        Assert.Equal(25, split.Panel2MinSize);
        split.Size = new Size(600, 300);
        split.SplitterDistance = 300;
        split.EndInit();
        Assert.Equal(200, split.Panel2MinSize);
        Assert.Equal(6, split.SplitterWidth);
        Assert.Equal(300, split.SplitterDistance);
        Assert.Equal(new Rectangle(306, 0, 294, 300), split.Panel2.Bounds);
    }

    [Fact]
    public void SettingTheDistanceRaisesSplitterMovedAndKeepsWithinTheMinimums()
    {
        using var split = new SplitContainer { Size = new Size(300, 100) };
        int moved = 0;
        split.SplitterMoved += (_, _) => moved++;
        split.SplitterDistance = 120;
        Assert.Equal(1, moved);
        split.SplitterDistance = 5;
        Assert.Equal(25, split.SplitterDistance);
        split.SplitterDistance = 299;
        Assert.Equal(300 - 25 - 4, split.SplitterDistance);
        Assert.Throws<ArgumentOutOfRangeException>(() => split.SplitterDistance = -1);
        // Never smaller than its panels' minimums and the splitter.
        split.Width = 10;
        Assert.Equal(25 + 4 + 25, split.Width);
    }

    [Fact]
    public void TheBorderIsDrawnByThePanels()
    {
        using var outer = new SplitContainer { Size = new Size(300, 100), BorderStyle = BorderStyle.FixedSingle };
        Assert.Equal(BorderStyle.FixedSingle, outer.Panel1.BorderStyle);
        Assert.Equal(BorderStyle.FixedSingle, outer.Panel2.BorderStyle);
        // A SplitContainer filling a panel with the same border takes it over: one line between them.
        var inner = new SplitContainer { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle };
        outer.Panel2.Controls.Add(inner);
        outer.PerformLayout();
        Assert.Equal(BorderStyle.None, outer.Panel2.BorderStyle);
        Assert.Equal(BorderStyle.FixedSingle, inner.Panel1.BorderStyle);
    }

    [Fact]
    public void CollapsingAPanelGivesTheOtherAllTheRoom()
    {
        using var split = new SplitContainer { Size = new Size(300, 100), SplitterDistance = 100 };
        split.Panel1Collapsed = true;
        Assert.Equal(new Rectangle(0, 0, 300, 100), split.Panel2.Bounds);
        split.Panel2Collapsed = true;
        Assert.False(split.Panel1Collapsed);
        Assert.Equal(new Rectangle(0, 0, 300, 100), split.Panel1.Bounds);
        split.Panel2Collapsed = false;
        Assert.Equal(100, split.Panel1.Width);
    }

    [Fact]
    public void ADragMovesThePanelsWhenLetGoAndAMovingHandlerCanStopIt()
    {
        var split = new SplitContainer { Bounds = new Rectangle(0, 0, 300, 100), SplitterDistance = 100 };
        var platform = TestPlatform.Install();
        var form = new Form { ClientSize = new Size(400, 300) };
        form.Controls.Add(split);
        form.Show();
        var window = platform.Windows.Last();
        using (form)
        {
            int moving = 0;
            split.SplitterMoving += (_, _) => moving++;
            window.Host.MouseDown(MouseButton.Left, new Point(102, 50), 1, InputModifiers.None);
            window.Host.MouseMove(new Point(152, 50), MouseButton.Left, InputModifiers.None);
            // While dragged, a bar shows where it goes; the panels have not moved yet.
            Assert.Equal(100, split.SplitterDistance);
            Assert.Equal(100, split.Panel1.Width);
            Assert.True(moving > 0);
            window.Host.MouseUp(MouseButton.Left, new Point(152, 50), InputModifiers.None);
            Assert.Equal(150, split.SplitterDistance);
            Assert.Equal(150, split.Panel1.Width);

            // Cancelled from SplitterMoving: it stays.
            split.SplitterMoving += (_, e) => e.Cancel = true;
            window.Host.MouseDown(MouseButton.Left, new Point(152, 50), 1, InputModifiers.None);
            window.Host.MouseMove(new Point(200, 50), MouseButton.Left, InputModifiers.None);
            window.Host.MouseUp(MouseButton.Left, new Point(200, 50), InputModifiers.None);
            Assert.Equal(150, split.SplitterDistance);
        }
    }
}
