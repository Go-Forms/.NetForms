using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;

namespace System.Windows.Forms;

public enum ToolStripDropDownDirection
{
    AboveLeft = 0,
    AboveRight = 1,
    BelowLeft = 2,
    BelowRight = 3,
    Left = 4,
    Right = 5,
    Default = 6,
}

public enum ToolStripDropDownCloseReason
{
    AppFocusChange = 0,
    AppClicked = 1,
    ItemClicked = 2,
    Keyboard = 3,
    CloseCalled = 4,
}

public delegate void ToolStripDropDownClosingEventHandler(object? sender, ToolStripDropDownClosingEventArgs e);
public delegate void ToolStripDropDownClosedEventHandler(object? sender, ToolStripDropDownClosedEventArgs e);

public class ToolStripDropDownClosingEventArgs : CancelEventArgs
{
    public ToolStripDropDownClosingEventArgs(ToolStripDropDownCloseReason reason) => CloseReason = reason;

    public ToolStripDropDownCloseReason CloseReason { get; }
}

public class ToolStripDropDownClosedEventArgs : EventArgs
{
    public ToolStripDropDownClosedEventArgs(ToolStripDropDownCloseReason reason) => CloseReason = reason;

    public ToolStripDropDownCloseReason CloseReason { get; }
}

/// <summary>
/// A strip that lives in its own borderless, non-activating top-level window, so it can extend past
/// the edges of its form - what Win32 gives a menu for free. The window is a <see cref="PopupForm"/>;
/// this control fills it (without docking: its Dock stays None, as in WinForms) and keeps the whole
/// ToolStrip machinery (items, layout, renderer).
/// </summary>
public class ToolStripDropDown : ToolStrip
{
    // Members WinForms hides from the designer on this control (Browsable(false)); checked against the real
    // WinForms by AttributeDiffTests.

    [Browsable(false)]
    public new event EventHandler? BindingContextChanged
    {
        add => base.BindingContextChanged += value;
        remove => base.BindingContextChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? BackgroundImageChanged
    {
        add => base.BackgroundImageChanged += value;
        remove => base.BackgroundImageChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? BackgroundImageLayoutChanged
    {
        add => base.BackgroundImageLayoutChanged += value;
        remove => base.BackgroundImageLayoutChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? CausesValidationChanged
    {
        add => base.CausesValidationChanged += value;
        remove => base.CausesValidationChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event UICuesEventHandler? ChangeUICues
    {
        add => base.ChangeUICues += value;
        remove => base.ChangeUICues -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? ContextMenuStripChanged
    {
        add => base.ContextMenuStripChanged += value;
        remove => base.ContextMenuStripChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event GiveFeedbackEventHandler? GiveFeedback
    {
        add => base.GiveFeedback += value;
        remove => base.GiveFeedback -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event HelpEventHandler? HelpRequested
    {
        add => base.HelpRequested += value;
        remove => base.HelpRequested -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? ImeModeChanged
    {
        add => base.ImeModeChanged += value;
        remove => base.ImeModeChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? RegionChanged
    {
        add => base.RegionChanged += value;
        remove => base.RegionChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? StyleChanged
    {
        add => base.StyleChanged += value;
        remove => base.StyleChanged -= value;
    }

    private readonly DropDownHost _host;
    private ToolStripItem? _ownerItem;
    private bool _autoClose = true;
    private bool _closing;

    public ToolStripDropDown()
    {
        GripStyle = ToolStripGripStyle.Hidden;
        CanOverflow = false;
        LayoutStyle = ToolStripLayoutStyle.Flow;
        // The drop-down fills its window, but not through Dock: in WinForms the drop-down IS the
        // window and its Dock stays None, which is what the designer must read (and not write).
        _host = new DropDownHost(this);
        _host.Controls.Add(this);
    }

    protected override Size DefaultSize => new Size(100, 25);

    protected override DockStyle DefaultDock => DockStyle.None;

    protected override Padding DefaultPadding => new Padding(1);

    [Category("Appearance")]
    [Description("Specifies the background color of the ToolStrip.")]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override Color BackColor
    {
        get => IsBackColorSet ? base.BackColor : Theme.MenuBackground;
        set => base.BackColor = value;
    }

    /// <summary>The item this drop-down belongs to, or null for a stand-alone context menu.</summary>
    [DefaultValue(null)]
    [Browsable(false)]
    public ToolStripItem? OwnerItem
    {
        get => _ownerItem;
        set => _ownerItem = value;
    }

    /// <summary>True when the drop-down was created by the framework rather than by user code.</summary>
    [Browsable(false)]
    public bool IsAutoGenerated { get; internal set; }

    [Category("Behavior")]
    [Description("Specifies whether the DropDown automatically closes through user action.")]
    [DefaultValue(true)]
    public bool AutoClose
    {
        get => _autoClose;
        set => _autoClose = value;
    }

    [Description("Indicates whether the opacity of the control can be adjusted.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool AllowTransparency { get; set; }

    [Category("Window Style")]
    [Description("The opacity percentage of the control.")]
    [DefaultValue(1D)]
    [Browsable(false)]
    public double Opacity { get; set; } = 1.0;

    public bool DropShadowEnabled { get; set; } = true;

    internal bool ShouldSerializeDropShadowEnabled() => !DropShadowEnabled;

    [Category("Behavior")]
    [Description("Indicates whether the user can use the TAB key to give focus to the control.")]
    [DefaultValue(false)]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new bool TabStop
    {
        get => base.TabStop;
        set => base.TabStop = value;
    }

    public Form? OwnerForm => _host.PopupOwnerForm;

    internal PopupForm Host => _host;

    // --- events --------------------------------------------------------------------------

    [Category("Action")]
    [Description("Occurs when the DropDown has opened.")]
    public event EventHandler? Opened;

    [Category("Action")]
    [Description("Occurs when the DropDown is opening.")]
    public event CancelEventHandler? Opening;

    [Category("Action")]
    [Description("Occurs when the DropDown has closed.")]
    public event ToolStripDropDownClosedEventHandler? Closed;

    [Category("Action")]
    [Description("Occurs when the DropDown is about to close.")]
    public event ToolStripDropDownClosingEventHandler? Closing;

    protected virtual void OnOpened(EventArgs e) => Opened?.Invoke(this, e);
    protected virtual void OnOpening(CancelEventArgs e) => Opening?.Invoke(this, e);
    protected virtual void OnClosed(ToolStripDropDownClosedEventArgs e) => Closed?.Invoke(this, e);
    protected virtual void OnClosing(ToolStripDropDownClosingEventArgs e) => Closing?.Invoke(this, e);

    // --- a menu the shell draws (NotifyIcon) -------------------------------------------------

    /// <summary>The tray shows this strip's items in its own menu; its events still fire. True if Opening cancelled.</summary>
    internal bool RaiseOpeningForShell()
    {
        var opening = new CancelEventArgs();
        OnOpening(opening);
        return opening.Cancel;
    }

    internal void RaiseOpenedForShell() => OnOpened(EventArgs.Empty);

    internal void RaiseClosedForShell()
    {
        OnClosing(new ToolStripDropDownClosingEventArgs(ToolStripDropDownCloseReason.AppFocusChange));
        OnClosed(new ToolStripDropDownClosedEventArgs(ToolStripDropDownCloseReason.AppFocusChange));
    }

    // --- show / close ----------------------------------------------------------------------

    protected override void SetVisibleCore(bool value)
    {
        if (value) Show();
        else Close();
    }

    public void Show() => ShowCore(null, LastScreenLocation ?? Point.Empty);

    public void Show(Point screenLocation) => ShowCore(null, screenLocation);

    public void Show(int x, int y) => ShowCore(null, new Point(x, y));

    public void Show(Control control, Point position) => Show(control, position, ToolStripDropDownDirection.Default);

    public void Show(Control control, int x, int y) => Show(control, new Point(x, y), ToolStripDropDownDirection.Default);

    public void Show(Control control, Point position, ToolStripDropDownDirection direction)
    {
        ArgumentNullException.ThrowIfNull(control);
        ShowCore(control, control.PointToScreen(position), direction);
    }

    internal Point? LastScreenLocation { get; private set; }

    /// <summary>Shows the drop-down at a screen point already worked out by the owning item.</summary>
    internal void ShowAtScreen(Point screenLocation, ToolStripDropDownDirection direction) => ShowCore(null, screenLocation, direction);

    private void ShowCore(Control? anchor, Point screenLocation, ToolStripDropDownDirection direction = ToolStripDropDownDirection.Default)
    {
        var opening = new CancelEventArgs();
        OnOpening(opening);
        if (opening.Cancel) return;

        EnsureItemLayout();
        var size = GetPreferredSize(Size.Empty);
        size = new Size(Math.Max(size.Width, MinimumSize.Width), Math.Max(size.Height, MinimumSize.Height));
        screenLocation = ApplyDirection(screenLocation, size, direction);
        screenLocation = ClampToScreen(screenLocation, size);
        LastScreenLocation = screenLocation;

        var anchorControl = anchor ?? _ownerItem?.Owner ?? (Control?)Form.ActiveForm;
        if (anchorControl == null) return;

        ToolStripManager.RegisterOpenDropDown(this);
        _host.ShowAt(anchorControl, screenLocation, size);
        PerformLayout();
        OnOpened(EventArgs.Empty);
        (_ownerItem as ToolStripDropDownItem)?.RaiseDropDownOpened();
    }

    private Point ApplyDirection(Point location, Size size, ToolStripDropDownDirection direction)
    {
        if (direction == ToolStripDropDownDirection.Default) direction = DefaultDropDownDirection;
        return direction switch
        {
            ToolStripDropDownDirection.AboveLeft => new Point(location.X - size.Width, location.Y - size.Height),
            ToolStripDropDownDirection.AboveRight => new Point(location.X, location.Y - size.Height),
            ToolStripDropDownDirection.BelowLeft => new Point(location.X - size.Width, location.Y),
            ToolStripDropDownDirection.Left => new Point(location.X - size.Width, location.Y),
            _ => location,
        };
    }

    private static Point ClampToScreen(Point location, Size size)
    {
        var area = Screen.FromPoint(location).WorkingArea;
        int x = Math.Min(location.X, area.Right - size.Width);
        int y = Math.Min(location.Y, area.Bottom - size.Height);
        return new Point(Math.Max(area.Left, x), Math.Max(area.Top, y));
    }

    public void Close() => Close(ToolStripDropDownCloseReason.CloseCalled);

    public void Close(ToolStripDropDownCloseReason reason)
    {
        if (_closing || !_host.VisibleOwn) return;
        var closing = new ToolStripDropDownClosingEventArgs(reason);
        OnClosing(closing);
        if (closing.Cancel) return;

        _closing = true;
        try
        {
            CloseChildDropDowns(reason);
            SelectItem(null);
            ToolStripManager.UnregisterOpenDropDown(this);
            _host.Dismiss();
        }
        finally
        {
            _closing = false;
        }
        OnClosed(new ToolStripDropDownClosedEventArgs(reason));
        if (_ownerItem is ToolStripDropDownItem owner)
        {
            owner.RaiseDropDownClosed();
            owner.Invalidate();
        }
    }

    internal void CloseChildDropDowns(ToolStripDropDownCloseReason reason)
    {
        foreach (var item in Items.ToArray())
        {
            if (item is ToolStripDropDownItem { DropDownVisible: true } child) child.DropDown.Close(reason);
        }
    }

    /// <summary>Closes this drop-down and every drop-down above it, up to the one the menu started from.</summary>
    internal void CloseChain(ToolStripDropDownCloseReason reason)
    {
        var root = this;
        while (root.OwnerItem?.Owner is ToolStripDropDown parent) root = parent;
        root.Close(reason);
    }

    // --- input ------------------------------------------------------------------------------

    internal override void SelectItem(ToolStripItem? item)
    {
        var previous = SelectedItemInternal;
        base.SelectItem(item);
        if (ReferenceEquals(previous, item)) return;

        // Moving off an item that had its submenu open closes it; landing on one opens it.
        if (previous is ToolStripDropDownItem { DropDownVisible: true } old && !ReferenceEquals(old, item))
        {
            old.HideDropDown();
        }
        if (item is ToolStripDropDownItem { HasDropDownItems: true } next && next.Enabled && _host.VisibleOwn)
        {
            next.ShowDropDown();
        }
    }

    internal override void HandleItemClick(ToolStripItem item)
    {
        base.HandleItemClick(item);
        (_ownerItem as ToolStripDropDownItem)?.RaiseDropDownItemClicked(item);
        // A leaf item closes the whole menu; a submenu parent keeps it open.
        if (_autoClose && item is not ToolStripDropDownItem { HasDropDownItems: true })
        {
            CloseChain(ToolStripDropDownCloseReason.ItemClicked);
        }
    }

    /// <summary>True when a click at this point in the owner form lands on the item that opened us.</summary>
    internal bool IsClickOnOwnerItem(Point ownerFormPoint)
    {
        if (_ownerItem?.Owner is not ToolStrip strip || strip is ToolStripDropDown) return false;
        var local = strip.PointFromForm(ownerFormPoint);
        return _ownerItem.Bounds.Contains(local);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ToolStripManager.UnregisterOpenDropDown(this);
            _host.Controls.Remove(this);
            _host.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <summary>The window the drop-down lives in; it forwards dismissal back to Close so events fire.</summary>
    private sealed class DropDownHost : PopupForm
    {
        private readonly ToolStripDropDown _dropDown;

        public DropDownHost(ToolStripDropDown dropDown)
        {
            _dropDown = dropDown;
            Dismissed += (_, _) =>
            {
                ToolStripManager.UnregisterOpenDropDown(_dropDown);
                _dropDown.CloseChildDropDowns(ToolStripDropDownCloseReason.AppClicked);
            };
        }

        public Form? PopupOwnerForm => Owner;

        /// <summary>
        /// An AutoSize drop-down sizes itself (and the window was opened at that size); one with a fixed
        /// size is stretched over the window instead.
        /// </summary>
        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            if (_dropDown.AutoSize)
            {
                _dropDown.Location = Point.Empty;
            }
            else
            {
                var client = ClientSize;
                _dropDown.SetBounds(0, 0, client.Width, client.Height);
            }
        }

        protected override bool IsClickInsideAnchor(Point ownerClientPoint) => _dropDown.IsClickOnOwnerItem(ownerClientPoint);
    }

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Category("Behavior")]
    [DefaultValue(false)]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new bool AllowItemReorder { get => base.AllowItemReorder; set => base.AllowItemReorder = value; }

    [Category("Layout")]
    [DefaultValue(AnchorStyles.Top | AnchorStyles.Left)]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override AnchorStyles Anchor { get => base.Anchor; set => base.Anchor = value; }

    [Category("Layout")]
    [DefaultValue(false)]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new bool CanOverflow { get => base.CanOverflow; set => base.CanOverflow = value; }

    [Category("Behavior")]
    [DefaultValue(null)]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override ContextMenuStrip? ContextMenuStrip { get => base.ContextMenuStrip; set => base.ContextMenuStrip = value; }

    [Category("Layout")]
    [DefaultValue(DockStyle.None)]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override DockStyle Dock { get => base.Dock; set => base.Dock = value; }

    [Category("Layout")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new Padding GripMargin { get => base.GripMargin; set => base.GripMargin = value; }

    [Category("Appearance")]
    [DefaultValue(ToolStripGripStyle.Hidden)]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new ToolStripGripStyle GripStyle { get => base.GripStyle; set => base.GripStyle = value; }

    [Category("Layout")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new Point Location { get => base.Location; set => base.Location = value; }

    [Category("Behavior")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new int TabIndex { get => base.TabIndex; set => base.TabIndex = value; }

    [Category("Behavior")]
    [DefaultValue(false)]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new bool Visible { get => base.Visible; set => base.Visible = value; }

    [Category("Property Changed")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? DockChanged
    {
        add => base.DockChanged += value;
        remove => base.DockChanged -= value;
    }

    [Category("Focus")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? Enter
    {
        add => base.Enter += value;
        remove => base.Enter -= value;
    }

    [Category("Property Changed")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? FontChanged
    {
        add => base.FontChanged += value;
        remove => base.FontChanged -= value;
    }

    [Category("Key")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event KeyEventHandler? KeyDown
    {
        add => base.KeyDown += value;
        remove => base.KeyDown -= value;
    }

    [Category("Key")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event KeyPressEventHandler? KeyPress
    {
        add => base.KeyPress += value;
        remove => base.KeyPress -= value;
    }

    [Category("Key")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event KeyEventHandler? KeyUp
    {
        add => base.KeyUp += value;
        remove => base.KeyUp -= value;
    }

    [Category("Focus")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? Leave
    {
        add => base.Leave += value;
        remove => base.Leave -= value;
    }

    [Category("Action")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event ScrollEventHandler? Scroll
    {
        add => base.Scroll += value;
        remove => base.Scroll -= value;
    }

    [Category("Property Changed")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? TabIndexChanged
    {
        add => base.TabIndexChanged += value;
        remove => base.TabIndexChanged -= value;
    }

    [Category("Property Changed")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? TabStopChanged
    {
        add => base.TabStopChanged += value;
        remove => base.TabStopChanged -= value;
    }

    [Category("Property Changed")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? TextChanged
    {
        add => base.TextChanged += value;
        remove => base.TextChanged -= value;
    }

    [Category("Focus")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? Validated
    {
        add => base.Validated += value;
        remove => base.Validated -= value;
    }

    [Category("Focus")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event CancelEventHandler? Validating
    {
        add => base.Validating += value;
        remove => base.Validating -= value;
    }
}

/// <summary>
/// The vertical menu a <see cref="ToolStripMenuItem"/> drops down: one item per row, full width, with
/// the check/image column down the left. WinForms folds the image margin into the drop-down's Padding;
/// we let items span the full width and draw the margin under them, which lays out the same but keeps
/// item bounds intuitive (docs/PLAN.md).
/// </summary>
public class ToolStripDropDownMenu : ToolStripDropDown
{
    /// <summary>Width of the check/image column, from dotnet/winforms' DefaultImageMarginWidth (24) plus its 1px border.</summary>
    internal const int DefaultImageMarginWidth = 25;

    internal const int DefaultImageAndCheckMarginWidth = 47;

    private bool _showImageMargin = true;
    private bool _showCheckMargin;

    public ToolStripDropDownMenu()
    {
        LayoutStyle = ToolStripLayoutStyle.Flow;
    }

    public ToolStripDropDownMenu(ToolStripItem ownerItem, bool isAutoGenerated) : this()
    {
        OwnerItem = ownerItem;
        IsAutoGenerated = isAutoGenerated;
    }

    protected override Padding DefaultPadding => new Padding(1, 2, 1, 2);

    [Category("Appearance")]
    [Description("Specifies whether the image margin will be shown.")]
    [DefaultValue(true)]
    public bool ShowImageMargin
    {
        get => _showImageMargin;
        set
        {
            if (_showImageMargin == value) return;
            _showImageMargin = value;
            ItemsChanged();
        }
    }

    [Category("Appearance")]
    [Description("Specifies whether the check margin will be shown.")]
    [DefaultValue(false)]
    public bool ShowCheckMargin
    {
        get => _showCheckMargin;
        set
        {
            if (_showCheckMargin == value) return;
            _showCheckMargin = value;
            ItemsChanged();
        }
    }

    /// <summary>Width of the column to the left of the item text; 0 when neither margin is shown.</summary>
    internal int ImageMarginWidth => _showCheckMargin && _showImageMargin ? DefaultImageAndCheckMarginWidth
        : _showImageMargin || _showCheckMargin ? DefaultImageMarginWidth
        : 0;

    [Category("Layout")]
    [Description("Specifies the layout orientation of the ToolStrip.")]
    [DefaultValue(ToolStripLayoutStyle.Flow)]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override ToolStripLayoutStyle LayoutStyle
    {
        get => ToolStripLayoutStyle.Flow;
        set { }
    }

    protected internal override ToolStripItem CreateDefaultItem(string? text, Image? image, EventHandler? onClick)
    {
        if (text == "-") return new ToolStripSeparator();
        return new ToolStripMenuItem(text, image, onClick);
    }

    private protected override void LayoutItems()
    {
        var display = DisplayRectangle;
        int y = display.Top;
        foreach (var item in Items)
        {
            if (!item.Available)
            {
                item.Placement = ToolStripItemPlacement.None;
                continue;
            }
            var margin = item.Margin;
            var pref = item.GetPreferredSize(new Size(display.Width, 0));
            y += margin.Top;
            item.SetBounds(new Rectangle(display.Left + margin.Left, y, Math.Max(0, display.Width - margin.Horizontal), pref.Height));
            item.Placement = ToolStripItemPlacement.Main;
            AddDisplayedItem(item);
            y += pref.Height + margin.Bottom;
        }
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        int width = 0, height = 0;
        foreach (var item in Items)
        {
            if (!item.Available) continue;
            var pref = item.GetPreferredSize(Size.Empty);
            width = Math.Max(width, pref.Width + item.Margin.Horizontal);
            height += pref.Height + item.Margin.Vertical;
        }
        var padding = Padding;
        return new Size(Math.Max(width + padding.Horizontal, 32), height + padding.Vertical);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        base.OnPaintBackground(e);
        if (ImageMarginWidth > 0)
        {
            var display = DisplayRectangle;
            var bounds = new Rectangle(display.Left, display.Top, ImageMarginWidth, display.Height);
            Renderer.DrawImageMargin(new ToolStripRenderEventArgs(e.Graphics, this, bounds, BackColor));
        }
    }
}

/// <summary>The drop-down that holds the items which did not fit on a full strip.</summary>
public class ToolStripOverflow : ToolStripDropDown
{
    private readonly List<ToolStripItem> _borrowed = new();

    public ToolStripOverflow(ToolStripItem parentItem)
    {
        OwnerItem = parentItem;
        IsAutoGenerated = true;
    }

    /// <summary>The overflow shows items that belong to the strip; it never takes ownership of them.</summary>
    internal void SetItems(IReadOnlyList<ToolStripItem> items)
    {
        _borrowed.Clear();
        _borrowed.AddRange(items);
    }

    internal bool HasItems => _borrowed.Count > 0;

    private protected override void LayoutItems()
    {
        var display = DisplayRectangle;
        int y = display.Top;
        foreach (var item in _borrowed)
        {
            if (!item.Available) continue;
            var pref = item.GetPreferredSize(new Size(display.Width, 0));
            item.SetBounds(new Rectangle(display.Left, y, display.Width, pref.Height));
            AddDisplayedItem(item);
            y += pref.Height;
        }
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        int width = 0, height = 0;
        foreach (var item in _borrowed)
        {
            if (!item.Available) continue;
            var pref = item.GetPreferredSize(Size.Empty);
            width = Math.Max(width, pref.Width);
            height += pref.Height;
        }
        var padding = Padding;
        return new Size(width + padding.Horizontal, height + padding.Vertical);
    }
}

/// <summary>The menu a control shows on right-click; assign it to <see cref="Control.ContextMenuStrip"/>.</summary>
[DefaultEvent(nameof(Opening))]
public class ContextMenuStrip : ToolStripDropDownMenu
{
    public ContextMenuStrip() { }

    public ContextMenuStrip(IContainer container) : this()
    {
        container?.Add(this);
    }

    /// <summary>The control the menu was last shown for (WinForms' SourceControl).</summary>
    [Description("The last control that caused this context menu strip to be displayed.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? SourceControl { get; internal set; }
}
