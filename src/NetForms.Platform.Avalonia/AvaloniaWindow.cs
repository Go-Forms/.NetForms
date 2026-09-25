using System;
using Size = System.Drawing.Size;
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using AvaloniaPoint = Avalonia.Point;
using AvaloniaSize = Avalonia.Size;

namespace NetForms.Platform.Avalonia;

/// <summary>One Avalonia Window per Form. Sizes and positions are DIPs; the window is sized by its client area.</summary>
internal sealed class AvaloniaWindow : IPlatformWindow
{
    /// <summary>The Avalonia window behind this one; the platform needs it to own native dialogs.</summary>
    internal Window Native => _window;

    private readonly IWindowHost _host;
    private readonly Window _window;
    private readonly FormSurface _surface;
    private Size _clientSize = new(300, 300);
    private Point _location;
    private bool _shown;
    private bool _closed;
    private AvaloniaWindow? _owner;
    private bool _decorations = true;

    public AvaloniaWindow(IWindowHost host)
    {
        _host = host;
        _surface = new FormSurface(host);
        _window = new Window
        {
            Content = _surface,
            SizeToContent = SizeToContent.Manual,
            CanResize = true,
            ShowActivated = true,
        };

        _window.Opened += (_, _) =>
        {
            _shown = true;
            // PositionChanged fires before Opened, when the frame offset is not known yet, so the
            // client origin is only correct from here on: re-read it and tell the form.
            var origin = ClientOriginOnScreen(_window.Position);
            if (origin != _location)
            {
                _location = origin;
                _host.Moved(_location);
            }
            _surface.Focus();
            _host.Shown();
        };
        _window.Closing += (_, e) =>
        {
            if (_closed) return;
            if (_host.Closing(!e.IsProgrammatic)) e.Cancel = true;
        };
        _window.Closed += (_, _) =>
        {
            if (_closed) return;
            _closed = true;
            _host.Closed();
        };
        _window.Activated += (_, _) => _host.Activated();
        _window.PropertyChanged += (_, e) =>
        {
            if (e.Property == Window.WindowStateProperty) _host.StateChanged(State);
        };
        _window.Deactivated += (_, _) => _host.Deactivated();
        _window.Resized += (_, e) =>
        {
            var size = ToSize(e.ClientSize);
            if (size == _clientSize) return;
            _clientSize = size;
            _host.Resized(size);
        };
        _window.PositionChanged += (_, e) =>
        {
            _location = ClientOriginOnScreen(e.Point);
            _host.Moved(_location);
        };
    }

    /// <summary>
    /// Where the client area's (0,0) sits on screen. Avalonia reports the position of the window
    /// frame, but a Form's Size is its client size (docs/PLAN.md, decision 7), so its Location has
    /// to be the client origin too - otherwise every pop-up lands a title bar too high.
    /// </summary>
    private Point ClientOriginOnScreen(PixelPoint framePosition)
    {
        double scale = _window.DesktopScaling;
        if (!_shown) return new Point((int)Math.Round(framePosition.X / scale), (int)Math.Round(framePosition.Y / scale));
        var client = _window.PointToScreen(new AvaloniaPoint(0, 0));
        return new Point((int)Math.Round(client.X / scale), (int)Math.Round(client.Y / scale));
    }

    /// <summary>The frame's thickness above and left of the client area, in pixels.</summary>
    private PixelPoint FrameOffset()
    {
        if (!_shown) return new PixelPoint(0, 0);
        var frame = _window.Position;
        var client = _window.PointToScreen(new AvaloniaPoint(0, 0));
        return new PixelPoint(client.X - frame.X, client.Y - frame.Y);
    }

    private static Size ToSize(AvaloniaSize s) => new((int)Math.Round(s.Width), (int)Math.Round(s.Height));

    public string Title
    {
        get => _window.Title ?? string.Empty;
        set => _window.Title = value;
    }

    public Size ClientSize
    {
        get => _shown ? ToSize(_window.ClientSize) : _clientSize;
        set
        {
            _clientSize = value;
            _window.Width = value.Width;
            _window.Height = value.Height;
        }
    }

    public Point Location
    {
        get => _location;
        set
        {
            _location = value;
            double scale = _window.DesktopScaling;
            // The caller means "put the client area here", so the frame goes above and left of it.
            var offset = FrameOffset();
            _window.Position = new PixelPoint(
                (int)Math.Round(value.X * scale) - offset.X,
                (int)Math.Round(value.Y * scale) - offset.Y);
        }
    }

    public bool Resizable
    {
        get => _window.CanResize;
        set => _window.CanResize = value;
    }

    public bool ShowInTaskbar
    {
        get => _window.ShowInTaskbar;
        set => _window.ShowInTaskbar = value;
    }

    public Size MinimumClientSize
    {
        get => new((int)_window.MinWidth, (int)_window.MinHeight);
        set
        {
            _window.MinWidth = value.Width;
            _window.MinHeight = value.Height;
        }
    }

    public Size MaximumClientSize
    {
        get => new(double.IsInfinity(_window.MaxWidth) ? 0 : (int)_window.MaxWidth, double.IsInfinity(_window.MaxHeight) ? 0 : (int)_window.MaxHeight);
        set
        {
            _window.MaxWidth = value.Width > 0 ? value.Width : double.PositiveInfinity;
            _window.MaxHeight = value.Height > 0 ? value.Height : double.PositiveInfinity;
        }
    }

    public WindowStartPosition StartPosition
    {
        get => _window.WindowStartupLocation switch
        {
            WindowStartupLocation.CenterScreen => WindowStartPosition.CenterScreen,
            WindowStartupLocation.CenterOwner => WindowStartPosition.CenterOwner,
            _ => WindowStartPosition.Manual,
        };
        set => _window.WindowStartupLocation = value switch
        {
            WindowStartPosition.CenterScreen => WindowStartupLocation.CenterScreen,
            WindowStartPosition.CenterOwner => WindowStartupLocation.CenterOwner,
            _ => WindowStartupLocation.Manual,
        };
    }

    public IPlatformWindow? Owner
    {
        get => _owner;
        set => _owner = value as AvaloniaWindow;
    }

    public PlatformWindowState State
    {
        get => _window.WindowState switch
        {
            WindowState.Minimized => PlatformWindowState.Minimized,
            WindowState.Maximized or WindowState.FullScreen => PlatformWindowState.Maximized,
            _ => PlatformWindowState.Normal,
        };
        set => _window.WindowState = value switch
        {
            PlatformWindowState.Minimized => WindowState.Minimized,
            PlatformWindowState.Maximized => WindowState.Maximized,
            _ => WindowState.Normal,
        };
    }

    public bool Decorations
    {
        get => _decorations;
        set
        {
            _decorations = value;
            _window.WindowDecorations = value ? WindowDecorations.Full : WindowDecorations.None;
        }
    }

    public bool CanMinimize
    {
        get => _window.CanMinimize;
        set => _window.CanMinimize = value;
    }

    public bool CanMaximize
    {
        get => _window.CanMaximize;
        set => _window.CanMaximize = value;
    }

    public bool TopMost
    {
        get => _window.Topmost;
        set => _window.Topmost = value;
    }

    public bool ShowActivated
    {
        get => _window.ShowActivated;
        set
        {
            _window.ShowActivated = value;
            if (!value) _window.ShowInTaskbar = false;
        }
    }

    public bool IsVisible => _window.IsVisible;

    public bool IsActive => _window.IsActive;

    public double Scaling => _window.DesktopScaling;

    public void Show()
    {
        if (_owner != null) _window.Show(_owner._window);
        else _window.Show();
    }

    public void ShowModal(IPlatformWindow? owner)
    {
        var ownerWindow = (owner as AvaloniaWindow ?? _owner)?._window;
        if (ownerWindow != null)
        {
            // Avalonia's ShowDialog is asynchronous; NetForms runs its own nested loop meanwhile.
            _ = _window.ShowDialog(ownerWindow);
        }
        else
        {
            _window.Show();
        }
    }

    public void Hide() => _window.Hide();

    public void Close()
    {
        if (_closed) return;
        _window.Close();
    }

    public void Activate() => _window.Activate();

    public void Invalidate(Rectangle? area) => _surface.Invalidate(area);

    /// <summary>Avalonia captures the pointer implicitly between press and release, which is what WinForms needs.</summary>
    public void SetCapture(bool capture) { }

    public void SetCursor(string cursorName)
    {
        var type = cursorName switch
        {
            "Hand" => StandardCursorType.Hand,
            "IBeam" => StandardCursorType.Ibeam,
            "Wait" => StandardCursorType.Wait,
            "Cross" => StandardCursorType.Cross,
            "No" => StandardCursorType.No,
            "SizeAll" => StandardCursorType.SizeAll,
            "SizeNS" => StandardCursorType.SizeNorthSouth,
            "SizeWE" => StandardCursorType.SizeWestEast,
            "SizeNESW" => StandardCursorType.BottomLeftCorner,
            "SizeNWSE" => StandardCursorType.BottomRightCorner,
            "Help" => StandardCursorType.Help,
            "AppStarting" => StandardCursorType.AppStarting,
            "HSplit" => StandardCursorType.SizeNorthSouth,
            "VSplit" => StandardCursorType.SizeWestEast,
            "UpArrow" => StandardCursorType.UpArrow,
            "DragCopy" => StandardCursorType.DragCopy,
            "DragMove" => StandardCursorType.DragMove,
            "DragLink" => StandardCursorType.DragLink,
            _ => StandardCursorType.Arrow,
        };
        _surface.Cursor = new global::Avalonia.Input.Cursor(type);
    }

    public void Dispose()
    {
        if (!_closed)
        {
            _closed = true;
            _window.Close();
        }
    }
}
