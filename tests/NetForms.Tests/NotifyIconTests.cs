using NetForms.Platform;
using Xunit;

namespace NetForms.Tests;

/// <summary>NotifyIcon through the platform's tray icon; the test platform plays the shell.</summary>
public class NotifyIconTests
{
    private static Icon SomeIcon() => new(SystemIcons.Information, 16, 16);

    [Fact]
    public void TheIconIsInTheTrayOnlyWhileVisibleAndWithAnIcon()
    {
        var platform = TestPlatform.Install();
        using var notify = new NotifyIcon { Text = "Backup running" };
        int before = platform.TrayIcons.Count;
        Assert.Equal(before, platform.TrayIcons.Count); // nothing until it is shown

        notify.Visible = true;
        var tray = platform.TrayIcons.Last();
        Assert.Equal(before + 1, platform.TrayIcons.Count);
        Assert.False(tray.Visible); // Shell_NotifyIcon adds nothing without an icon
        Assert.Equal("Backup running", tray.ToolTip);

        notify.Icon = SomeIcon();
        Assert.True(tray.Visible);
        Assert.NotNull(tray.IconPng);

        notify.Text = "Done";
        Assert.Equal("Done", tray.ToolTip);
        Assert.Throws<ArgumentOutOfRangeException>(() => notify.Text = new string('x', 128));

        notify.Visible = false;
        Assert.False(tray.Visible);
        notify.Dispose();
        Assert.True(tray.Disposed);
    }

    [Fact]
    public void ClicksAndDoubleClicksComeInWinFormsOrder()
    {
        var platform = TestPlatform.Install();
        using var notify = new NotifyIcon { Icon = SomeIcon(), Visible = true };
        var tray = platform.TrayIcons.Last();
        var log = new List<string>();
        notify.MouseDown += (_, e) => log.Add($"MouseDown {e.Button} {e.Clicks}");
        notify.MouseUp += (_, e) => log.Add($"MouseUp {e.Button}");
        notify.Click += (_, e) => log.Add($"Click {((MouseEventArgs)e).Button}");
        notify.MouseClick += (_, e) => log.Add($"MouseClick {e.Button}");
        notify.DoubleClick += (_, _) => log.Add("DoubleClick");
        notify.MouseDoubleClick += (_, e) => log.Add($"MouseDoubleClick {e.Button}");

        tray.Host.Clicked();
        tray.Host.Clicked(); // within the double-click time
        Assert.Equal(new[]
        {
            "MouseDown Left 1", "MouseUp Left", "Click Left", "MouseClick Left",
            "DoubleClick", "MouseDoubleClick Left", "MouseDown Left 2", "MouseUp Left",
        }, log);

        // A third click starts over: it is a single click again.
        log.Clear();
        tray.Host.Clicked();
        Assert.Contains("Click Left", log);
        Assert.DoesNotContain("DoubleClick", log);
    }

    [Fact]
    public void TheShellMenuIsBuiltFromTheContextMenuStrip()
    {
        var platform = TestPlatform.Install();
        var menu = new ContextMenuStrip();
        int opened = 0, exits = 0;
        var open = new ToolStripMenuItem("&Open");
        open.Click += (_, _) => opened++;
        var exit = new ToolStripMenuItem("E&xit") { Enabled = false };
        exit.Click += (_, _) => exits++;
        var more = new ToolStripMenuItem("More");
        more.DropDownItems.Add(new ToolStripMenuItem("Details") { Checked = true });
        menu.Items.AddRange(new ToolStripItem[] { open, new ToolStripSeparator(), exit, more, new ToolStripMenuItem("Hidden") { Visible = false } });
        using var notify = new NotifyIcon { Icon = SomeIcon(), ContextMenuStrip = menu, Visible = true };
        var tray = platform.TrayIcons.Last();

        var items = tray.Menu!;
        Assert.Equal(new[] { "Open", "", "Exit", "More" }, items.Select(i => i.Text));
        Assert.True(items[1].IsSeparator);
        Assert.False(items[2].Enabled);
        Assert.True(items[3].Items![0].Checked);

        items[0].Click!();
        Assert.Equal(1, opened);

        // A right click opens the menu: the strip's Opening runs first and may change it, then the mouse events.
        var log = new List<string>();
        menu.Opening += (_, _) =>
        {
            log.Add("Opening");
            if (menu.Items.Count == 5) menu.Items.Add(new ToolStripMenuItem("Added late"));
        };
        menu.Opened += (_, _) => log.Add("Opened");
        menu.Closed += (_, _) => log.Add("Closed");
        notify.MouseDown += (_, e) => log.Add($"MouseDown {e.Button}");
        notify.MouseClick += (_, e) => log.Add($"MouseClick {e.Button}");
        tray.Host.MenuOpening();
        tray.Host.MenuClosed();
        Assert.Equal(new[] { "MouseDown Right", "Opening", "Opened", "MouseClick Right", "Closed" }, log);
        Assert.Equal("Added late", tray.Menu!.Last().Text);
    }

    [Fact]
    public void TheBalloonShowsInTheCornerAndReportsHowItWentAway()
    {
        var platform = TestPlatform.Install();
        using var notify = new NotifyIcon();
        var log = new List<string>();
        notify.BalloonTipShown += (_, _) => log.Add("Shown");
        notify.BalloonTipClicked += (_, _) => log.Add("Clicked");
        notify.BalloonTipClosed += (_, _) => log.Add("Closed");

        // Not in the tray: nothing shows (WinForms checks _added).
        int windows = platform.Windows.Count;
        notify.ShowBalloonTip(1000, "Title", "Text", ToolTipIcon.Info);
        Assert.Equal(windows, platform.Windows.Count);
        Assert.Throws<ArgumentException>(() => notify.ShowBalloonTip(1000, "Title", "", ToolTipIcon.None));

        notify.Icon = SomeIcon();
        notify.Visible = true;
        notify.ShowBalloonTip(1000, "Backup", "The backup has finished.", ToolTipIcon.Info);
        var balloon = platform.Windows.Last();
        Assert.True(balloon.IsVisible);
        Assert.False(balloon.ShowActivated);
        Assert.Equal(new[] { "Shown" }, log);
        using (var bmp = balloon.Paint()) Assert.True(bmp.Width >= 300);

        // It times out by itself...
        platform.Timers.Last(t => t.IsRunning && t.Interval == TimeSpan.FromMilliseconds(NotifyIcon.BalloonLifetime)).Fire();
        Assert.False(balloon.IsVisible);
        Assert.Equal(new[] { "Shown", "Closed" }, log);

        // ...or the user clicks it.
        log.Clear();
        notify.BalloonTipText = "Click me";
        notify.ShowBalloonTip(0);
        platform.Windows.Last().Click(new Point(20, 20));
        Assert.Equal(new[] { "Shown", "Clicked" }, log);
        Assert.Null(notify.Balloon);

        // Hiding the icon takes the balloon with it.
        log.Clear();
        notify.ShowBalloonTip(0);
        notify.Visible = false;
        Assert.Equal(new[] { "Shown", "Closed" }, log);
    }
}
