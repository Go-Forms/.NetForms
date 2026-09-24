using NetForms.Platform;
using SkiaSharp;

namespace NetForms.Tests;

/// <summary>
/// A platform without windows: lets tests show a Form, drive its IWindowHost directly
/// (mouse, keys, resize) and paint it into a bitmap, all without Avalonia.
/// </summary>
internal sealed class TestPlatform : IPlatform
{
    public static TestPlatform Install()
    {
        if (Application.Platform is TestPlatform existing) return existing;
        var p = new TestPlatform();
        Application.Platform = p;
        return p;
    }

    public List<TestWindow> Windows { get; } = new();

    // --- native dialogs: the test platform answers with whatever the test queued up ---------

    /// <summary>What the next ShowOpenFileDialog returns; null means the user cancelled.</summary>
    public string[]? OpenFileResult { get; set; }

    public string? SaveFileResult { get; set; }

    public string? FolderResult { get; set; }

    /// <summary>The options each picker was asked with, so a test can assert on them.</summary>
    public List<FileDialogOptions> FileDialogCalls { get; } = new();

    public List<FolderDialogOptions> FolderDialogCalls { get; } = new();

    public string[]? ShowOpenFileDialog(IPlatformWindow? owner, FileDialogOptions options)
    {
        FileDialogCalls.Add(options);
        return OpenFileResult;
    }

    public string? ShowSaveFileDialog(IPlatformWindow? owner, FileDialogOptions options)
    {
        FileDialogCalls.Add(options);
        return SaveFileResult;
    }

    public string? ShowFolderDialog(IPlatformWindow? owner, FolderDialogOptions options)
    {
        FolderDialogCalls.Add(options);
        return FolderResult;
    }

    public void Initialize() { }

    public IPlatformWindow CreateWindow(IWindowHost host)
    {
        var w = new TestWindow(host);
        Windows.Add(w);
        return w;
    }

    /// <summary>Called instead of pumping messages, so a test can drive a modal dialog to its end.</summary>
    public Action? OnMessageLoop { get; set; }

    public void RunMessageLoop()
    {
        var handler = OnMessageLoop;
        OnMessageLoop = null;
        handler?.Invoke();
    }
    public void ExitMessageLoop() { }
    public void DoEvents() { }
    public bool IsUIThread => true;
    public void Post(Action action) => action();

    public List<TestTimer> Timers { get; } = new();

    public List<TestTrayIcon> TrayIcons { get; } = new();

    public IPlatformTrayIcon? CreateTrayIcon(ITrayIconHost host)
    {
        var icon = new TestTrayIcon(host);
        TrayIcons.Add(icon);
        return icon;
    }

    public IPlatformTimer CreateTimer(Action tick)
    {
        var t = new TestTimer(tick, this);
        Timers.Add(t);
        return t;
    }

    public string? ClipboardText { get; set; }
    public string? GetClipboardText() => ClipboardText;
    public void SetClipboardText(string text) => ClipboardText = text;

    public ScreenInfo[] GetScreens() => new[] { new ScreenInfo("test", new Rectangle(0, 0, 1920, 1080), new Rectangle(0, 0, 1920, 1040), true, 32) };
}

internal sealed class TestTimer : IPlatformTimer
{
    private readonly Action _tick;
    private readonly TestPlatform _owner;

    public TestTimer(Action tick, TestPlatform owner)
    {
        _tick = tick;
        _owner = owner;
    }

    public TimeSpan Interval { get; set; }
    public bool IsRunning { get; private set; }
    public void Start() => IsRunning = true;
    public void Stop() => IsRunning = false;
    public void Fire() { if (IsRunning) _tick(); }
    public void Dispose() => _owner.Timers.Remove(this);
}

internal sealed class TestWindow : IPlatformWindow
{
    public TestWindow(IWindowHost host) => Host = host;

    public IWindowHost Host { get; }
    public string Title { get; set; } = string.Empty;
    public Size ClientSize { get; set; }
    public Point Location { get; set; }
    public bool Resizable { get; set; } = true;
    public bool ShowInTaskbar { get; set; } = true;
    public Size MinimumClientSize { get; set; }
    public Size MaximumClientSize { get; set; }
    public WindowStartPosition StartPosition { get; set; }
    public IPlatformWindow? Owner { get; set; }
    public PlatformWindowState State { get; set; }
    public bool Decorations { get; set; } = true;
    public bool CanMinimize { get; set; } = true;
    public bool CanMaximize { get; set; } = true;
    public bool TopMost { get; set; }
    public bool ShowActivated { get; set; } = true;
    public string CursorName { get; private set; } = "Default";
    public bool IsModal { get; private set; }
    public bool IsVisible { get; private set; }
    public bool IsActive { get; private set; }
    public double Scaling => 1.0;
    public int InvalidateCount { get; private set; }
    public List<Rectangle> Invalidated { get; } = new();

    public void Show()
    {
        IsVisible = true;
        Host.Shown();
        if (ShowActivated)
        {
            IsActive = true;
            Host.Activated();
        }
    }

    public void ShowModal(IPlatformWindow? owner)
    {
        IsModal = true;
        Show();
    }

    public void Hide() => IsVisible = false;

    public void Close()
    {
        if (Host.Closing(userRequested: false)) return;
        IsVisible = false;
        Host.Closed();
    }

    public void Activate() { }

    public void Invalidate(Rectangle? area)
    {
        InvalidateCount++;
        Invalidated.Add(area ?? new Rectangle(Point.Empty, ClientSize));
    }

    public void SetCapture(bool capture) { }

    public void SetCursor(string cursorName) => CursorName = cursorName;

    public void Dispose() { }

    /// <summary>Simulate the user resizing the window.</summary>
    public void Resize(int width, int height)
    {
        ClientSize = new Size(width, height);
        Host.Resized(ClientSize);
    }

    public void Click(Point p)
    {
        Host.MouseMove(p, MouseButton.None, InputModifiers.None);
        Host.MouseDown(MouseButton.Left, p, 1, InputModifiers.None);
        Host.MouseUp(MouseButton.Left, p, InputModifiers.None);
    }

    public Bitmap Paint()
    {
        var bmp = new Bitmap(Math.Max(1, ClientSize.Width), Math.Max(1, ClientSize.Height));
        using var canvas = new SKCanvas(bmp.Skia);
        Host.Paint(canvas, ClientSize, new Rectangle(Point.Empty, ClientSize));
        return bmp;
    }
}

/// <summary>A tray icon that remembers what it was told and lets a test play the shell.</summary>
internal sealed class TestTrayIcon : IPlatformTrayIcon
{
    public TestTrayIcon(ITrayIconHost host) => Host = host;

    public ITrayIconHost Host { get; }
    public byte[]? IconPng { get; set; }
    public string ToolTip { get; set; } = string.Empty;
    public bool Visible { get; set; }
    public IReadOnlyList<TrayMenuItem>? Menu { get; set; }
    public bool Disposed { get; private set; }

    public void Dispose() => Disposed = true;
}
