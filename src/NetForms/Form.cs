using System;
using System.ComponentModel;
using System.Drawing;
using NetForms.Platform;

namespace System.Windows.Forms;

/// <summary>
/// A top-level window. Owns one platform window (created on first Show), receives the
/// platform's paint and input callbacks and routes them into the control tree: hit-testing,
/// mouse capture, MouseEnter/Leave tracking, focus and keyboard dispatch all live here.
/// </summary>
[DefaultEvent("Load")]
public partial class Form : ContainerControl
{
    private static Form? s_activeForm;
    private static MouseButtons s_pressedButtons;
    private static Point s_lastMouseScreen;
    private static Keys s_modifiers;

    private IPlatformWindow? _window;
    private WindowHost? _host;
    private bool _loaded;
    private bool _syncingFromWindow;
    private bool _windowActive;
    private bool _closing;
    private bool _modal;
    private Form? _owner;
    private Form? _modalOwner;
    private FormWindowState _windowState = FormWindowState.Normal;
    private bool _maximizeBox = true;
    private bool _minimizeBox = true;
    private bool _topMost;
    private Icon? _icon;
    private Cursor? _appliedCursor;
    private CloseReason _closeReason = CloseReason.None;
    private DialogResult _dialogResult = DialogResult.None;
    private FormStartPosition _startPosition = FormStartPosition.WindowsDefaultLocation;
    private FormBorderStyle _borderStyle = FormBorderStyle.Sizable;
    private bool _showInTaskbar = true;
    private Point _windowLocation;

    private Control? _focused;
    private Control? _capture;
    private Control? _mouseOver;
    private bool _keyboardCues;

    public Form()
    {
        VisibleOwn = false;
        SetStyle(ControlStyles.Selectable, false);
    }

    protected override Size DefaultSize => new Size(300, 300);

    /// <summary>An MDI child stops being top-level: it becomes a control of its parent's client area.</summary>
    protected override bool GetTopLevel() => _topLevel && !IsMdiChild;

    // --- window properties ---------------------------------------------------------

    [Category("Appearance")]
    [Description("The text associated with the control.")]
    [Localizable(true)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override string Text
    {
        get => base.Text;
        set
        {
            base.Text = value;
            if (_window != null) _window.Title = value;
        }
    }

    [Category("Layout")]
    [Description("Determines the position of a form when it first appears.")]
    [DefaultValue(FormStartPosition.WindowsDefaultLocation)]
    [Localizable(true)]
    public FormStartPosition StartPosition
    {
        get => _startPosition;
        set => _startPosition = value;
    }

    [Category("Appearance")]
    [Description("Indicates the appearance and behavior of the border and title bar of the form.")]
    [DefaultValue(FormBorderStyle.Sizable)]
    public FormBorderStyle FormBorderStyle
    {
        get => _borderStyle;
        set
        {
            if (_borderStyle == value) return;
            _borderStyle = value;
            if (_window != null)
            {
                _window.Resizable = IsSizable;
                _window.Decorations = value != FormBorderStyle.None;
            }
        }
    }

    private bool IsSizable => _borderStyle is FormBorderStyle.Sizable or FormBorderStyle.SizableToolWindow;

    [Category("Window Style")]
    [Description("Determines whether the form appears in the Windows Taskbar.")]
    [DefaultValue(true)]
    public bool ShowInTaskbar
    {
        get => _showInTaskbar;
        set
        {
            _showInTaskbar = value;
            if (_window != null) _window.ShowInTaskbar = value;
        }
    }

    [Category("Window Style")]
    [Description("Determines whether a form has a maximize box in the upper-right corner of its caption bar.")]
    [DefaultValue(true)]
    public bool MaximizeBox
    {
        get => _maximizeBox;
        set
        {
            _maximizeBox = value;
            if (_window != null) _window.CanMaximize = value;
        }
    }

    [Category("Window Style")]
    [Description("Determines whether a form has a minimize box in the upper-right corner of its caption bar.")]
    [DefaultValue(true)]
    public bool MinimizeBox
    {
        get => _minimizeBox;
        set
        {
            _minimizeBox = value;
            if (_window != null) _window.CanMinimize = value;
        }
    }

    [Category("Window Style")]
    [Description("Determines whether a form has a Control/System menu box.")]
    [DefaultValue(true)]
    public bool ControlBox { get; set; } = true;

    [Category("Window Style")]
    [Description("Indicates whether an icon is displayed in the title bar of the form.")]
    [DefaultValue(true)]
    public bool ShowIcon { get; set; } = true;

    [Category("Window Style")]
    [Description("Indicates whether the form always appears above all other forms that do not have this property set to true.")]
    [DefaultValue(false)]
    public bool TopMost
    {
        get => _topMost;
        set
        {
            _topMost = value;
            if (_window != null) _window.TopMost = value;
        }
    }

    [Description("Determines whether keyboard events for controls on the form are registered with the form.")]
    [DefaultValue(false)]
    public bool KeyPreview { get; set; }

    private AutoSizeMode _autoSizeMode = AutoSizeMode.GrowOnly;

    [Category("Layout")]
    [Description("Specifies the mode by which the user interface element automatically resizes itself.")]
    [DefaultValue(AutoSizeMode.GrowOnly)]
    [Localizable(true)]
    public AutoSizeMode AutoSizeMode
    {
        get => _autoSizeMode;
        set
        {
            if (_autoSizeMode == value) return;
            _autoSizeMode = value;
            if (AutoSize) AdjustSizeToPreferred();
        }
    }

    internal override AutoSizeMode AutoSizeModeCore => _autoSizeMode;

    [Category("Layout")]
    [Description("Determines the initial visual state of the form.")]
    [DefaultValue(FormWindowState.Normal)]
    public FormWindowState WindowState
    {
        get => _windowState;
        set
        {
            if (_windowState == value) return;
            _windowState = value;
            if (_window != null)
            {
                _window.State = value switch
                {
                    FormWindowState.Minimized => PlatformWindowState.Minimized,
                    FormWindowState.Maximized => PlatformWindowState.Maximized,
                    _ => PlatformWindowState.Normal,
                };
            }
        }
    }

    [Category("Window Style")]
    [Description("The owner of this form.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Form? Owner
    {
        get => _owner;
        set
        {
            if (_owner == value) return;
            if (value == this) throw new ArgumentException("A form cannot own itself.");
            _owner?._ownedForms.Remove(this);
            _owner = value;
            value?.AddOwnedForm(this);
            if (_window != null) _window.Owner = value?._window;
        }
    }

    [Category("Window Style")]
    [Description("Indicates the icon for a form. This icon is displayed in the form's system menu box and when the form is minimized.")]
    [Localizable(true)]
    public Icon? Icon
    {
        get => _icon;
        set => _icon = value;
    }

    internal bool ShouldSerializeIcon() => _icon != null;

    /// <summary>A form's position is written only once it has one (StartPosition decides otherwise).</summary>
    internal override bool ShouldSerializeLocation() => Left != 0 || Top != 0;

    /// <summary>Override to true for windows (drop-downs, tooltips) that must appear without stealing the focus.</summary>
    protected virtual bool ShowWithoutActivation => false;

    /// <summary>Raised on every mouse-down that reaches this form, before routing; popups use it to close themselves.</summary>
    internal event EventHandler<MouseEventArgs>? MouseDownAnywhere;

    /// <summary>Raised when the form's window moves or resizes; popups follow or close.</summary>
    internal event EventHandler? WindowMovedOrResized;

    [Description("The accept button of the form. If this is set, the button is 'clicked' whenever the user presses the 'ENTER' key.")]
    [DefaultValue(null)]
    public IButtonControl? AcceptButton { get; set; }

    [Description("The cancel button of the form. If this property is set, the button is 'clicked' whenever the user presses the 'ESC' key.")]
    [DefaultValue(null)]
    public IButtonControl? CancelButton { get; set; }

    /// <summary>The form's menu bar. Set automatically by the first MenuStrip added to the form, as WinForms does.</summary>
    [Category("Window Style")]
    [Description("Specifies the primary MenuStrip for the Form. This property is used for keyboard activation and automatic merging in MDI.")]
    [DefaultValue(null)]
    public MenuStrip? MainMenuStrip { get; set; }

    [Category("Window Style")]
    [Description("The opacity percentage of the control.")]
    [DefaultValue(1D)]
    public double Opacity { get; set; } = 1.0;

    [Category("Behavior")]
    [Description("The value this form will return if displayed as a dialog box.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DialogResult DialogResult
    {
        get => _dialogResult;
        set
        {
            _dialogResult = value;
            if (_modal && value != DialogResult.None && !_closing) Close();
        }
    }

    [Category("Window Style")]
    [Description("Indicates if this form is currently being displayed as a modal dialog box.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Modal => _modal;

    public static Form? ActiveForm => s_activeForm;

    /// <summary>The window's screen position (client origin), as far as the platform reports it.</summary>
    internal Point WindowLocation => _windowLocation;

    [Category("Layout")]
    [Description("The location of this form in desktop coordinates.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Point DesktopLocation
    {
        get => _windowLocation;
        set => Location = value;
    }

    [Category("Layout")]
    [Description("The bounds of this form in desktop coordinates.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Rectangle DesktopBounds => new Rectangle(_windowLocation, Size);

    internal bool IsWindowCreated => _window != null;

    /// <summary>The platform window behind this form; the native dialogs need it as their owner.</summary>
    internal IPlatformWindow? PlatformWindow => _window;

    internal bool IsWindowActive => _windowActive;

    protected override void SetClientSizeCore(int x, int y)
    {
        // The Avalonia window is sized by its client area, so Size == ClientSize here
        // (a documented Ф0 difference: WinForms' Size includes the frame).
        base.SetClientSizeCore(x, y);
    }

    protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
    {
        base.SetBoundsCore(x, y, width, height, specified);
        if (_window != null && !_syncingFromWindow)
        {
            if ((specified & BoundsSpecified.Size) != 0) _window.ClientSize = new Size(Width, Height);
            if ((specified & BoundsSpecified.Location) != 0)
            {
                _windowLocation = new Point(x, y);
                _window.Location = _windowLocation;
            }
        }
        else if (_window == null && (specified & BoundsSpecified.Location) != 0)
        {
            _windowLocation = new Point(x, y);
        }
    }

    [Category("Layout")]
    [Description("The minimum size the form can be resized to.")]
    [Localizable(true)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override Size MinimumSize
    {
        get => base.MinimumSize;
        set
        {
            if (base.MinimumSize == value) return;
            base.MinimumSize = value;
            if (_window != null) _window.MinimumClientSize = value;
            OnMinimumSizeChanged(EventArgs.Empty);
        }
    }

    [Category("Layout")]
    [Description("The maximum size the form can be resized to.")]
    [DefaultValue(typeof(Size), "0, 0")]
    [Localizable(true)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override Size MaximumSize
    {
        get => base.MaximumSize;
        set
        {
            if (base.MaximumSize == value) return;
            base.MaximumSize = value;
            if (_window != null) _window.MaximumClientSize = value;
            OnMaximumSizeChanged(EventArgs.Empty);
        }
    }

    // --- show / close --------------------------------------------------------------

    protected override void SetVisibleCore(bool value)
    {
        if (VisibleOwn == value) return;

        // An MDI child, or a form made a child control (TopLevel = false), has no window of its own:
        // showing it means appearing inside the parent.
        if (IsEmbedded)
        {
            VisibleOwn = value;
            if (value && !_loaded)
            {
                _loaded = true;
                PerformLayout();
                OnLoad(EventArgs.Empty);
            }
            OnVisibleChanged(EventArgs.Empty);
            if (value)
            {
                MdiParent?.ActivateMdiChild(this);
                OnShown(EventArgs.Empty);
            }
            Parent?.PerformLayout(this, nameof(Visible));
            Parent?.Invalidate();
            return;
        }

        if (value)
        {
            CreateWindowIfNeeded();
            VisibleOwn = true;
            OnVisibleChanged(EventArgs.Empty);
            if (!_loaded)
            {
                _loaded = true;
                PerformLayout();
                OnLoad(EventArgs.Empty);
            }
            if (_modal) _window!.ShowModal(_modalOwner?._window);
            else _window!.Show();
            InvalidateWindow(ClientRectangle);
        }
        else
        {
            VisibleOwn = false;
            _window?.Hide();
            OnVisibleChanged(EventArgs.Empty);
        }
    }

    internal void CreateWindowIfNeeded()
    {
        if (_window != null) return;
        var platform = Application.Platform;
        platform.Initialize();
        _host = new WindowHost(this);
        _window = platform.CreateWindow(_host);
        _window.Title = Text;
        _window.Resizable = IsSizable;
        _window.ShowInTaskbar = _showInTaskbar;
        _window.MinimumClientSize = MinimumSize;
        _window.MaximumClientSize = MaximumSize;
        _window.StartPosition = _startPosition switch
        {
            FormStartPosition.CenterScreen => WindowStartPosition.CenterScreen,
            FormStartPosition.CenterParent => WindowStartPosition.CenterOwner,
            FormStartPosition.Manual => WindowStartPosition.Manual,
            _ => WindowStartPosition.WindowsDefault,
        };
        _window.Decorations = _borderStyle != FormBorderStyle.None;
        _window.ShowActivated = !ShowWithoutActivation;
        _window.CanMaximize = _maximizeBox;
        _window.CanMinimize = _minimizeBox;
        _window.TopMost = _topMost;
        _window.Owner = _owner?._window;
        if (_startPosition == FormStartPosition.Manual) _window.Location = _windowLocation;
        _window.ClientSize = ClientSize;
        if (_windowState != FormWindowState.Normal)
        {
            _window.State = _windowState == FormWindowState.Minimized ? PlatformWindowState.Minimized : PlatformWindowState.Maximized;
            if (_windowState == FormWindowState.Maximized) ApplyMaximizedBounds();
        }
        Application.RegisterForm(this);
        RaiseHandleCreatedRecursive();
    }

    /// <summary>
    /// A maximized window is sized by the window manager. WinForms creates the handle already
    /// maximized, so <see cref="Load"/> sees the real size; we have to fill it in ourselves, or
    /// code that reads Width/Height in Load gets the design-time size instead. The platform
    /// corrects any difference as soon as it reports the window's own bounds.
    /// </summary>
    private void ApplyMaximizedBounds()
    {
        var area = Screen.FromPoint(_windowLocation).WorkingArea;
        if (area.Width <= 0 || area.Height <= 0) return;
        _syncingFromWindow = true;
        try
        {
            SetBounds(area.X, area.Y, area.Width, area.Height, BoundsSpecified.All);
        }
        finally
        {
            _syncingFromWindow = false;
        }
    }

    public void Close()
    {
        if (IsDisposed || _closing) return;
        _closeReason = CloseReason.UserClosing;

        if (!_topLevel && !IsMdiChild)
        {
            // A form made a child control closes as a control: gone from its parent, then disposed.
            if (RaiseClosing()) return;
            VisibleOwn = false;
            var host = Parent;
            host?.Controls.Remove(this);
            Application.UnregisterForm(this);
            RaiseFormClosed(new FormClosedEventArgs(CloseReason.UserClosing));
            _closing = false;
            _closeReason = CloseReason.None;
            host?.Invalidate();
            Dispose();
            return;
        }

        if (IsMdiChild)
        {
            if (RaiseClosing()) return;
            var parent = _mdiParent;
            var reason = CloseReason.UserClosing;
            VisibleOwn = false;
            parent?.MdiClientArea?.Controls.Remove(this);
            if (parent != null && ReferenceEquals(parent.ActiveMdiChild, this))
            {
                var remaining = parent.MdiChildren;
                parent.ActivateMdiChild(remaining.Length > 0 ? remaining[0] : null);
            }
            _mdiParent = null;
            Application.UnregisterForm(this);
            RaiseFormClosed(new FormClosedEventArgs(reason));
            _closing = false;
            _closeReason = CloseReason.None;
            parent?.Invalidate();
            Dispose();
            return;
        }

        if (_window != null)
        {
            _window.Close();
        }
        else
        {
            // Never shown: run the closing sequence ourselves.
            if (RaiseClosing()) return;
            RaiseClosed();
        }
    }

    internal void CloseFromApplication()
    {
        _closeReason = CloseReason.ApplicationExitCall;
        if (_window != null) _window.Close();
        else { if (!RaiseClosing()) RaiseClosed(); }
    }

    /// <summary>Returns true when the close was cancelled.</summary>
    private bool RaiseClosing()
    {
        if (_closing) return false;
        _closing = true;
        var reason = _closeReason == CloseReason.None ? CloseReason.UserClosing : _closeReason;
        var e = new FormClosingEventArgs(reason, false);
        RaiseFormClosing(e);
        if (e.Cancel)
        {
            _closing = false;
            _closeReason = CloseReason.None;
            return true;
        }
        return false;
    }

    private void RaiseClosed()
    {
        var reason = _closeReason == CloseReason.None ? CloseReason.UserClosing : _closeReason;
        VisibleOwn = false;
        _window?.Dispose();
        _window = null;
        _host = null;
        _windowActive = false;
        if (s_activeForm == this) s_activeForm = null;
        Application.UnregisterForm(this);
        RaiseFormClosed(new FormClosedEventArgs(reason));
        OnHandleDestroyed(EventArgs.Empty); // after FormClosed, as the window goes after WM_CLOSE
        _closing = false;
        _closeReason = CloseReason.None;
        if (_modal)
        {
            // A modal form survives its close (the caller reads DialogResult and may show it again).
            _modal = false;
            Application.Platform.ExitMessageLoop();
        }
        else
        {
            Dispose();
        }
    }

    public DialogResult ShowDialog() => ShowDialog(null);

    public DialogResult ShowDialog(IWin32Window? owner)
    {
        if (VisibleOwn) throw new InvalidOperationException("Form is already visible.");
        if (owner == this) throw new ArgumentException("A form cannot own its own dialog.", nameof(owner));
        _dialogResult = DialogResult.None;
        _modal = true;
        _modalOwner = owner as Form ?? (owner as Control)?.FindForm() ?? ActiveForm;
        if (_modalOwner == this) _modalOwner = null;
        Show();
        if (_modal && !IsDisposed)
        {
            Application.Platform.RunMessageLoop();
        }
        _modalOwner?.Activate();
        _modalOwner = null;
        return _dialogResult;
    }

    protected void CenterToParent()
    {
        var parent = _modalOwner ?? _owner;
        var area = parent != null ? parent.DesktopBounds : Screen.PrimaryScreen.WorkingArea;
        Location = new Point(area.X + (area.Width - Width) / 2, area.Y + (area.Height - Height) / 2);
    }

    protected void CenterToScreen()
    {
        var area = Screen.PrimaryScreen.WorkingArea;
        Location = new Point(area.X + (area.Width - Width) / 2, area.Y + (area.Height - Height) / 2);
    }

    public void Activate()
    {
        // An MDI child is activated inside its parent, not by the window manager.
        if (IsMdiChild)
        {
            MdiParent?.ActivateMdiChild(this);
            return;
        }
        _window?.Activate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _window != null)
        {
            var w = _window;
            _window = null;
            _host = null;
            Application.UnregisterForm(this);
            w.Dispose();
        }
        base.Dispose(disposing);
    }

    // --- painting and invalidation -------------------------------------------------

    internal void InvalidateWindow(Rectangle rect) => _window?.Invalidate(rect);

    internal void UpdateWindow() { }

    private void PaintWindow(SkiaSharp.SKCanvas canvas, Size clientSize, Rectangle clip)
    {
        using var g = Graphics.FromCanvas(canvas);
        PaintTree(g, clip);
    }

    // --- mouse routing -------------------------------------------------------------

    internal static MouseButtons PressedButtons => s_pressedButtons;
    internal static Point LastMouseScreenPosition => s_lastMouseScreen;
    internal static Keys CurrentModifiers => s_modifiers;

    internal Control? CaptureControl => _capture;

    internal void SetCapture(Control? control)
    {
        if (_capture == control) return;
        var old = _capture;
        _capture = control;
        _window?.SetCapture(control != null);
        old?.RaiseCaptureChanged();
    }

    private static MouseButtons ToButtons(MouseButton b)
    {
        MouseButtons r = MouseButtons.None;
        if ((b & MouseButton.Left) != 0) r |= MouseButtons.Left;
        if ((b & MouseButton.Right) != 0) r |= MouseButtons.Right;
        if ((b & MouseButton.Middle) != 0) r |= MouseButtons.Middle;
        if ((b & MouseButton.XButton1) != 0) r |= MouseButtons.XButton1;
        if ((b & MouseButton.XButton2) != 0) r |= MouseButtons.XButton2;
        return r;
    }

    private static Keys ToModifiers(InputModifiers m)
    {
        Keys k = Keys.None;
        if ((m & InputModifiers.Shift) != 0) k |= Keys.Shift;
        if ((m & InputModifiers.Control) != 0) k |= Keys.Control;
        if ((m & InputModifiers.Alt) != 0) k |= Keys.Alt;
        return k;
    }

    private Control MouseTarget(Point formPoint) => _capture ?? HitTest(formPoint);

    private void UpdateMouseOver(Control? over)
    {
        if (_mouseOver != over)
        {
            _mouseOver?.RaiseMouseLeave();
            _mouseOver = over;
            over?.RaiseMouseEnter();
        }
        UpdateCursor();
    }

    /// <summary>Cursor.Current: shows <paramref name="cursor"/> until the next mouse move picks the control's again.</summary>
    internal void ShowCursorNow(Cursor cursor)
    {
        _appliedCursor = cursor;
        _window?.SetCursor(cursor.Name);
    }

    /// <summary>Push the cursor of the control under the pointer (or the capturing one) to the window.</summary>
    internal void UpdateCursor()
    {
        var target = _capture ?? _mouseOver ?? this;
        var cursor = Application.UseWaitCursor || target.UseWaitCursor ? Cursors.WaitCursor : target.Cursor;
        if (ReferenceEquals(cursor, _appliedCursor)) return;
        _appliedCursor = cursor;
        _window?.SetCursor(cursor.Name);
    }

    private void HandleMouseMove(Point p, MouseButton pressed, InputModifiers mods)
    {
        s_modifiers = ToModifiers(mods);
        s_pressedButtons = ToButtons(pressed);
        s_lastMouseScreen = new Point(_windowLocation.X + p.X, _windowLocation.Y + p.Y);
        var target = MouseTarget(p);
        var local = target.PointFromForm(p);
        UpdateMouseOver(_capture != null ? (target.ClientRectangle.Contains(local) ? target : null) : target);
        target.RaiseMouseMove(new MouseEventArgs(s_pressedButtons, 0, local.X, local.Y, 0));
    }

    private void HandleMouseDown(MouseButton button, Point p, int clicks, InputModifiers mods)
    {
        s_modifiers = ToModifiers(mods);
        var b = ToButtons(button);
        s_pressedButtons |= b;
        MouseDownAnywhere?.Invoke(this, new MouseEventArgs(b, clicks, p.X, p.Y, 0));
        var target = MouseTarget(p);

        // Clicking anywhere in an MDI child - its frame or a control on it - activates it.
        if (_capture == null && MdiChildOf(target) is { } mdiChild) mdiChild.MdiParent?.ActivateMdiChild(mdiChild);

        var local = target.PointFromForm(p);
        UpdateMouseOver(target);
        target.RaiseMouseDown(new MouseEventArgs(b, clicks, local.X, local.Y, 0));
    }

    private void HandleMouseUp(MouseButton button, Point p, InputModifiers mods)
    {
        s_modifiers = ToModifiers(mods);
        var b = ToButtons(button);
        s_pressedButtons &= ~b;
        var target = MouseTarget(p);
        var local = target.PointFromForm(p);
        target.RaiseMouseUp(new MouseEventArgs(b, 1, local.X, local.Y, 0));
        // Capture is normally released now; re-evaluate what the pointer is over.
        if (_capture == null && !IsDisposed) UpdateMouseOver(HitTest(p));
    }

    private void HandleMouseWheel(Point p, int delta, InputModifiers mods)
    {
        var target = MouseTarget(p);
        var local = target.PointFromForm(p);
        target.RaiseMouseWheel(new MouseEventArgs(s_pressedButtons, 0, local.X, local.Y, delta));
    }

    private void HandleMouseLeave()
    {
        if (_capture == null) UpdateMouseOver(null);
    }

    /// <summary>A child left the tree: drop any references the router holds to it.</summary>
    internal void ChildRemoved(Control control)
    {
        if (_capture != null && (_capture == control || control.Contains(_capture))) SetCapture(null);
        if (_mouseOver != null && (_mouseOver == control || control.Contains(_mouseOver))) _mouseOver = null;
        if (_focused != null && (_focused == control || control.Contains(_focused))) _focused = null;
        if (ActiveControl != null && (ActiveControl == control || control.Contains(ActiveControl))) SetActiveControlInternal(null);
    }

    // --- focus ---------------------------------------------------------------------

    internal Control? FocusedControl => _focused;

    internal bool KeyboardFocusCuesShown => _keyboardCues;

    internal void ShowFocusCuesFromKeyboard()
    {
        if (_keyboardCues) return;
        _keyboardCues = true;
        _focused?.Invalidate();
    }

    /// <summary>
    /// Move the focus. Select()/ActiveControl/Tab go in WinForms' ContainerControl order: Leave(old),
    /// Validating/Validated(old), Enter(new), LostFocus(old), GotFocus(new) - UpdateFocusedControl runs
    /// before the window focus moves. <paramref name="nativeFirst"/> is Control.Focus() (and a click, which
    /// focuses through it): there Win32 moves the focus first, so LostFocus(old) comes before Leave
    /// (checked against WinForms, CompatScenarios focus/b-focus). A cancelled Validating keeps the focus.
    /// </summary>
    internal bool SetFocusedControl(Control control, bool nativeFirst = false)
    {
        if (_focused == control) return true;
        var old = _focused;
        if (nativeFirst) old?.RaiseLostFocus();
        if (old != null && old != this)
        {
            old.RaiseLeave();
            var autoValidate = ContainerControl.GetAutoValidateForControl(old);
            if (old.CausesValidation && control.CausesValidation && autoValidate != AutoValidate.Disable)
            {
                var e = new CancelEventArgs();
                old.RaiseValidating(e);
                if (e.Cancel && autoValidate == AutoValidate.EnablePreventFocusChange)
                {
                    old.RaiseEnter();
                    if (nativeFirst) old.RaiseGotFocus();   // the focus goes back where it was
                    return false;
                }
                if (!e.Cancel) old.RaiseValidated();
            }
        }
        control.RaiseEnter();
        _focused = control;
        SetActiveControlInternal(control == this ? null : control);
        if (!nativeFirst) old?.RaiseLostFocus();
        control.RaiseGotFocus();
        old?.Invalidate();
        control.Invalidate();
        return true;
    }

    private void SelectInitialControl()
    {
        if (_focused != null) return;
        if (!SelectNextControl(null, true, true, true, false))
        {
            _focused = this;
        }
    }

    // --- keyboard routing ----------------------------------------------------------

    private bool HandleKeyDown(int virtualKey, InputModifiers mods)
    {
        s_modifiers = ToModifiers(mods);
        var keyData = (Keys)virtualKey | s_modifiers;
        var target = _focused ?? this;
        if (FilterKeyMessage(Message.WM_KEYDOWN, keyData)) return true;

        // PreviewKeyDown comes first (PreProcessControlMessage); a key it marks as input skips the
        // menus and dialog keys and goes straight to KeyDown.
        bool input = target.RaisePreviewKeyDown(keyData);
        if (!input)
        {
            // Menu shortcuts and the open menu see the key before anything else, as in Win32.
            if (ToolStripManager.ProcessCmdKey(keyData, this)) return true;
            if (ProcessMenuMnemonic(keyData)) return true;

            if (target.ProcessDialogKeyInternal(keyData)) return true;

            // F1 unhandled by now is WM_HELP: HelpRequested on the focused control, bubbling up.
            if (keyData == Keys.F1)
            {
                var help = new HelpEventArgs(MousePosition);
                target.RaiseHelpRequested(help);
                if (help.Handled) return true;
            }
        }

        if (KeyPreview && target != this)
        {
            var e = new KeyEventArgs(keyData);
            OnKeyDown(e);
            if (e.Handled) return true;
        }
        return target.RaiseKeyDown(keyData);
    }

    /// <summary>Application message filters see keyboard messages first (IMessageFilter, decision 115).</summary>
    private static bool FilterKeyMessage(int msg, Keys keyData)
    {
        if (!Application.HasMessageFilters) return false;
        var m = Message.Create(IntPtr.Zero, msg, (IntPtr)(int)(keyData & Keys.KeyCode), IntPtr.Zero);
        return Application.FilterMessage(ref m);
    }

    private bool HandleKeyUp(int virtualKey, InputModifiers mods)
    {
        s_modifiers = ToModifiers(mods);
        var keyData = (Keys)virtualKey | s_modifiers;
        if (FilterKeyMessage(Message.WM_KEYUP, keyData)) return true;
        var target = _focused ?? this;
        if (KeyPreview && target != this)
        {
            var e = new KeyEventArgs(keyData);
            OnKeyUp(e);
            if (e.Handled) return true;
        }
        return target.RaiseKeyUp(keyData);
    }

    private void HandleTextInput(string text)
    {
        var target = _focused ?? this;
        foreach (var ch in text)
        {
            if (KeyPreview && target != this)
            {
                var e = new KeyPressEventArgs(ch);
                OnKeyPress(e);
                if (e.Handled) continue;
            }
            target.RaiseKeyPress(ch);
        }
    }

    /// <summary>Alt+letter opens the matching top-level menu.</summary>
    private bool ProcessMenuMnemonic(Keys keyData)
    {
        if ((keyData & Keys.Alt) == 0 || MainMenuStrip == null) return false;
        var code = keyData & Keys.KeyCode;
        if (code is < Keys.A or > Keys.Z) return false;

        char ch = (char)('A' + (code - Keys.A));
        foreach (var item in MainMenuStrip.Items)
        {
            if (item is not ToolStripDropDownItem { Enabled: true, Available: true } menu) continue;
            if (!IsMnemonic(ch, menu.Text)) continue;
            MainMenuStrip.SelectItem(menu);
            menu.ShowDropDown(fromKeyboard: true);
            return true;
        }
        return false;
    }

    /// <summary>True when <paramref name="text"/> marks <paramref name="charCode"/> with an ampersand.</summary>
    public static bool IsMnemonic(char charCode, string? text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        for (int i = 0; i < text.Length - 1; i++)
        {
            if (text[i] != '&') continue;
            if (text[i + 1] == '&') { i++; continue; }
            return char.ToUpperInvariant(text[i + 1]) == char.ToUpperInvariant(charCode);
        }
        return false;
    }

    protected override void OnControlAdded(ControlEventArgs e)
    {
        // The first MenuStrip on a form becomes its menu bar, as the designer expects.
        if (MainMenuStrip == null && e.Control is MenuStrip menuStrip) MainMenuStrip = menuStrip;
        base.OnControlAdded(e);
    }

    protected override bool ProcessDialogKey(Keys keyData)
    {
        if ((keyData & (Keys.Alt | Keys.Control)) == Keys.None)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Return:
                    {
                        var button = _focused as IButtonControl ?? AcceptButton;
                        if (button != null)
                        {
                            button.PerformClick();
                            return true;
                        }
                        break;
                    }
                case Keys.Escape:
                    if (CancelButton != null)
                    {
                        CancelButton.PerformClick();
                        return true;
                    }
                    break;
            }
        }
        return base.ProcessDialogKey(keyData);
    }

    // --- events --------------------------------------------------------------------

    [Category("Behavior")]
    [Description("Occurs whenever the user loads the form.")]
    public event EventHandler? Load;

    [Category("Behavior")]
    [Description("Occurs whenever the form is first shown.")]
    public event EventHandler? Shown;

    [Category("Behavior")]
    [Description("Occurs whenever the user closes the form, before the form has been closed and specifies the close reason.")]
    public event FormClosingEventHandler? FormClosing;

    [Category("Behavior")]
    [Description("Occurs whenever the user closes the form, after the form has been closed and specifies the close reason.")]
    public event FormClosedEventHandler? FormClosed;

    [Category("Focus")]
    [Description("Occurs whenever the form is activated.")]
    public event EventHandler? Activated;

    [Category("Focus")]
    [Description("Occurs whenever the form is deactivated.")]
    public event EventHandler? Deactivate;

    [Category("Action")]
    [Description("Occurs when the form enters the sizing modal loop.")]
    public event EventHandler? ResizeBegin;

    [Category("Action")]
    [Description("Occurs when the form exits the sizing modal loop.")]
    public event EventHandler? ResizeEnd;

    protected virtual void OnLoad(EventArgs e) => Load?.Invoke(this, e);
    protected virtual void OnShown(EventArgs e) => Shown?.Invoke(this, e);
    protected virtual void OnFormClosing(FormClosingEventArgs e) => FormClosing?.Invoke(this, e);
    protected virtual void OnFormClosed(FormClosedEventArgs e) => FormClosed?.Invoke(this, e);
    protected virtual void OnActivated(EventArgs e) => Activated?.Invoke(this, e);
    protected virtual void OnDeactivate(EventArgs e) => Deactivate?.Invoke(this, e);
    protected virtual void OnResizeBegin(EventArgs e) => ResizeBegin?.Invoke(this, e);
    protected virtual void OnResizeEnd(EventArgs e) => ResizeEnd?.Invoke(this, e);

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Invalidate();
        WindowMovedOrResized?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMove(EventArgs e)
    {
        base.OnMove(e);
        WindowMovedOrResized?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        using var brush = new SolidBrush(BackColor);
        pevent.Graphics.FillRectangle(brush, pevent.ClipRectangle);
    }

    // --- the platform's view of this form -----------------------------------------

    /// <summary>Adapter between the platform window and the form; keeps IWindowHost off Form's public surface.</summary>
    private sealed class WindowHost : IWindowHost
    {
        private readonly Form _form;

        public WindowHost(Form form) => _form = form;

        public void Paint(SkiaSharp.SKCanvas canvas, Size clientSize, Rectangle clip) => Application.Dispatch(() => _form.PaintWindow(canvas, clientSize, clip));

        public void MouseDown(MouseButton button, Point position, int clicks, InputModifiers modifiers) => Application.Dispatch(() => _form.HandleMouseDown(button, position, clicks, modifiers));

        public void MouseUp(MouseButton button, Point position, InputModifiers modifiers) => Application.Dispatch(() => _form.HandleMouseUp(button, position, modifiers));

        public void MouseMove(Point position, MouseButton pressedButtons, InputModifiers modifiers) => Application.Dispatch(() => _form.HandleMouseMove(position, pressedButtons, modifiers));

        public void MouseWheel(Point position, int delta, InputModifiers modifiers) => Application.Dispatch(() => _form.HandleMouseWheel(position, delta, modifiers));

        public void MouseLeave() => Application.Dispatch(_form.HandleMouseLeave);

        public bool KeyDown(int virtualKey, InputModifiers modifiers) => Application.Dispatch(() => _form.HandleKeyDown(virtualKey, modifiers), true);

        public bool KeyUp(int virtualKey, InputModifiers modifiers) => Application.Dispatch(() => _form.HandleKeyUp(virtualKey, modifiers), true);

        public void TextInput(string text) => Application.Dispatch(() => _form.HandleTextInput(text));

        public void Resized(Size clientSize)
        {
            if (_form.ClientSize == clientSize) return;
            _form._syncingFromWindow = true;
            try
            {
                Application.Dispatch(() => _form.SetBounds(0, 0, clientSize.Width, clientSize.Height, BoundsSpecified.Size));
            }
            finally
            {
                _form._syncingFromWindow = false;
            }
        }

        public void Moved(Point location)
        {
            _form._windowLocation = location;
            _form._syncingFromWindow = true;
            try
            {
                Application.Dispatch(() => _form.SetBounds(location.X, location.Y, 0, 0, BoundsSpecified.Location));
            }
            finally
            {
                _form._syncingFromWindow = false;
            }
        }

        public void Activated()
        {
            _form._windowActive = true;
            s_activeForm = _form;
            Application.Dispatch(() =>
            {
                _form.SelectInitialControl();
                _form.OnActivated(EventArgs.Empty);
            });
            _form._focused?.Invalidate();
        }

        public void Deactivated()
        {
            _form._windowActive = false;
            if (s_activeForm == _form) s_activeForm = null;
            Application.Dispatch(() => _form.OnDeactivate(EventArgs.Empty));
            _form._focused?.Invalidate();
        }

        public void StateChanged(PlatformWindowState state)
        {
            if (_form._windowState == FormWindowState.Normal && state != PlatformWindowState.Normal) _form.RememberRestoreBounds();
            _form._windowState = state switch
            {
                PlatformWindowState.Minimized => FormWindowState.Minimized,
                PlatformWindowState.Maximized => FormWindowState.Maximized,
                _ => FormWindowState.Normal,
            };
        }

        public bool Closing(bool userRequested)
        {
            if (userRequested && _form._closeReason == CloseReason.None) _form._closeReason = CloseReason.UserClosing;
            return Application.Dispatch(_form.RaiseClosing, false);
        }

        public void Closed() => Application.Dispatch(_form.RaiseClosed);

        public void Shown()
        {
            Application.Dispatch(() => _form.OnShown(EventArgs.Empty));
        }
    }
}

/// <summary>WinForms' handle-bearing window interface; kept for signature compatibility (ShowDialog(IWin32Window)).</summary>
public interface IWin32Window
{
    IntPtr Handle { get; }
}
