using System;
using System.Collections.Generic;
using System.Drawing;

namespace NetForms.Platform;

/// <summary>
/// What the OS gave WinForms and what Avalonia gives us: windows, a message loop,
/// an input stream. Everything above this line (the Control tree, painting, focus,
/// layout) belongs to NetForms itself and never sees the toolkit behind this interface.
/// </summary>
public interface IPlatform
{
    /// <summary>Initialise the toolkit on the calling thread. Idempotent.</summary>
    void Initialize();

    /// <summary>Create a top-level window that reports to <paramref name="host"/>.</summary>
    IPlatformWindow CreateWindow(IWindowHost host);

    /// <summary>Run the message loop on the current thread until <see cref="ExitMessageLoop"/>.</summary>
    void RunMessageLoop();

    /// <summary>Make the innermost <see cref="RunMessageLoop"/> return.</summary>
    void ExitMessageLoop();

    /// <summary>Process pending messages without blocking (Application.DoEvents).</summary>
    void DoEvents();

    /// <summary>True when called on the UI thread.</summary>
    bool IsUIThread { get; }

    /// <summary>Queue <paramref name="action"/> on the UI thread.</summary>
    void Post(Action action);

    /// <summary>Create a UI-thread timer that calls <paramref name="tick"/> every interval while started.</summary>
    IPlatformTimer CreateTimer(Action tick);

    /// <summary>The monitors, primary first.</summary>
    ScreenInfo[] GetScreens();

    /// <summary>Plain text on the system clipboard, or null when there is none.</summary>
    string? GetClipboardText();

    /// <summary>Put plain text on the system clipboard (empty string clears it).</summary>
    void SetClipboardText(string text);

    /// <summary>
    /// The system's own file and folder pickers. Runs modally against <paramref name="owner"/> and
    /// returns null when the user cancels; an empty array means the same for the multi-select forms.
    /// </summary>
    string[]? ShowOpenFileDialog(IPlatformWindow? owner, FileDialogOptions options);

    string? ShowSaveFileDialog(IPlatformWindow? owner, FileDialogOptions options);

    string? ShowFolderDialog(IPlatformWindow? owner, FolderDialogOptions options);

    /// <summary>
    /// An icon in the notification area (WinForms' NotifyIcon), or null where there is none - the
    /// default, so platforms without a tray (tests, the designer) need not implement it.
    /// </summary>
    IPlatformTrayIcon? CreateTrayIcon(ITrayIconHost host) => null;
}

/// <summary>A notification-area icon. Setters take effect at once; nothing shows until <see cref="Visible"/>.</summary>
public interface IPlatformTrayIcon : IDisposable
{
    /// <summary>The icon as PNG bytes; null for none (the shell then shows nothing).</summary>
    byte[]? IconPng { set; }

    string ToolTip { set; }

    bool Visible { set; }

    /// <summary>The menu the shell shows on a right click; null for none.</summary>
    IReadOnlyList<TrayMenuItem>? Menu { set; }
}

/// <summary>What the tray icon reports back.</summary>
public interface ITrayIconHost
{
    /// <summary>The icon was clicked (the primary button: that is all the shells report).</summary>
    void Clicked();

    /// <summary>The shell is about to show the menu: the host may set a new <see cref="IPlatformTrayIcon.Menu"/>.</summary>
    void MenuOpening();

    /// <summary>The menu was dismissed, with or without a choice.</summary>
    void MenuClosed();
}

/// <summary>One entry of a tray menu: a command, a sub-menu (<see cref="Items"/>) or a separator.</summary>
public sealed class TrayMenuItem
{
    public string Text { get; init; } = string.Empty;
    public bool Enabled { get; init; } = true;
    public bool Checked { get; init; }
    public bool IsSeparator { get; init; }
    public Action? Click { get; init; }
    public IReadOnlyList<TrayMenuItem>? Items { get; init; }
}

/// <summary>What a file picker should offer; the managed side owns the WinForms-shaped API above it.</summary>
public sealed class FileDialogOptions
{
    public string Title { get; set; } = string.Empty;

    /// <summary>WinForms' filter string: "Text files|*.txt|All files|*.*".</summary>
    public string Filter { get; set; } = string.Empty;

    /// <summary>One-based index into <see cref="Filter"/>, as WinForms counts it.</summary>
    public int FilterIndex { get; set; } = 1;

    public string InitialDirectory { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string DefaultExt { get; set; } = string.Empty;

    public bool Multiselect { get; set; }

    public bool AddExtension { get; set; } = true;
}

public sealed class FolderDialogOptions
{
    public string Title { get; set; } = string.Empty;

    public string InitialDirectory { get; set; } = string.Empty;
}

public interface IPlatformTimer : IDisposable
{
    TimeSpan Interval { get; set; }
    bool IsRunning { get; }
    void Start();
    void Stop();
}

public sealed record ScreenInfo(string DeviceName, Rectangle Bounds, Rectangle WorkingArea, bool Primary, int BitsPerPixel);

/// <summary>A top-level native window. All coordinates are device-independent pixels (96 DPI).</summary>
public interface IPlatformWindow : IDisposable
{
    string Title { get; set; }
    Size ClientSize { get; set; }
    Point Location { get; set; }
    bool Resizable { get; set; }
    bool ShowInTaskbar { get; set; }
    Size MinimumClientSize { get; set; }
    Size MaximumClientSize { get; set; }
    WindowStartPosition StartPosition { get; set; }
    IPlatformWindow? Owner { get; set; }
    PlatformWindowState State { get; set; }
    /// <summary>Title bar and frame on/off (FormBorderStyle.None).</summary>
    bool Decorations { get; set; }
    bool CanMinimize { get; set; }
    bool CanMaximize { get; set; }
    bool TopMost { get; set; }
    /// <summary>False for popups (drop-downs, tooltips) that must not take the focus from their owner.</summary>
    bool ShowActivated { get; set; }
    bool IsVisible { get; }
    bool IsActive { get; }
    double Scaling { get; }

    void Show();
    /// <summary>Show as a modal dialog of <paramref name="owner"/> (or application-modal when null): the owner stops receiving input until this window closes.</summary>
    void ShowModal(IPlatformWindow? owner);
    void Hide();
    /// <summary>Close the window. The host's <see cref="IWindowHost.Closing"/> is consulted first.</summary>
    void Close();
    void Activate();
    /// <summary>Request a repaint; <c>null</c> means the whole client area.</summary>
    void Invalidate(Rectangle? area);
    void SetCapture(bool capture);
    /// <summary>Set the pointer cursor by its WinForms name ("Default", "Hand", "IBeam", "SizeWE", ...).</summary>
    void SetCursor(string cursorName);
}

public enum PlatformWindowState
{
    Normal,
    Minimized,
    Maximized,
}

public enum WindowStartPosition
{
    Manual,
    CenterScreen,
    CenterOwner,
    WindowsDefault,
}
