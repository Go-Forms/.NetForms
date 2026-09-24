using NetForms.Platform;
using Xunit;

namespace NetForms.Tests;

/// <summary>
/// MDI: a child form is a control inside the parent's client area, with its own caption, border
/// and client coordinate system. One platform window serves the parent and every child.
/// </summary>
public class MdiTests
{
    private static (TestPlatform platform, Form parent, TestWindow window) ShowParent(int width = 600, int height = 400)
    {
        var platform = TestPlatform.Install();
        var parent = new Form { ClientSize = new Size(width, height), IsMdiContainer = true };
        parent.Show();
        return (platform, parent, platform.Windows.Last());
    }

    private static Form AddChild(Form parent, string text, Rectangle? bounds = null)
    {
        var child = new Form { MdiParent = parent, Text = text };
        if (bounds.HasValue) child.Bounds = bounds.Value;
        child.Show();
        return child;
    }

    // --- structure ------------------------------------------------------------------------

    [Fact]
    public void AChildIsAControlOfTheParentAndNotAWindowOfItsOwn()
    {
        var (platform, parent, _) = ShowParent();
        using (parent)
        {
            Assert.True(parent.IsMdiContainer);
            Assert.NotNull(parent.MdiClientArea);
            Assert.Equal(DockStyle.Fill, parent.MdiClientArea!.Dock);

            int windowsBefore = platform.Windows.Count;
            var child = AddChild(parent, "Doc 1");

            // No second platform window: the child lives inside the parent's.
            Assert.Equal(windowsBefore, platform.Windows.Count);
            Assert.True(child.IsMdiChild);
            Assert.Same(parent, child.MdiParent);
            Assert.Contains(child, parent.MdiClientArea.Controls);
            Assert.Same(parent, child.TopLevelForm);
            Assert.Same(child, child.FindForm());

            Assert.Single(parent.MdiChildren);
            Assert.Same(child, parent.ActiveMdiChild);
        }
    }

    [Fact]
    public void SettingMdiParentRequiresAContainer()
    {
        TestPlatform.Install();
        using var plain = new Form();
        using var child = new Form();
        Assert.Throws<ArgumentException>(() => child.MdiParent = plain);

        plain.IsMdiContainer = true;
        child.MdiParent = plain;
        Assert.True(child.IsMdiChild);

        // Detaching puts it back on its own.
        child.MdiParent = null;
        Assert.False(child.IsMdiChild);
        Assert.Empty(plain.MdiChildren);
    }

    [Fact]
    public void ActivatingAChildBringsItToTheFront()
    {
        var (_, parent, _) = ShowParent();
        using (parent)
        {
            var first = AddChild(parent, "First");
            var second = AddChild(parent, "Second");

            // The newest child is active and front-most (index 0 is the front of the z-order).
            Assert.Same(second, parent.ActiveMdiChild);
            Assert.Equal(0, parent.MdiClientArea!.Controls.GetChildIndex(second));

            int activations = 0;
            parent.MdiChildActivate += (_, _) => activations++;
            first.Activate();

            Assert.Same(first, parent.ActiveMdiChild);
            Assert.Equal(0, parent.MdiClientArea.Controls.GetChildIndex(first));
            Assert.Equal(1, activations);
        }
    }

    // --- the child's coordinate system ---------------------------------------------------------

    [Fact]
    public void TheChildsClientAreaIsInsetByItsBorderAndCaption()
    {
        var (_, parent, _) = ShowParent();
        using (parent)
        {
            var child = AddChild(parent, "Doc", new Rectangle(20, 30, 300, 200));
            var button = new Button { Bounds = new Rectangle(10, 10, 80, 24) };
            child.Controls.Add(button);

            // The client area is smaller than the child's bounds by the frame.
            Assert.True(child.ClientSize.Width < child.Width);
            Assert.True(child.ClientSize.Height < child.Height - Form.MdiChildCaptionHeight);

            // A control on the child is positioned in the child's client coordinates, and its
            // position in the window is the sum of the child's bounds, the frame and its own.
            int frameX = child.Width - child.ClientSize.Width;
            int frameY = child.Height - child.ClientSize.Height;
            var inWindow = button.PointToScreen(Point.Empty);
            var childOrigin = child.PointToScreen(Point.Empty);
            Assert.Equal(childOrigin.X + 10, inWindow.X);
            Assert.Equal(childOrigin.Y + 10, inWindow.Y);
            Assert.True(frameY > frameX, "the caption makes the vertical frame taller than the horizontal one");
        }
    }

    [Fact]
    public void MouseInputReachesAControlOnAChildThroughTheFrame()
    {
        var (_, parent, window) = ShowParent();
        using (parent)
        {
            var child = AddChild(parent, "Doc", new Rectangle(40, 50, 300, 200));
            int clicks = 0;
            var button = new Button { Bounds = new Rectangle(10, 10, 80, 24) };
            button.Click += (_, _) => clicks++;
            child.Controls.Add(button);

            // Click the middle of the button, in window coordinates.
            var origin = button.PointToScreen(Point.Empty);
            window.Click(new Point(origin.X + 40, origin.Y + 12));

            Assert.Equal(1, clicks);
            Assert.Same(child, parent.ActiveMdiChild);
        }
    }

    [Fact]
    public void DraggingTheCaptionMovesTheChild()
    {
        var (_, parent, window) = ShowParent();
        using (parent)
        {
            var child = AddChild(parent, "Doc", new Rectangle(40, 50, 300, 200));
            var client = parent.MdiClientArea!;
            // A point on the child's caption, in window coordinates.
            var caption = new Point(client.Left + child.Left + 60, client.Top + child.Top + 10);

            window.Host.MouseDown(MouseButton.Left, caption, 1, InputModifiers.None);
            window.Host.MouseMove(new Point(caption.X + 35, caption.Y + 25), MouseButton.Left, InputModifiers.None);
            window.Host.MouseUp(MouseButton.Left, new Point(caption.X + 35, caption.Y + 25), InputModifiers.None);

            Assert.Equal(75, child.Left);
            Assert.Equal(75, child.Top);
            Assert.Equal(new Size(300, 200), child.Size);
        }
    }

    [Fact]
    public void TheCloseButtonOnTheCaptionClosesTheChild()
    {
        var (_, parent, window) = ShowParent();
        using (parent)
        {
            var child = AddChild(parent, "Doc", new Rectangle(40, 50, 300, 200));
            bool closed = false;
            child.FormClosed += (_, _) => closed = true;

            var client = parent.MdiClientArea!;
            // The close button sits at the right end of the caption.
            var close = new Point(client.Left + child.Right - 12, client.Top + child.Top + 12);
            window.Host.MouseDown(MouseButton.Left, close, 1, InputModifiers.None);
            window.Host.MouseUp(MouseButton.Left, close, InputModifiers.None);

            Assert.True(closed);
            Assert.Empty(parent.MdiChildren);
            Assert.Null(parent.ActiveMdiChild);
        }
    }

    // --- LayoutMdi ---------------------------------------------------------------------------------

    [Fact]
    public void TileHorizontalSplitsTheClientAreaIntoRows()
    {
        var (_, parent, _) = ShowParent();
        using (parent)
        {
            var a = AddChild(parent, "A");
            var b = AddChild(parent, "B");
            parent.LayoutMdi(MdiLayout.TileHorizontal);

            var area = parent.MdiClientArea!.ClientRectangle;
            Assert.Equal(area.Width, a.Width);
            Assert.Equal(area.Width, b.Width);
            Assert.Equal(0, a.Top);
            Assert.Equal(a.Bottom, b.Top);
            Assert.Equal(area.Height, a.Height + b.Height);
        }
    }

    [Fact]
    public void TileVerticalSplitsTheClientAreaIntoColumns()
    {
        var (_, parent, _) = ShowParent();
        using (parent)
        {
            var a = AddChild(parent, "A");
            var b = AddChild(parent, "B");
            parent.LayoutMdi(MdiLayout.TileVertical);

            var area = parent.MdiClientArea!.ClientRectangle;
            Assert.Equal(area.Height, a.Height);
            Assert.Equal(0, a.Left);
            Assert.Equal(a.Right, b.Left);
            Assert.Equal(area.Width, a.Width + b.Width);
        }
    }

    [Fact]
    public void CascadeStepsTheChildrenDownAndRight()
    {
        var (_, parent, _) = ShowParent();
        using (parent)
        {
            var a = AddChild(parent, "A");
            var b = AddChild(parent, "B");
            var c = AddChild(parent, "C");
            parent.LayoutMdi(MdiLayout.Cascade);

            // Arranged back to front, so the front-most (newest, active) child ends up
            // furthest down and right - the way Windows cascades.
            Assert.True(c.Left > b.Left && b.Left > a.Left);
            Assert.True(c.Top > b.Top && b.Top > a.Top);
            Assert.Equal(a.Size, b.Size);
            Assert.Same(c, parent.ActiveMdiChild);
        }
    }

    [Fact]
    public void AMaximisedChildFillsTheClientAreaAndFollowsItsResize()
    {
        var (_, parent, window) = ShowParent();
        using (parent)
        {
            var child = AddChild(parent, "Doc", new Rectangle(40, 50, 300, 200));
            var client = parent.MdiClientArea!;

            // Double-clicking the caption maximises.
            var caption = new Point(client.Left + child.Left + 60, client.Top + child.Top + 10);
            window.Host.MouseDown(MouseButton.Left, caption, 2, InputModifiers.None);
            window.Host.MouseUp(MouseButton.Left, caption, InputModifiers.None);

            Assert.Equal(FormWindowState.Maximized, child.WindowState);
            Assert.Equal(client.ClientRectangle, child.Bounds);

            // The parent's window grows: the maximised child grows with it.
            window.Resize(800, 600);
            Assert.Equal(client.ClientRectangle, child.Bounds);
            Assert.Equal(client.Width, child.Width);
        }
    }

    // --- independent top-level windows (the non-MDI half of "multi-window") -------------------------------

    [Fact]
    public void SeparateTopLevelFormsEachGetTheirOwnWindow()
    {
        var platform = TestPlatform.Install();
        int before = platform.Windows.Count;

        using var first = new Form { Text = "First", ClientSize = new Size(300, 200) };
        using var second = new Form { Text = "Second", ClientSize = new Size(320, 240) };
        first.Show();
        second.Show();

        // Two forms, two platform windows - side by side, not nested.
        Assert.Equal(before + 2, platform.Windows.Count);
        Assert.Contains(first, Application.OpenForms.Cast<Form>());
        Assert.Contains(second, Application.OpenForms.Cast<Form>());
        Assert.Null(first.MdiParent);
        Assert.False(second.IsMdiChild);
        Assert.Same(first, first.TopLevelForm);
        Assert.Same(second, second.TopLevelForm);

        // Closing one leaves the other running.
        second.Close();
        Assert.DoesNotContain(second, Application.OpenForms.Cast<Form>());
        Assert.Contains(first, Application.OpenForms.Cast<Form>());
    }

    [Fact]
    public void TheSampleOpensAnIndependentWindowToo()
    {
        var platform = TestPlatform.Install();
        using var main = new global::MdiDemo.MainForm();
        main.Show();
        int windows = platform.Windows.Count;
        int children = main.MdiChildren.Length;

        // Inside the parent: no new OS window, one more MDI child.
        main.NewDocument();
        Assert.Equal(windows, platform.Windows.Count);
        Assert.Equal(children + 1, main.MdiChildren.Length);

        // Separate: a new OS window, and the MDI children are untouched.
        var separate = main.NewSeparateWindow();
        Assert.Equal(windows + 1, platform.Windows.Count);
        Assert.Equal(children + 1, main.MdiChildren.Length);
        Assert.False(separate.IsMdiChild);
        separate.Close();
    }

    // --- the sample -------------------------------------------------------------------------------------

    [Fact]
    public void TheMdiSampleOpensDocumentsInsideOneWindow()
    {
        var platform = TestPlatform.Install();
        int windowsBefore = platform.Windows.Count;
        using var main = new global::MdiDemo.MainForm();
        main.Show();
        var window = platform.Windows.Last();

        // The sample opens two documents in its constructor, and they cost no extra window.
        Assert.Equal(windowsBefore + 1, platform.Windows.Count);
        Assert.Equal(2, main.MdiChildren.Length);
        Assert.NotNull(main.ActiveMdiChild);
        Assert.True(main.IsMdiContainer);

        var third = main.NewDocument();
        Assert.Equal(3, main.MdiChildren.Length);
        Assert.Same(third, main.ActiveMdiChild);

        // The client area sits between the tool bar and the status bar.
        var client = main.MdiClientArea!;
        var menu = main.MainMenuStrip!;
        var status = (StatusStrip)main.Controls["statusStrip1"]!;
        Assert.True(client.Top >= menu.Bottom);
        Assert.Equal(status.Top, client.Bottom);

        main.LayoutMdi(MdiLayout.TileHorizontal);
        using var bitmap = window.Paint();
        Assert.Equal(760, bitmap.Width);

        var outDir = Path.Combine(AppContext.BaseDirectory, "render-out");
        Directory.CreateDirectory(outDir);
        bitmap.Save(Path.Combine(outDir, "mdi-tiled.png"));

        main.LayoutMdi(MdiLayout.Cascade);
        using var cascaded = window.Paint();
        cascaded.Save(Path.Combine(outDir, "mdi-cascade.png"));
    }
}
