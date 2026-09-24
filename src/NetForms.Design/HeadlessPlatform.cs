using System;
using System.Drawing;
using System.Windows.Forms;
using NetForms.Platform;

namespace NetForms.Design;

/// <summary>
/// The platform of the designer process: no windows, no message loop, no ticking timers. A designed
/// form lives only as an object tree that is painted into bitmaps; nothing in it may run on its own
/// (a <c>Timer</c> enabled in the designer must not fire), and nothing may pop up a window.
/// </summary>
internal sealed class HeadlessPlatform : IPlatform
{
    /// <summary>Installs the headless platform unless another non-Avalonia one (a test's) is already there.</summary>
    public static void Install()
    {
        if (Application.PlatformIfCreated == null) Application.Platform = new HeadlessPlatform();
    }

    public void Initialize() { }
    public IPlatformWindow CreateWindow(IWindowHost host) => new HeadlessWindow(host);
    public void RunMessageLoop() { }
    public void ExitMessageLoop() { }
    public void DoEvents() { }
    public bool IsUIThread => true;
    public void Post(Action action) => action();
    public IPlatformTimer CreateTimer(Action tick) => new InertTimer();
    public ScreenInfo[] GetScreens() => new[] { new ScreenInfo("designer", new Rectangle(0, 0, 1920, 1080), new Rectangle(0, 0, 1920, 1040), true, 32) };
    public string? GetClipboardText() => null;
    public void SetClipboardText(string text) { }
    public string[]? ShowOpenFileDialog(IPlatformWindow? owner, FileDialogOptions options) => null;
    public string? ShowSaveFileDialog(IPlatformWindow? owner, FileDialogOptions options) => null;
    public string? ShowFolderDialog(IPlatformWindow? owner, FolderDialogOptions options) => null;

    private sealed class InertTimer : IPlatformTimer
    {
        public TimeSpan Interval { get; set; }
        public bool IsRunning { get; private set; }
        public void Start() => IsRunning = true;
        public void Stop() => IsRunning = false;
        public void Dispose() { }
    }

    private sealed class HeadlessWindow : IPlatformWindow
    {
        private readonly IWindowHost _host;

        public HeadlessWindow(IWindowHost host) => _host = host;

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
        public bool IsVisible => false;
        public bool IsActive => false;
        public double Scaling => 1.0;
        public void Show() { }
        public void ShowModal(IPlatformWindow? owner) { }
        public void Hide() { }
        public void Close() => _host.Closed();
        public void Activate() { }
        public void Invalidate(Rectangle? area) { }
        public void SetCapture(bool capture) { }
        public void SetCursor(string cursorName) { }
        public void Dispose() { }
    }
}
