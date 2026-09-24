using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using NetForms.Platform;

namespace System.Windows.Forms;

/// <summary>
/// An icon in the notification area: Shell_NotifyIcon on Windows, a StatusNotifierItem on Linux (through the
/// platform layer, <see cref="IPlatform.CreateTrayIcon"/>).
/// </summary>
/// <remarks>
/// Differences the shells force (decision 119): they report only a primary-button click, so a double click is
/// two clicks within <see cref="SystemInformation.DoubleClickTime"/>, and a right click is known only when it
/// opens the <see cref="ContextMenuStrip"/> - which the shell draws itself, from the strip's menu items. The
/// balloon tip is NetForms' own non-activating window in the corner of the working area, the same on both
/// systems; like Windows since Vista, it ignores the timeout argument.
/// </remarks>
[DefaultProperty(nameof(Text))]
[DefaultEvent(nameof(MouseDoubleClick))]
[ToolboxItemFilter("System.Windows.Forms")]
[Description("Displays an icon in the notification area, on the right side of the Windows taskbar, during run time.")]
public sealed class NotifyIcon : Component
{
    internal const int MaxTextSize = 127;

    /// <summary>How long a balloon stays up (Windows' default notification time).</summary>
    internal const int BalloonLifetime = 5000;

    private Icon? _icon;
    private string _text = string.Empty;
    private bool _visible;
    private ContextMenuStrip? _contextMenuStrip;
    private ToolTipIcon _balloonTipIcon;
    private string _balloonTipText = string.Empty;
    private string _balloonTipTitle = string.Empty;
    private IPlatformTrayIcon? _tray;
    private bool _trayCreated;
    private long _lastClick = long.MinValue / 2;
    private BalloonPopup? _balloon;

    public NotifyIcon()
    {
    }

    public NotifyIcon(IContainer container)
        : this()
    {
        ArgumentNullException.ThrowIfNull(container);
        container.Add(this);
    }

    [Category("Appearance")]
    [Localizable(true)]
    [DefaultValue("")]
    [Description("The text to associate with the balloon ToolTip.")]
    public string BalloonTipText
    {
        get => _balloonTipText;
        set => _balloonTipText = value;
    }

    [Category("Appearance")]
    [DefaultValue(ToolTipIcon.None)]
    [Description("The icon to associate with the balloon ToolTip.")]
    public ToolTipIcon BalloonTipIcon
    {
        get => _balloonTipIcon;
        set
        {
            if (!Enum.IsDefined(value)) throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(ToolTipIcon));
            _balloonTipIcon = value;
        }
    }

    [Category("Appearance")]
    [Localizable(true)]
    [DefaultValue("")]
    [Description("The title of the balloon ToolTip.")]
    public string BalloonTipTitle
    {
        get => _balloonTipTitle;
        set => _balloonTipTitle = value;
    }

    [Category("Action")]
    [Description("Occurs when a balloon ToolTip is clicked.")]
    public event EventHandler? BalloonTipClicked;

    [Category("Action")]
    [Description("Occurs when a balloon ToolTip is closed.")]
    public event EventHandler? BalloonTipClosed;

    [Category("Action")]
    [Description("Occurs when a balloon tip is shown.")]
    public event EventHandler? BalloonTipShown;

    [DefaultValue(null)]
    [Category("Behavior")]
    [Description("The shortcut menu to show when the user right-clicks the icon.")]
    public ContextMenuStrip? ContextMenuStrip
    {
        get => _contextMenuStrip;
        set
        {
            _contextMenuStrip = value;
            if (_tray != null) _tray.Menu = BuildMenu();
        }
    }

    [Category("Appearance")]
    [Localizable(true)]
    [DefaultValue(null)]
    [Description("The icon to display in the system tray.")]
    public Icon? Icon
    {
        get => _icon;
        set
        {
            if (_icon == value) return;
            _icon = value;
            UpdateIcon(_visible);
        }
    }

    [Category("Appearance")]
    [Localizable(true)]
    [DefaultValue("")]
    [AllowNull]
    [Description("The text that will be displayed when the mouse hovers over the icon.")]
    public string Text
    {
        get => _text;
        set
        {
            value ??= string.Empty;
            if (value == _text) return;
            if (value.Length > MaxTextSize) throw new ArgumentOutOfRangeException(nameof(Text), value, "Text length must be less than 128 characters long.");
            _text = value;
            if (_tray != null) _tray.ToolTip = value;
        }
    }

    [Category("Behavior")]
    [Localizable(true)]
    [DefaultValue(false)]
    [Description("Determines whether the control is visible or hidden.")]
    public bool Visible
    {
        get => _visible;
        set
        {
            if (_visible == value) return;
            UpdateIcon(value);
            _visible = value;
        }
    }

    [Category("Data")]
    [Localizable(false)]
    [Bindable(true)]
    [Description("User-defined data associated with the object.")]
    [DefaultValue(null)]
    [TypeConverter(typeof(StringConverter))]
    public object? Tag { get; set; }

    [Category("Action")]
    [Description("Occurs when the component is clicked.")]
    public event EventHandler? Click;

    [Category("Action")]
    [Description("Occurs when the component is double-clicked.")]
    public event EventHandler? DoubleClick;

    [Category("Action")]
    [Description("Occurs when the component is clicked with the mouse.")]
    public event MouseEventHandler? MouseClick;

    [Category("Action")]
    [Description("Occurs when the component is double-clicked with the mouse. ")]
    public event MouseEventHandler? MouseDoubleClick;

    [Category("Mouse")]
    [Description("Occurs when the mouse pointer is over the component and a mouse button is pressed.")]
    public event MouseEventHandler? MouseDown;

#pragma warning disable CS0067 // Declared for source compatibility: no shell reports the pointer moving over the icon.
    [Category("Mouse")]
    [Description("Occurs when the mouse pointer is moved over the component.")]
    public event MouseEventHandler? MouseMove;
#pragma warning restore CS0067

    [Category("Mouse")]
    [Description("Occurs when the mouse pointer is over the component and a mouse button is released.")]
    public event MouseEventHandler? MouseUp;

    /// <summary>True while the icon is in the notification area (WinForms' <c>_added</c>): visible and with an icon.</summary>
    private bool Added => _visible && _icon != null && !DesignMode;

    public void ShowBalloonTip(int timeout) => ShowBalloonTip(timeout, _balloonTipTitle, _balloonTipText, _balloonTipIcon);

    public void ShowBalloonTip(int timeout, string tipTitle, string tipText, ToolTipIcon tipIcon)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(timeout);
        if (string.IsNullOrEmpty(tipText)) throw new ArgumentException("Balloon tip text must have a non-empty value.");
        if (!Enum.IsDefined(tipIcon)) throw new InvalidEnumArgumentException(nameof(tipIcon), (int)tipIcon, typeof(ToolTipIcon));
        if (!Added) return;

        CloseBalloon();
        _balloon = new BalloonPopup(this, tipTitle ?? string.Empty, tipText, tipIcon);
        _balloon.Open();
        BalloonTipShown?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The balloon went away: the user dismissed it, it timed out or a new one replaced it.</summary>
    private void CloseBalloon()
    {
        var balloon = _balloon;
        if (balloon == null) return;
        _balloon = null;
        balloon.Close();
        balloon.Dispose();
        BalloonTipClosed?.Invoke(this, EventArgs.Empty);
    }

    private void BalloonClicked()
    {
        var balloon = _balloon;
        if (balloon == null) return;
        _balloon = null;
        balloon.Close();
        balloon.Dispose();
        BalloonTipClicked?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The balloon on screen, for tests.</summary>
    internal Form? Balloon => _balloon;

    private void UpdateIcon(bool showIconInTray)
    {
        if (DesignMode) return;
        if (!_trayCreated && showIconInTray)
        {
            _trayCreated = true;
            _tray = Application.Platform.CreateTrayIcon(new Host(this));
            if (_tray != null)
            {
                _tray.ToolTip = _text;
                _tray.Menu = BuildMenu();
            }
        }
        if (_tray == null) return;
        _tray.IconPng = _icon == null ? null : ToPng(_icon);
        _tray.Visible = showIconInTray && _icon != null;
        if (!(showIconInTray && _icon != null)) CloseBalloon();
    }

    private static byte[] ToPng(Icon icon)
    {
        using var stream = new MemoryStream();
        icon.Save(stream);
        return stream.ToArray();
    }

    /// <summary>The strip's visible menu items as the shell's menu: text without mnemonics, state, sub-menus.</summary>
    private IReadOnlyList<TrayMenuItem>? BuildMenu() => _contextMenuStrip == null ? null : BuildItems(_contextMenuStrip.Items);

    private static List<TrayMenuItem> BuildItems(ToolStripItemCollection items)
    {
        var result = new List<TrayMenuItem>();
        foreach (ToolStripItem item in items)
        {
            if (!item.Available) continue;
            switch (item)
            {
                case ToolStripSeparator:
                    result.Add(new TrayMenuItem { IsSeparator = true });
                    break;
                case ToolStripMenuItem menuItem:
                    result.Add(new TrayMenuItem
                    {
                        Text = StripMnemonics(menuItem.Text ?? string.Empty),
                        Enabled = menuItem.Enabled,
                        Checked = menuItem.Checked,
                        Click = menuItem.PerformClick,
                        Items = menuItem.HasDropDownItems ? BuildItems(menuItem.DropDownItems) : null,
                    });
                    break;
            }
        }
        return result;
    }

    private static string StripMnemonics(string text)
    {
        if (text.IndexOf('&') < 0) return text;
        var sb = new System.Text.StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '&' && i + 1 < text.Length) i++;
            sb.Append(text[i]);
        }
        return sb.ToString();
    }

    // --- what the shell reports ------------------------------------------------------------

    /// <summary>A primary click; the second one within the double-click time is a double click, as WM_LBUTTONDBLCLK.</summary>
    internal void OnShellClicked()
    {
        long now = Environment.TickCount64;
        bool doubleClick = now - _lastClick <= SystemInformation.DoubleClickTime;
        _lastClick = doubleClick ? long.MinValue / 2 : now;
        if (doubleClick)
        {
            DoubleClick?.Invoke(this, new MouseEventArgs(MouseButtons.Left, 2, 0, 0, 0));
            MouseDoubleClick?.Invoke(this, new MouseEventArgs(MouseButtons.Left, 2, 0, 0, 0));
            MouseDown?.Invoke(this, new MouseEventArgs(MouseButtons.Left, 2, 0, 0, 0));
            MouseUp?.Invoke(this, new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0));
            return;
        }
        MouseDown?.Invoke(this, new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
        MouseUp?.Invoke(this, new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0));
        Click?.Invoke(this, new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0));
        MouseClick?.Invoke(this, new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0));
    }

    /// <summary>A right click that opens the menu: WM_RBUTTONDOWN, the menu's Opening, then WM_RBUTTONUP.</summary>
    internal void OnShellMenuOpening()
    {
        MouseDown?.Invoke(this, new MouseEventArgs(MouseButtons.Right, 1, 0, 0, 0));
        if (_contextMenuStrip != null && _tray != null)
        {
            bool cancelled = _contextMenuStrip.RaiseOpeningForShell();
            _tray.Menu = cancelled ? Array.Empty<TrayMenuItem>() : BuildMenu();
            if (!cancelled) _contextMenuStrip.RaiseOpenedForShell();
        }
        MouseUp?.Invoke(this, new MouseEventArgs(MouseButtons.Right, 0, 0, 0, 0));
        Click?.Invoke(this, new MouseEventArgs(MouseButtons.Right, 0, 0, 0, 0));
        MouseClick?.Invoke(this, new MouseEventArgs(MouseButtons.Right, 0, 0, 0, 0));
    }

    internal void OnShellMenuClosed() => _contextMenuStrip?.RaiseClosedForShell();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CloseBalloon();
            _icon = null;
            _text = string.Empty;
            _tray?.Dispose();
            _tray = null;
            _contextMenuStrip = null;
        }
        base.Dispose(disposing);
    }

    private sealed class Host : ITrayIconHost
    {
        private readonly NotifyIcon _owner;

        public Host(NotifyIcon owner) => _owner = owner;

        public void Clicked() => Application.Dispatch(_owner.OnShellClicked);

        public void MenuOpening() => Application.Dispatch(_owner.OnShellMenuOpening);

        public void MenuClosed() => Application.Dispatch(_owner.OnShellMenuClosed);
    }

    /// <summary>The balloon: icon, bold title and wrapped text, in the corner of the primary working area.</summary>
    private sealed class BalloonPopup : PopupForm
    {
        private const int Width_ = 360;
        private const int Pad = 12;
        private const int IconSize = 32;

        private readonly NotifyIcon _owner;
        private readonly string _title;
        private readonly string _body;
        private readonly Icon? _icon;
        private readonly Timer _timer = new() { Interval = BalloonLifetime };
        private Font? _titleFont;

        public BalloonPopup(NotifyIcon owner, string title, string body, ToolTipIcon icon)
        {
            _owner = owner;
            _title = title;
            _body = body;
            _icon = icon switch
            {
                ToolTipIcon.Info => SystemIcons.Information,
                ToolTipIcon.Warning => SystemIcons.Warning,
                ToolTipIcon.Error => SystemIcons.Error,
                _ => null,
            };
            BackColor = SystemColors.Window;
            ForeColor = SystemColors.WindowText;
            _timer.Tick += (_, _) => _owner.CloseBalloon();
        }

        private Font TitleFont => _titleFont ??= new Font(Font, FontStyle.Bold);

        private int TextLeft => Pad + (_icon != null ? IconSize + Pad : 0);

        public void Open()
        {
            int textWidth = Width_ - TextLeft - Pad;
            var flags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix;
            int height = Pad;
            if (_title.Length > 0) height += TextRenderer.MeasureText(_title, TitleFont, new Size(textWidth, int.MaxValue), flags).Height + 4;
            height += TextRenderer.MeasureText(_body, Font, new Size(textWidth, int.MaxValue), flags).Height + Pad;
            if (_icon != null) height = Math.Max(height, IconSize + 2 * Pad);

            var area = Screen.PrimaryScreen.WorkingArea;
            ClientSize = new Size(Width_, height);
            Location = new Point(area.Right - Width_ - Pad, area.Bottom - height - Pad);
            Show();
            _timer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            using (var border = new Pen(SystemColors.ControlDark)) g.DrawRectangle(border, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
            if (_icon != null) g.DrawIcon(_icon, new Rectangle(Pad, Pad, IconSize, IconSize));

            int x = TextLeft, y = Pad, width = ClientSize.Width - x - Pad;
            var flags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix;
            if (_title.Length > 0)
            {
                var titleSize = TextRenderer.MeasureText(_title, TitleFont, new Size(width, int.MaxValue), flags);
                TextRenderer.DrawText(g, _title, TitleFont, new Rectangle(x, y, width, titleSize.Height), ForeColor, flags);
                y += titleSize.Height + 4;
            }
            TextRenderer.DrawText(g, _body, Font, new Rectangle(x, y, width, ClientSize.Height - y - Pad), ForeColor, flags);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            _owner.BalloonClicked();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Dispose();
                _titleFont?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
