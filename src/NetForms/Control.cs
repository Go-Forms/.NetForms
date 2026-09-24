using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms.Layout;

namespace System.Windows.Forms;

/// <summary>
/// The base of every visual element. Owns its bounds (relative to the parent's client
/// area), its children, ambient properties (Font/ForeColor/BackColor inherit from the
/// parent), layout (Anchor/Dock), painting (<see cref="Invalidate()"/> → <see cref="OnPaint"/>)
/// and the mouse/keyboard/focus events - the same contract as WinForms' Control, without HWNDs.
/// </summary>
[DefaultEvent("Click")]
[DefaultProperty("Text")]
public partial class Control : Component, IWin32Window
{
    private static Font? s_defaultFont;

    private Control? _parent;
    private ControlCollection? _controls;
    private string _text = string.Empty;
    private string _name = string.Empty;
    private int _x, _y, _width, _height;
    private Font? _font;
    private Color _backColor = Color.Empty;
    private Color _foreColor = Color.Empty;
    private bool _visible = true;
    private bool _enabled = true;
    private bool _tabStop = true;
    private int _tabIndex = -1;
    private AnchorStyles _anchor = AnchorStyles.Top | AnchorStyles.Left;
    private DockStyle _dock = DockStyle.None;
    private Padding _padding = Padding.Empty;
    private Padding _margin = new Padding(3);
    private Size _minimumSize;
    private Size _maximumSize;
    private ControlStyles _controlStyle;
    private int _layoutSuspendCount;
    private bool _layoutDeferred;
    private bool _autoSize;
    private bool _isDisposed;
    private Size _specifiedSize;

    public Control() : this(string.Empty) { }

    public Control(string? text)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.CacheText | ControlStyles.StandardClick |
                 ControlStyles.Selectable | ControlStyles.StandardDoubleClick | ControlStyles.UserPaint |
                 ControlStyles.UseTextForAccessibility, true);
        var size = DefaultSize;
        _width = size.Width;
        _height = size.Height;
        _padding = DefaultPadding;
        _margin = DefaultMargin;
        if (!string.IsNullOrEmpty(text)) _text = text;
    }

    public Control(string? text, int left, int top, int width, int height) : this(text)
    {
        SetBounds(left, top, width, height);
    }

    public Control(Control? parent, string? text) : this(text)
    {
        Parent = parent;
    }

    public Control(Control? parent, string? text, int left, int top, int width, int height) : this(parent, text)
    {
        SetBounds(left, top, width, height);
    }

    // --- defaults ------------------------------------------------------------------

    public static Font DefaultFont
    {
        get => s_defaultFont ??= SystemFonts.DefaultFont;
        internal set => s_defaultFont = value;
    }

    public static Color DefaultBackColor => SystemColors.Control;
    public static Color DefaultForeColor => SystemColors.ControlText;

    protected virtual Size DefaultSize => Size.Empty;
    protected virtual Padding DefaultPadding => Padding.Empty;
    protected virtual Padding DefaultMargin => new Padding(3);
    protected virtual Size DefaultMinimumSize => Size.Empty;
    protected virtual Size DefaultMaximumSize => Size.Empty;
    protected virtual Cursor DefaultCursor => Cursors.Default;

    // --- identity ------------------------------------------------------------------

    [Browsable(false)]
    public string Name
    {
        get => _name;
        set => _name = value ?? string.Empty;
    }

    [Category("Data")]
    [Description("User-defined data associated with the object.")]
    [DefaultValue(null)]
    public object? Tag { get; set; }

    [Category("Appearance")]
    [Description("The text associated with the control.")]
    [Localizable(true)]
    public virtual string Text
    {
        get => _text;
        set
        {
            value ??= string.Empty;
            if (_text == value) return;
            _text = value;
            OnTextChanged(EventArgs.Empty);
        }
    }

    [Category("Accessibility")]
    [Description("The name that will be reported to accessibility clients.")]
    [DefaultValue(null)]
    [Localizable(true)]
    public string? AccessibleName { get; set; }

    [Category("Accessibility")]
    [Description("The description that will be reported to accessibility clients.")]
    [DefaultValue(null)]
    [Localizable(true)]
    public string? AccessibleDescription { get; set; }

    // --- tree ----------------------------------------------------------------------

    [Category("Behavior")]
    [Description("The parent of this control.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Parent
    {
        get => _parent;
        set
        {
            if (_parent == value) return;
            if (value != null) value.Controls.Add(this);
            else _parent?.Controls.Remove(this);
        }
    }

    [Description("The collection of child controls within this control.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public ControlCollection Controls => _controls ??= CreateControlsInstance();

    protected virtual ControlCollection CreateControlsInstance() => new ControlCollection(this);

    [Description("Indicates whether the control contains one or more child controls.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool HasChildren => _controls != null && _controls.Count > 0;

    [Category("Behavior")]
    [Description("Retrieves the top-level control that contains this control.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? TopLevelControl
    {
        get
        {
            Control c = this;
            while (c._parent != null) c = c._parent;
            return c.GetTopLevel() ? c : null;
        }
    }

    protected virtual bool GetTopLevel() => false;

    public Form? FindForm()
    {
        Control? c = this;
        while (c != null && c is not Form) c = c._parent;
        return c as Form;
    }

    /// <summary>
    /// The form that owns the platform window: the same as <see cref="FindForm"/> for an ordinary
    /// control, and the MDI parent for anything inside an MDI child. Window-level state - the paint
    /// surface, focus, capture and the cursor - lives there.
    /// </summary>
    internal Form? TopLevelForm
    {
        get
        {
            Control? c = this;
            while (c != null)
            {
                if (c is Form { MdiParent: null } form) return form;
                c = c._parent;
            }
            return null;
        }
    }

    public bool Contains(Control? ctl)
    {
        while (ctl != null)
        {
            ctl = ctl._parent;
            if (ctl == this) return true;
        }
        return false;
    }

    public IContainerControl? GetContainerControl()
    {
        Control? c = _parent;
        while (c != null && !c.GetStyle(ControlStyles.ContainerControl)) c = c._parent;
        return c as IContainerControl;
    }

    internal void AssignParent(Control? value)
    {
        var old = _parent;
        _parent = value;
        OnParentChanged(EventArgs.Empty);
        if (old?.Font != Font) OnFontChanged(EventArgs.Empty);
        if (old?.BackColor != BackColor) OnBackColorChanged(EventArgs.Empty);
        if (old?.ForeColor != ForeColor) OnForeColorChanged(EventArgs.Empty);
    }

    // --- geometry ------------------------------------------------------------------

    [Category("Layout")]
    [Description("The coordinates of the upper-left corner of the control relative to the upper-left corner of its container.")]
    [Localizable(true)]
    public Point Location
    {
        get => new Point(_x, _y);
        set => SetBounds(value.X, value.Y, _width, _height, BoundsSpecified.Location);
    }

    [Category("Layout")]
    [Description("The size of the control in pixels.")]
    [Localizable(true)]
    public Size Size
    {
        get => new Size(_width, _height);
        set => SetBounds(_x, _y, value.Width, value.Height, BoundsSpecified.Size);
    }

    [Category("Layout")]
    [Description("The bounds of the control, in container coordinates.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Rectangle Bounds
    {
        get => new Rectangle(_x, _y, _width, _height);
        set => SetBounds(value.X, value.Y, value.Width, value.Height, BoundsSpecified.All);
    }

    [Category("Layout")]
    [Description("The upper left of the control, in container coordinates.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Left
    {
        get => _x;
        set => SetBounds(value, _y, _width, _height, BoundsSpecified.X);
    }

    [Category("Layout")]
    [Description("The top of the control, in container coordinates.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Top
    {
        get => _y;
        set => SetBounds(_x, value, _width, _height, BoundsSpecified.Y);
    }

    [Category("Layout")]
    [Description("The width of the control, in container coordinates.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Width
    {
        get => _width;
        set => SetBounds(_x, _y, value, _height, BoundsSpecified.Width);
    }

    [Category("Layout")]
    [Description("The height of the user interface element, in pixels.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Height
    {
        get => _height;
        set => SetBounds(_x, _y, _width, value, BoundsSpecified.Height);
    }

    [Category("Layout")]
    [Description("The distance, in pixels, between the right edge of the control and the left edge of its container's client area.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Right => _x + _width;

    [Category("Layout")]
    [Description("The bottom of the control, in container coordinates.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Bottom => _y + _height;

    [Category("Layout")]
    [Description("Determines the size of the inner area of this control.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Size ClientSize
    {
        get => ClientSizeCore;
        set => SetClientSizeCore(value.Width, value.Height);
    }

    /// <summary>
    /// The size of the client area. A plain control has no non-client area, so this is its size;
    /// an MDI child form subtracts its frame and caption.
    /// </summary>
    internal virtual Size ClientSizeCore => new Size(_width, _height);

    /// <summary>
    /// Where the client area starts inside the control's own bounds. Non-zero only when a control
    /// draws a frame of its own (an MDI child): child coordinates are relative to the client area,
    /// as in WinForms, and painting, hit-testing and coordinate mapping shift by this much.
    /// </summary>
    internal virtual Point ClientOrigin => Point.Empty;

    /// <summary>The area children live in, in client coordinates - the origin is always (0,0).</summary>
    [Category("Layout")]
    [Description("Retrieves the rectangle of the inner area of this control.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Rectangle ClientRectangle => new Rectangle(Point.Empty, ClientSizeCore);

    /// <summary>
    /// The rectangle docking and anchoring work against: the client area. As in WinForms, only a
    /// <see cref="ScrollableControl"/> (Panel, Form, UserControl…) takes its <see cref="Padding"/> off it;
    /// a plain Control docks its children over the whole client area, whatever its Padding says.
    /// </summary>
    [Description("Retrieves the display rectangle of this control.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public virtual Rectangle DisplayRectangle => ClientRectangle;

    [Browsable(false)]
    public Size PreferredSize => GetPreferredSize(Size.Empty);

    /// <summary>
    /// The size the control wants. A plain control answers with its current size; a
    /// container answers with what DefaultLayout measures for its children (docked and
    /// anchored, see <see cref="System.Windows.Forms.Layout.DefaultLayout.GetPreferredContentSize"/>)
    /// plus its insets, so AutoSize containers grow around their content.
    /// </summary>
    public virtual Size GetPreferredSize(Size proposedSize)
    {
        if (_controls == null || _controls.Count == 0) return Size;
        var content = System.Windows.Forms.Layout.DefaultLayout.GetPreferredContentSize(this);
        var display = DisplayRectangle;
        var client = ClientSize;
        return new Size(
            content.Width + display.X + (client.Width - display.Right) + (Width - client.Width),
            content.Height + display.Y + (client.Height - display.Bottom) + (Height - client.Height));
    }

    /// <summary>
    /// How AutoSize resizes: GrowAndShrink follows the preferred size exactly (Label);
    /// GrowOnly - the documented default of Button/CheckBox/Panel/GroupBox/Form - never
    /// goes below the size user code last set.
    /// </summary>
    internal virtual AutoSizeMode AutoSizeModeCore => AutoSizeMode.GrowAndShrink;

    [Category("Layout")]
    [Description("Specifies the minimum size of the control.")]
    [Localizable(true)]
    public virtual Size MinimumSize
    {
        get => _minimumSize;
        set
        {
            if (_minimumSize == value) return;
            _minimumSize = value;
            SetBounds(_x, _y, _width, _height, BoundsSpecified.Size);
        }
    }

    [Category("Layout")]
    [Description("Specifies the maximum size of the control.")]
    [Localizable(true)]
    public virtual Size MaximumSize
    {
        get => _maximumSize;
        set
        {
            if (_maximumSize == value) return;
            _maximumSize = value;
            SetBounds(_x, _y, _width, _height, BoundsSpecified.Size);
        }
    }

    public void SetBounds(int x, int y, int width, int height) => SetBounds(x, y, width, height, BoundsSpecified.All);

    public void SetBounds(int x, int y, int width, int height, BoundsSpecified specified)
    {
        if ((specified & BoundsSpecified.X) == 0) x = _x;
        if ((specified & BoundsSpecified.Y) == 0) y = _y;
        if ((specified & BoundsSpecified.Width) == 0) width = _width;
        if ((specified & BoundsSpecified.Height) == 0) height = _height;

        if ((specified & BoundsSpecified.Size) != 0 && !_adjustingSize)
        {
            _specifiedSize = new Size((specified & BoundsSpecified.Width) != 0 ? width : _specifiedSize.Width, (specified & BoundsSpecified.Height) != 0 ? height : _specifiedSize.Height);
        }

        if (_x != x || _y != y || _width != width || _height != height)
        {
            SetBoundsCore(x, y, width, height, specified);
            _parent?.PerformLayout(this, nameof(Bounds));
        }
        else
        {
            // Setting the same bounds again still refreshes the anchor offsets, as WinForms does.
            _parent?.LayoutEngine.InitLayout(this, specified);
        }
    }

    /// <summary>Bounds set by the layout engine: no anchor refresh, no re-layout of the parent.</summary>
    internal void SetBoundsFromLayout(Rectangle bounds)
    {
        if (_x == bounds.X && _y == bounds.Y && _width == bounds.Width && _height == bounds.Height) return;
        SetBoundsCore(bounds.X, bounds.Y, bounds.Width, bounds.Height, BoundsSpecified.None);
    }

    protected virtual void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
    {
        width = ApplySizeConstraints(width, _minimumSize.Width, _maximumSize.Width);
        height = ApplySizeConstraints(height, _minimumSize.Height, _maximumSize.Height);

        bool moved = _x != x || _y != y;
        bool resized = _width != width || _height != height;
        if (!moved && !resized) return;

        _x = x;
        _y = y;
        _width = width;
        _height = height;

        _parent?.LayoutEngine.InitLayout(this, specified);

        if (moved)
        {
            OnLocationChanged(EventArgs.Empty);
        }
        if (resized)
        {
            OnSizeChanged(EventArgs.Empty);
        }
        _parent?.Invalidate();
    }

    private static int ApplySizeConstraints(int value, int min, int max)
    {
        if (value < 0) value = 0;
        if (max > 0 && value > max) value = max;
        if (min > 0 && value < min) value = min;
        return value;
    }

    protected virtual void SetClientSizeCore(int x, int y)
    {
        // No non-client area on a plain control: client size is the size.
        Size = new Size(x, y);
    }

    protected virtual Size SizeFromClientSize(Size clientSize) => clientSize;

    public Point PointToScreen(Point p)
    {
        var origin = TopLevelForm?.WindowLocation ?? Point.Empty;
        var inForm = PointToForm(p);
        return new Point(origin.X + inForm.X, origin.Y + inForm.Y);
    }

    public Point PointToClient(Point p)
    {
        var origin = TopLevelForm?.WindowLocation ?? Point.Empty;
        var formPoint = new Point(p.X - origin.X, p.Y - origin.Y);
        return PointFromForm(formPoint);
    }

    public Rectangle RectangleToScreen(Rectangle r) => new Rectangle(PointToScreen(r.Location), r.Size);

    public Rectangle RectangleToClient(Rectangle r) => new Rectangle(PointToClient(r.Location), r.Size);

    /// <summary>
    /// Translate a point in this control's client coordinates to the top-level form's client
    /// coordinates. A control's client area starts at its own bounds plus its
    /// <see cref="ClientOrigin"/>, so both are added on the way up; MDI children are walked through
    /// because the window belongs to their parent.
    /// </summary>
    internal Point PointToForm(Point p)
    {
        Control? c = this;
        while (c is not null and not Form { MdiParent: null })
        {
            var origin = c.ClientOrigin;
            p.Offset(c._x + origin.X, c._y + origin.Y);
            c = c._parent;
        }
        return p;
    }

    internal Point PointFromForm(Point p)
    {
        Control? c = this;
        while (c is not null and not Form { MdiParent: null })
        {
            var origin = c.ClientOrigin;
            p.Offset(-c._x - origin.X, -c._y - origin.Y);
            c = c._parent;
        }
        return p;
    }

    // --- layout --------------------------------------------------------------------

    [Category("Layout")]
    [Description("Defines the edges of the container to which a certain control is bound. When a control is anchored to an edge, the distance between the control's closest edge and the specified edge will remain constant.")]
    [DefaultValue(AnchorStyles.Top | AnchorStyles.Left)]
    [Localizable(true)]
    public virtual AnchorStyles Anchor
    {
        get => _anchor;
        set
        {
            if (_anchor == value) return;
            _anchor = value;
            // Anchor and Dock are mutually exclusive: the last one set wins.
            if (value != (AnchorStyles.Top | AnchorStyles.Left) && _dock != DockStyle.None)
            {
                _dock = DockStyle.None;
            }
            if (_parent != null) DefaultLayout.UpdateAnchorInfo(this);
            _parent?.PerformLayout(this, nameof(Anchor));
        }
    }

    [Category("Layout")]
    [Description("Defines which borders of the control are bound to the container.")]
    [DefaultValue(DockStyle.None)]
    [Localizable(true)]
    public virtual DockStyle Dock
    {
        get => _dock;
        set
        {
            if (_dock == value) return;
            _dock = value;
            if (value != DockStyle.None)
            {
                _anchor = AnchorStyles.Top | AnchorStyles.Left;
            }
            else if (_parent != null)
            {
                DefaultLayout.UpdateAnchorInfo(this);
            }
            OnDockChanged(EventArgs.Empty);
            _parent?.PerformLayout(this, nameof(Dock));
        }
    }

    [Category("Layout")]
    [Description("Specifies the interior spacing of a control.")]
    [Localizable(true)]
    public Padding Padding
    {
        get => _padding;
        set
        {
            if (_padding == value) return;
            _padding = value;
            OnPaddingChanged(EventArgs.Empty);
            PerformLayout(this, nameof(Padding));
        }
    }

    [Category("Layout")]
    [Description("Specifies space between this control and another control's margin.")]
    [Localizable(true)]
    public Padding Margin
    {
        get => _margin;
        set
        {
            if (_margin == value) return;
            _margin = value;
            OnMarginChanged(EventArgs.Empty);
            _parent?.PerformLayout(this, nameof(Margin));
        }
    }

    [Category("Layout")]
    [Description("Specifies whether a control will automatically size itself to fit its contents.")]
    [DefaultValue(false)]
    [Localizable(true)]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public virtual bool AutoSize
    {
        get => _autoSize;
        set
        {
            if (_autoSize == value) return;
            _autoSize = value;
            OnAutoSizeChanged(EventArgs.Empty);
            if (value) AdjustSizeToPreferred();
            _parent?.PerformLayout(this, nameof(AutoSize));
        }
    }

    /// <summary>
    /// AutoSize as the layout engine sees it (WinForms' <c>CommonProperties.GetAutoSize</c>). A text box
    /// differs: its AutoSize is a fixed-height style it enforces itself, not layout auto-sizing.
    /// </summary>
    internal virtual bool LayoutAutoSize => _autoSize;

    /// <summary>Resize to <see cref="PreferredSize"/>; controls that auto-size call this when their content changes.</summary>
    internal void AdjustSizeToPreferred()
    {
        if (!_autoSize || _adjustingSize) return;
        // A container's AutoSize is applied from its parent's layout in WinForms: a Panel that has no
        // parent never grows (a top-level Form sizes itself; a Label resizes itself either way).
        // Checked against WinForms, CompatScenarios autosize/panel-* and autosize/parented-*.
        if (_parent == null && _controls is { Count: > 0 } && this is not Form) return;
        if (_dock != DockStyle.None)
        {
            AdjustDockedSizeToPreferred();
            return;
        }
        var pref = GetPreferredSize(Size.Empty);
        if (AutoSizeModeCore == AutoSizeMode.GrowOnly)
        {
            pref = new Size(Math.Max(pref.Width, _specifiedSize.Width), Math.Max(pref.Height, _specifiedSize.Height));
        }
        if (pref != Size)
        {
            _adjustingSize = true;
            try
            {
                SetBoundsCore(_x, _y, pref.Width, pref.Height, BoundsSpecified.None);
                _parent?.LayoutEngine.InitLayout(this, BoundsSpecified.Size);
                _parent?.PerformLayout(this, nameof(AutoSize));
            }
            finally
            {
                _adjustingSize = false;
            }
        }
    }

    private bool _adjustingSize;

    [Browsable(false)]
    public virtual LayoutEngine LayoutEngine => DefaultLayout.Instance;

    internal DefaultLayout.AnchorInfo? AnchorInfo { get; set; }

    /// <summary>The control's own Visible flag - what layout looks at, regardless of the parent's state.</summary>
    internal bool ParticipatesInLayout => _visible;

    public void SuspendLayout() => _layoutSuspendCount++;

    public void ResumeLayout() => ResumeLayout(true);

    public void ResumeLayout(bool performLayout)
    {
        if (_layoutSuspendCount > 0) _layoutSuspendCount--;

        if (!performLayout)
        {
            // WinForms re-captures every child's anchor offsets here, which is what makes
            // designer code (bounds set before ClientSize) come out right.
            if (_controls != null)
            {
                foreach (var child in _controls) LayoutEngine.InitLayout(child, BoundsSpecified.All);
            }
        }
        else if (_layoutSuspendCount == 0 && _layoutDeferred)
        {
            PerformLayout();
        }
    }

    public void PerformLayout() => PerformLayout(null, null);

    public void PerformLayout(Control? affectedControl, string? affectedProperty)
    {
        if (_layoutSuspendCount > 0)
        {
            _layoutDeferred = true;
            return;
        }
        _layoutDeferred = false;
        OnLayout(new LayoutEventArgs(affectedControl, affectedProperty));
    }

    /// <summary>
    /// A docked AutoSize control takes its preferred size only across the dock - the height of a Top
    /// strip, the width of a Left panel - and the dock layout gives it the rest (DefaultLayout's
    /// <c>xGetDockedSize</c>). So it does not resize itself: it asks the parent to lay it out again.
    /// </summary>
    private void AdjustDockedSizeToPreferred()
    {
        if (!LayoutAutoSize) return;
        Size target;
        switch (_dock)
        {
            case DockStyle.Top:
            case DockStyle.Bottom:
                target = new Size(_width, GetPreferredSize(new Size(_width, 1)).Height);
                break;
            case DockStyle.Left:
            case DockStyle.Right:
                target = new Size(GetPreferredSize(new Size(1, _height)).Width, _height);
                break;
            default:
                return; // Fill takes whatever is left
        }
        if (target == Size) return;
        _adjustingSize = true;
        try
        {
            if (_parent != null) _parent.PerformLayout(this, nameof(AutoSize));
            else SetBoundsCore(_x, _y, target.Width, target.Height, BoundsSpecified.None);
        }
        finally
        {
            _adjustingSize = false;
        }
    }

    protected virtual void OnLayout(LayoutEventArgs levent)
    {
        Layout?.Invoke(this, levent);
        LayoutEngine.Layout(this, levent);
        if (_autoSize && _controls != null && _controls.Count > 0) AdjustSizeToPreferred();
    }

    /// <summary>Painted after the children (embedded scroll bars); nothing by default.</summary>
    internal virtual void OnPaintOverlay(Graphics g) { }

    /// <summary>True for points the control keeps for itself even where a child lies (embedded scroll bars).</summary>
    internal virtual bool IsOverlayPoint(Point p) => false;

    // --- adornments ------------------------------------------------------------------

    private List<Control>? _adornments;

    /// <summary>
    /// Adds an internal control that floats above this control's children: painted after them and hit
    /// before them, but never part of <see cref="Controls"/>, layout or tab order. It stands for what in
    /// WinForms is a separate child window on top of the siblings (ErrorProvider's icon). Its bounds are
    /// in this control's client coordinates and are set with <see cref="SetBoundsFromLayout"/>.
    /// </summary>
    internal void AddAdornment(Control adornment)
    {
        if (adornment._parent == this) return;
        (_adornments ??= new List<Control>()).Add(adornment);
        adornment._parent = this;
        Invalidate(adornment.Bounds);
    }

    internal void RemoveAdornment(Control adornment)
    {
        if (_adornments == null || !_adornments.Remove(adornment)) return;
        TopLevelForm?.ChildRemoved(adornment);
        adornment._parent = null;
        Invalidate(adornment.Bounds);
    }

    /// <summary>For an adornment: whether the point (in its own coordinates) belongs to it; the rest passes through.</summary>
    internal virtual bool AdornmentContains(Point p) => true;

    // --- ambient appearance --------------------------------------------------------

    [Category("Appearance")]
    [Description("The font used to display text in the control.")]
    [Localizable(true)]
    public virtual Font Font
    {
        get => _font ?? _parent?.Font ?? DefaultFont;
        set
        {
            if (ReferenceEquals(_font, value)) return;
            var old = Font;
            _font = value;
            if (!Equals(old, Font)) OnFontChanged(EventArgs.Empty);
        }
    }

    public void ResetFont() => Font = null!;

    [Category("Appearance")]
    [Description("The background color of the component.")]
    public virtual Color BackColor
    {
        get
        {
            if (!_backColor.IsEmpty) return _backColor;
            return _parent?.BackColor ?? DefaultBackColor;
        }
        set
        {
            if (_backColor == value) return;
            var old = BackColor;
            _backColor = value;
            if (old != BackColor) OnBackColorChanged(EventArgs.Empty);
        }
    }

    public void ResetBackColor() => BackColor = Color.Empty;

    /// <summary>True when BackColor was set on this control rather than inherited.</summary>
    internal bool IsBackColorSet => !_backColor.IsEmpty;

    /// <summary>The ambient colour as the base class computes it, for controls whose own default is SystemColors.Window.</summary>
    internal Color AmbientBackColor => _backColor.IsEmpty ? (_parent?.BackColor ?? DefaultBackColor) : _backColor;

    [Category("Appearance")]
    [Description("The foreground color of this component, which is used to display text.")]
    public virtual Color ForeColor
    {
        get
        {
            if (!_foreColor.IsEmpty) return _foreColor;
            return _parent?.ForeColor ?? DefaultForeColor;
        }
        set
        {
            if (_foreColor == value) return;
            var old = ForeColor;
            _foreColor = value;
            if (old != ForeColor) OnForeColorChanged(EventArgs.Empty);
        }
    }

    public void ResetForeColor() => ForeColor = Color.Empty;

    private Cursor? _cursor;

    [Category("Appearance")]
    [Description("The cursor that appears when the pointer moves over the control.")]
    public virtual Cursor Cursor
    {
        // As in WinForms: a control with a cursor of its own by default (a text box's I-beam, a link's
        // hand) shows it; only controls whose default is the arrow inherit the parent's cursor.
        get => _cursor ?? (DefaultCursor != Cursors.Default ? DefaultCursor : _parent?.Cursor ?? DefaultCursor);
        set
        {
            if (ReferenceEquals(_cursor, value)) return;
            _cursor = value;
            OnCursorChanged(EventArgs.Empty);
            TopLevelForm?.UpdateCursor();
        }
    }

    [Category("Property Changed")]
    [Description("Event raised when the value of the Cursor property is changed on Control.")]
    public event EventHandler? CursorChanged;

    protected virtual void OnCursorChanged(EventArgs e) => CursorChanged?.Invoke(this, e);

    [Category("Appearance")]
    [Description("Indicates whether the component should draw right-to-left for RTL languages.")]
    [Localizable(true)]
    public virtual RightToLeft RightToLeft
    {
        get => _rightToLeft;
        set
        {
            if (_rightToLeft == value) return;
            _rightToLeft = value;
            OnRightToLeftChanged(EventArgs.Empty);
        }
    }

    private RightToLeft _rightToLeft = RightToLeft.No;

    [Category("Appearance")]
    [Description("When this property is true, the Cursor property of the control and its child controls is set to WaitCursor.")]
    [DefaultValue(false)]
    public bool UseWaitCursor { get; set; }

    // --- state ---------------------------------------------------------------------

    [Category("Behavior")]
    [Description("Determines whether the control is visible or hidden.")]
    [Localizable(true)]
    public bool Visible
    {
        get => GetVisibleCore();
        set => SetVisibleCore(value);
    }

    protected virtual bool GetVisibleCore()
    {
        if (!_visible) return false;
        return _parent == null || _parent.GetVisibleCore();
    }

    protected virtual void SetVisibleCore(bool value)
    {
        if (_visible == value) return;
        _visible = value;
        OnVisibleChanged(EventArgs.Empty);
        _parent?.PerformLayout(this, nameof(Visible));
        _parent?.Invalidate();
    }

    /// <summary>The Visible flag as set on this control, ignoring the parents.</summary>
    internal bool VisibleOwn
    {
        get => _visible;
        set => _visible = value;
    }

    /// <summary>Moves the control to the front of the z-order: index 0 of its parent's Controls, painted last, docked last.</summary>
    public void BringToFront()
    {
        if (_parent != null) _parent.Controls.SetChildIndex(this, 0);
        else if (this is Form form) form.Activate();
    }

    /// <summary>Moves the control to the back of the z-order: the end of its parent's Controls.</summary>
    public void SendToBack()
    {
        if (_parent != null) _parent.Controls.SetChildIndex(this, _parent.Controls.Count - 1);
    }

    public void Show() => Visible = true;

    public void Hide() => Visible = false;

    [Category("Behavior")]
    [Description("Indicates whether the control is enabled.")]
    [Localizable(true)]
    public bool Enabled
    {
        get => _enabled && (_parent == null || _parent.Enabled);
        set
        {
            if (_enabled == value) return;
            bool old = Enabled;
            _enabled = value;
            if (old != Enabled) OnEnabledChanged(EventArgs.Empty);
        }
    }

    [Category("Behavior")]
    [Description("Indicates whether the user can use the TAB key to give focus to the control.")]
    [DefaultValue(true)]
    public bool TabStop
    {
        get => _tabStop;
        set
        {
            if (_tabStop == value) return;
            _tabStop = value;
            OnTabStopChanged(EventArgs.Empty);
        }
    }

    [Category("Behavior")]
    [Description("Determines the index in the TAB order that this control will occupy.")]
    [Localizable(true)]
    public int TabIndex
    {
        get => _tabIndex == -1 ? 0 : _tabIndex;
        set
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            if (_tabIndex == value) return;
            _tabIndex = value;
            OnTabIndexChanged(EventArgs.Empty);
        }
    }

    [Category("Focus")]
    [Description("Indicates whether this component raises validation events.")]
    [DefaultValue(true)]
    public bool CausesValidation
    {
        get => _causesValidation;
        set
        {
            if (_causesValidation == value) return;
            _causesValidation = value;
            OnCausesValidationChanged(EventArgs.Empty);
        }
    }

    private bool _causesValidation = true;

    [Category("Behavior")]
    [Description("Indicates whether the control can accept data that the user drags onto it.")]
    [DefaultValue(false)]
    public bool AllowDrop { get; set; }

    /// <summary>The menu shown on right-click; a control without one lets the click fall through to its parent.</summary>
    [Category("Behavior")]
    [Description("The shortcut menu to display when the user right-clicks the control.")]
    [DefaultValue(null)]
    public virtual ContextMenuStrip? ContextMenuStrip
    {
        get => _contextMenuStrip;
        set
        {
            if (_contextMenuStrip == value) return;
            _contextMenuStrip = value;
            OnContextMenuStripChanged(EventArgs.Empty);
        }
    }

    private ContextMenuStrip? _contextMenuStrip;

    private ControlBindingsCollection? _dataBindings;

    /// <summary>Links between this control's properties and a data source.</summary>
    [Category("Data")]
    [Description("The data bindings for the control.")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public ControlBindingsCollection DataBindings => _dataBindings ??= new ControlBindingsCollection(this);

    /// <summary>Right mouse-up: find the nearest ContextMenuStrip up the parent chain and pop it up.</summary>
    internal void ShowContextMenuStrip(Point clientPoint)
    {
        for (Control? c = this; c != null; c = c._parent)
        {
            var menu = c.ContextMenuStrip;
            if (menu == null) continue;
            menu.SourceControl = this;
            menu.Show(this, clientPoint);
            return;
        }
    }

    [Category("Behavior")]
    [Description("Determines the IME (Input Method Editor) status of the object when selected.")]
    [Localizable(true)]
    public ImeMode ImeMode
    {
        get => _imeMode;
        set
        {
            if (_imeMode == value) return;
            _imeMode = value;
            OnImeModeChanged(EventArgs.Empty);
        }
    }

    private ImeMode _imeMode = ImeMode.Inherit;

    /// <summary>True once the control lives in a shown form (the "handle created" moment of WinForms).</summary>
    [Description("Indicates whether the control has a handle associated with it.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsHandleCreated => TopLevelForm?.IsWindowCreated == true;

    [Description("Determines if the control has been fully created.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Created => IsHandleCreated;

    [Description("Determines if this control has been disposed.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsDisposed => _isDisposed;

    [Description("Determines whether this control is in the process of being disposed.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Disposing { get; private set; }

    [Category("Behavior")]
    [Description("Determines if this control is in the process of recreating its handle.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool RecreatingHandle => false;

    [Description("Determines if Invoke or BeginInvoke should be used to access this control cross-thread.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool InvokeRequired => !Application.IsUIThread;

    [Description("The native handle for this control.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IntPtr Handle => IntPtr.Zero;

    protected bool GetStyle(ControlStyles flag) => (_controlStyle & flag) == flag;

    protected void SetStyle(ControlStyles flag, bool value)
    {
        _controlStyle = value ? _controlStyle | flag : _controlStyle & ~flag;
    }

    protected virtual bool DoubleBuffered
    {
        get => GetStyle(ControlStyles.OptimizedDoubleBuffer);
        set => SetStyle(ControlStyles.OptimizedDoubleBuffer, value);
    }

    protected virtual bool CanEnableIme => false;

    [Category("Focus")]
    [Description("Checks if this control can be selected.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool CanSelect => GetStyle(ControlStyles.Selectable) && IsVisibleAndEnabledChain();

    [Category("Focus")]
    [Description("Checks if this control can receive the focus.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public virtual bool CanFocus => IsHandleCreated && IsVisibleAndEnabledChain();

    private bool IsVisibleAndEnabledChain()
    {
        for (Control? c = this; c != null; c = c._parent)
        {
            if (!c._visible || !c._enabled) return false;
        }
        return true;
    }

    // --- invalidation --------------------------------------------------------------

    public void Invalidate() => Invalidate(ClientRectangle, false);

    public void Invalidate(bool invalidateChildren) => Invalidate(ClientRectangle, invalidateChildren);

    public void Invalidate(Rectangle rc) => Invalidate(rc, false);

    public void Invalidate(Rectangle rc, bool invalidateChildren)
    {
        var form = TopLevelForm;
        if (form != null)
        {
            var inForm = new Rectangle(PointToForm(rc.Location), rc.Size);
            form.InvalidateWindow(inForm);
        }
        OnInvalidated(new InvalidateEventArgs(rc));
    }

    public void Update() => TopLevelForm?.UpdateWindow();

    public virtual void Refresh()
    {
        Invalidate(true);
        Update();
    }

    public Graphics CreateGraphics()
    {
        // No live surface to hand out: give a throw-away bitmap so measuring code keeps working.
        var bmp = new Bitmap(Math.Max(1, _width), Math.Max(1, _height));
        return Graphics.FromImage(bmp);
    }

    public IAsyncResult BeginInvoke(Delegate method) => BeginInvoke(method, null);

    public IAsyncResult BeginInvoke(Delegate method, params object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(method);
        var tcs = new System.Threading.Tasks.TaskCompletionSource<object?>();
        Application.Post(() =>
        {
            try { tcs.SetResult(method.DynamicInvoke(args)); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }

    public object? Invoke(Delegate method) => Invoke(method, null);

    public object? Invoke(Delegate method, params object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(method);
        if (!InvokeRequired) return method.DynamicInvoke(args);
        var task = (System.Threading.Tasks.Task<object?>)BeginInvoke(method, args);
        return task.GetAwaiter().GetResult();
    }

    public void Invoke(Action method) => Invoke((Delegate)method, null);

    public object? EndInvoke(IAsyncResult asyncResult) => ((System.Threading.Tasks.Task<object?>)asyncResult).GetAwaiter().GetResult();

    // --- disposal ------------------------------------------------------------------

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_isDisposed)
        {
            Disposing = true;
            if (_controls != null)
            {
                foreach (var c in _controls.ToArray()) c.Dispose();
            }
            _parent?.Controls.Remove(this);
            Disposing = false;
        }
        _isDisposed = true;
        base.Dispose(disposing);
    }

    public override string ToString() => GetType().FullName + ", Text: " + Text;

    // --- events and On* raisers ----------------------------------------------------

    [Category("Action")]
    [Description("Occurs when the component is clicked.")]
    public event EventHandler? Click;

    [Category("Action")]
    [Description("Occurs when the component is double-clicked.")]
    public event EventHandler? DoubleClick;

    [Category("Action")]
    [Description("Occurs when the control is clicked by the mouse.")]
    public event MouseEventHandler? MouseClick;

    [Category("Action")]
    [Description("Occurs when the control is double clicked by the mouse.")]
    public event MouseEventHandler? MouseDoubleClick;

    [Category("Mouse")]
    [Description("Occurs when the mouse pointer is over the component and a mouse button is pressed.")]
    public event MouseEventHandler? MouseDown;

    [Category("Mouse")]
    [Description("Occurs when the mouse pointer is over the component and a mouse button is released.")]
    public event MouseEventHandler? MouseUp;

    [Category("Mouse")]
    [Description("Occurs when the mouse pointer is moved over the component.")]
    public event MouseEventHandler? MouseMove;

    [Category("Mouse")]
    [Description("Occurs when the mouse wheel moves while the control has focus.")]
    [Browsable(false)]
    public event MouseEventHandler? MouseWheel;

    [Category("Mouse")]
    [Description("Occurs when the mouse enters the visible part of the control.")]
    public event EventHandler? MouseEnter;

    [Category("Mouse")]
    [Description("Occurs when the mouse leaves the visible part of the control.")]
    public event EventHandler? MouseLeave;

    [Category("Mouse")]
    [Description("Occurs when the mouse remains stationary inside of the control for an amount of time.")]
    public event EventHandler? MouseHover;

    [Category("Action")]
    [Description("Occurs after the mouse capture is changed.")]
    public event EventHandler? MouseCaptureChanged;

    [Category("Key")]
    [Description("Occurs when a key is first pressed.")]
    public event KeyEventHandler? KeyDown;

    [Category("Key")]
    [Description("Occurs when a key is released.")]
    public event KeyEventHandler? KeyUp;

    [Category("Key")]
    [Description("Occurs when the control has focus and the user presses and releases a key.")]
    public event KeyPressEventHandler? KeyPress;

    [Category("Focus")]
    [Description("Occurs when the control becomes the active control of the form.")]
    public event EventHandler? Enter;

    [Category("Focus")]
    [Description("Occurs when the control is no longer the active control of the form.")]
    public event EventHandler? Leave;

    [Category("Focus")]
    [Description("Occurs when the control gets focus.")]
    [Browsable(false)]
    public event EventHandler? GotFocus;

    [Category("Focus")]
    [Description("Occurs when the control loses focus.")]
    [Browsable(false)]
    public event EventHandler? LostFocus;

    [Category("Appearance")]
    [Description("Occurs when a control needs repainting.")]
    public event PaintEventHandler? Paint;

    [Category("Appearance")]
    [Description("Occurs when a control's display requires redrawing.")]
    [Browsable(false)]
    public event InvalidateEventHandler? Invalidated;

    [Category("Layout")]
    [Description("Occurs when a control is resized.")]
    public event EventHandler? Resize;

    [Category("Property Changed")]
    [Description("Event raised when the value of the Size property is changed on Control.")]
    public event EventHandler? SizeChanged;

    [Category("Property Changed")]
    [Description("Event raised when the value of the Location property is changed on Control.")]
    public event EventHandler? LocationChanged;

    [Category("Layout")]
    [Description("Occurs when a control is moved.")]
    public event EventHandler? Move;

    [Category("Property Changed")]
    [Description("Event raised when the value of the Text property is changed on Control.")]
    public event EventHandler? TextChanged;

    [Category("Property Changed")]
    [Description("Event raised when the value of the Font property is changed on Control.")]
    public event EventHandler? FontChanged;

    [Category("Property Changed")]
    [Description("Event raised when the value of the ForeColor property is changed on Control.")]
    public event EventHandler? ForeColorChanged;

    [Category("Property Changed")]
    [Description("Event raised when the value of the BackColor property is changed on Control.")]
    public event EventHandler? BackColorChanged;

    [Category("Property Changed")]
    [Description("Occurs when the control's enabled state changes.")]
    public event EventHandler? EnabledChanged;

    [Category("Property Changed")]
    [Description("Occurs when the control's visibility changes.")]
    public event EventHandler? VisibleChanged;

    [Category("Property Changed")]
    [Description("Event raised when the value of the Parent property is changed on Control.")]
    public event EventHandler? ParentChanged;

    [Category("Property Changed")]
    [Description("Event raised when the value of the Dock property is changed on Control.")]
    public event EventHandler? DockChanged;

    [Category("Layout")]
    [Description("Occurs when the Padding property has changed.")]
    public event EventHandler? PaddingChanged;

    [Category("Layout")]
    [Description("Occurs when the Margin property has changed.")]
    public event EventHandler? MarginChanged;

    [Category("Property Changed")]
    [Description("Occurs when the AutoSize property has changed.")]
    [Browsable(false)]
    public event EventHandler? AutoSizeChanged;

    [Category("Property Changed")]
    [Description("Event raised when the value of the TabStop property is changed on Control.")]
    public event EventHandler? TabStopChanged;

    [Category("Property Changed")]
    [Description("Event raised when the value of the TabIndex property is changed.")]
    public event EventHandler? TabIndexChanged;

    [Category("Property Changed")]
    [Description("Event raised when the value of the ClientSize property is changed on Control.")]
    public event EventHandler? ClientSizeChanged;

    [Category("Private")]
    [Description("Occurs when the control's native handle is created.")]
    [Browsable(false)]
    public event EventHandler? HandleCreated;

    [Category("Private")]
    [Description("Occurs when the control's native handle is destroyed.")]
    [Browsable(false)]
    public event EventHandler? HandleDestroyed;

    [Category("Behavior")]
    [Description("Occurs when a control is added to this control.")]
    public event ControlEventHandler? ControlAdded;

    [Category("Behavior")]
    [Description("Occurs when a control is removed from this control.")]
    public event ControlEventHandler? ControlRemoved;

    [Category("Layout")]
    [Description("Occurs when the control is about to lay out its contents.")]
    public event LayoutEventHandler? Layout;

    [Category("Focus")]
    [Description("Occurs when the control is validating.")]
    public event CancelEventHandler? Validating;

    [Category("Focus")]
    [Description("Occurs after a control has been successfully validated.")]
    public event EventHandler? Validated;

    protected virtual void OnClick(EventArgs e) => Click?.Invoke(this, e);
    protected virtual void OnDoubleClick(EventArgs e) => DoubleClick?.Invoke(this, e);
    protected virtual void OnMouseClick(MouseEventArgs e) => MouseClick?.Invoke(this, e);
    protected virtual void OnMouseDoubleClick(MouseEventArgs e) => MouseDoubleClick?.Invoke(this, e);
    protected virtual void OnMouseDown(MouseEventArgs e) => MouseDown?.Invoke(this, e);
    protected virtual void OnMouseUp(MouseEventArgs e) => MouseUp?.Invoke(this, e);
    protected virtual void OnMouseMove(MouseEventArgs e) => MouseMove?.Invoke(this, e);
    protected virtual void OnMouseWheel(MouseEventArgs e) => MouseWheel?.Invoke(this, e);
    protected virtual void OnMouseEnter(EventArgs e) => MouseEnter?.Invoke(this, e);
    protected virtual void OnMouseLeave(EventArgs e) => MouseLeave?.Invoke(this, e);
    protected virtual void OnMouseHover(EventArgs e) => MouseHover?.Invoke(this, e);
    protected virtual void OnMouseCaptureChanged(EventArgs e) => MouseCaptureChanged?.Invoke(this, e);
    protected virtual void OnKeyDown(KeyEventArgs e) => KeyDown?.Invoke(this, e);
    protected virtual void OnKeyUp(KeyEventArgs e) => KeyUp?.Invoke(this, e);
    protected virtual void OnKeyPress(KeyPressEventArgs e) => KeyPress?.Invoke(this, e);
    protected virtual void OnEnter(EventArgs e) => Enter?.Invoke(this, e);
    protected virtual void OnLeave(EventArgs e) => Leave?.Invoke(this, e);
    protected virtual void OnGotFocus(EventArgs e) => GotFocus?.Invoke(this, e);
    protected virtual void OnLostFocus(EventArgs e) => LostFocus?.Invoke(this, e);
    protected virtual void OnValidating(CancelEventArgs e) => Validating?.Invoke(this, e);
    protected virtual void OnValidated(EventArgs e) => Validated?.Invoke(this, e);
    protected virtual void OnInvalidated(InvalidateEventArgs e) => Invalidated?.Invoke(this, e);
    protected virtual void OnHandleCreated(EventArgs e) => HandleCreated?.Invoke(this, e);
    protected virtual void OnHandleDestroyed(EventArgs e) => HandleDestroyed?.Invoke(this, e);

    protected virtual void OnPaint(PaintEventArgs e) => Paint?.Invoke(this, e);

    protected virtual void OnPaintBackground(PaintEventArgs pevent)
    {
        var color = BackColor;
        if (BackgroundImage is { } image)
        {
            PaintBackgroundImage(pevent.Graphics, image, color, pevent.ClipRectangle);
            return;
        }
        if (color.A == 0) return;
        using var brush = new SolidBrush(color);
        pevent.Graphics.FillRectangle(brush, pevent.ClipRectangle);
    }

    protected virtual void OnResize(EventArgs e)
    {
        if (GetStyle(ControlStyles.ResizeRedraw)) Invalidate();
        PerformLayout(this, nameof(Bounds));
        Resize?.Invoke(this, e);
    }

    protected virtual void OnSizeChanged(EventArgs e)
    {
        OnResize(EventArgs.Empty);
        SizeChanged?.Invoke(this, e);
        OnClientSizeChanged(EventArgs.Empty);
    }

    protected virtual void OnClientSizeChanged(EventArgs e) => ClientSizeChanged?.Invoke(this, e);

    protected virtual void OnLocationChanged(EventArgs e)
    {
        OnMove(EventArgs.Empty);
        LocationChanged?.Invoke(this, e);
    }

    protected virtual void OnMove(EventArgs e) => Move?.Invoke(this, e);

    protected virtual void OnTextChanged(EventArgs e)
    {
        TextChanged?.Invoke(this, e);
        AdjustSizeToPreferred();
        Invalidate();
    }

    protected virtual void OnFontChanged(EventArgs e)
    {
        FontChanged?.Invoke(this, e);
        AdjustSizeToPreferred();
        Invalidate();
        if (_controls != null)
        {
            foreach (var c in _controls)
            {
                if (c._font == null) c.OnParentFontChanged(e);
            }
        }
    }

    protected virtual void OnParentFontChanged(EventArgs e) => OnFontChanged(e);

    protected virtual void OnForeColorChanged(EventArgs e)
    {
        ForeColorChanged?.Invoke(this, e);
        Invalidate();
        if (_controls != null)
        {
            foreach (var c in _controls)
            {
                if (c._foreColor == Color.Empty) c.OnParentForeColorChanged(e);
            }
        }
    }

    protected virtual void OnParentForeColorChanged(EventArgs e) => OnForeColorChanged(e);

    protected virtual void OnBackColorChanged(EventArgs e)
    {
        BackColorChanged?.Invoke(this, e);
        Invalidate();
        if (_controls != null)
        {
            foreach (var c in _controls)
            {
                if (c._backColor == Color.Empty) c.OnParentBackColorChanged(e);
            }
        }
    }

    protected virtual void OnParentBackColorChanged(EventArgs e) => OnBackColorChanged(e);

    protected virtual void OnEnabledChanged(EventArgs e)
    {
        EnabledChanged?.Invoke(this, e);
        Invalidate();
        if (_controls != null)
        {
            foreach (var c in _controls) c.OnParentEnabledChanged(e);
        }
    }

    protected virtual void OnParentEnabledChanged(EventArgs e) => OnEnabledChanged(e);

    protected virtual void OnVisibleChanged(EventArgs e)
    {
        VisibleChanged?.Invoke(this, e);
        if (_controls != null)
        {
            foreach (var c in _controls)
            {
                if (c._visible) c.OnParentVisibleChanged(e);
            }
        }
    }

    protected virtual void OnParentVisibleChanged(EventArgs e) => OnVisibleChanged(e);

    protected virtual void OnParentChanged(EventArgs e) => ParentChanged?.Invoke(this, e);
    protected virtual void OnDockChanged(EventArgs e) => DockChanged?.Invoke(this, e);
    protected virtual void OnPaddingChanged(EventArgs e) => PaddingChanged?.Invoke(this, e);
    protected virtual void OnMarginChanged(EventArgs e) => MarginChanged?.Invoke(this, e);
    protected virtual void OnAutoSizeChanged(EventArgs e) => AutoSizeChanged?.Invoke(this, e);
    protected virtual void OnTabStopChanged(EventArgs e) => TabStopChanged?.Invoke(this, e);
    protected virtual void OnTabIndexChanged(EventArgs e) => TabIndexChanged?.Invoke(this, e);
    protected virtual void OnControlAdded(ControlEventArgs e) => ControlAdded?.Invoke(this, e);
    protected virtual void OnControlRemoved(ControlEventArgs e) => ControlRemoved?.Invoke(this, e);

    protected virtual void OnCreateControl() { }

    /// <summary>Forces creation of the control (in WinForms: its handle). Here: the top-level form's window.</summary>
    public void CreateControl()
    {
        TopLevelForm?.CreateWindowIfNeeded();
        OnCreateControl();
    }

    internal void RaiseHandleCreatedRecursive()
    {
        OnHandleCreated(EventArgs.Empty);
        if (_controls != null)
        {
            foreach (var c in _controls) c.RaiseHandleCreatedRecursive();
        }
    }
}
