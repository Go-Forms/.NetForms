using System;
using System.ComponentModel;
using System.Drawing;

namespace System.Windows.Forms;

public enum FixedPanel
{
    None = 0,
    Panel1 = 1,
    Panel2 = 2,
}

public delegate void SplitterEventHandler(object? sender, SplitterEventArgs e);
public delegate void SplitterCancelEventHandler(object? sender, SplitterCancelEventArgs e);

public class SplitterEventArgs : EventArgs
{
    public SplitterEventArgs(int x, int y, int splitX, int splitY)
    {
        X = x;
        Y = y;
        SplitX = splitX;
        SplitY = splitY;
    }

    public int X { get; }
    public int Y { get; }
    public int SplitX { get; set; }
    public int SplitY { get; set; }
}

public class SplitterCancelEventArgs : CancelEventArgs
{
    public SplitterCancelEventArgs(int mouseCursorX, int mouseCursorY, int splitX, int splitY)
    {
        MouseCursorX = mouseCursorX;
        MouseCursorY = mouseCursorY;
        SplitX = splitX;
        SplitY = splitY;
    }

    public int MouseCursorX { get; }
    public int MouseCursorY { get; }
    public int SplitX { get; set; }
    public int SplitY { get; set; }
}

public sealed class SplitterPanel : Panel
{
    public SplitterPanel(SplitContainer owner) => Owner = owner ?? throw new ArgumentNullException(nameof(owner));

    internal SplitContainer Owner { get; }

    [Category("Layout")]
    [Description("Defines the edges of the container to which a certain control is bound. When a control is anchored to an edge, the distance between the control's closest edge and the specified edge will remain constant.")]
    [DefaultValue(AnchorStyles.Top | AnchorStyles.Left)]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new AnchorStyles Anchor { get => base.Anchor; set { } }

    [Category("Layout")]
    [Description("Defines which borders of the control are bound to the container.")]
    [DefaultValue(DockStyle.None)]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new DockStyle Dock { get => base.Dock; set { } }

    [Category("Behavior")]
    [Description("Determines whether the control is visible or hidden.")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new bool Visible { get => base.Visible; set { } }

    [Category("Layout")]
    [Description("Specifies the minimum size of the control.")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new Size MinimumSize { get => base.MinimumSize; set { } }

    [Category("Layout")]
    [Description("Specifies the maximum size of the control.")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new Size MaximumSize { get => base.MaximumSize; set { } }

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Category("Layout")]
    [DefaultValue(AutoSizeMode.GrowOnly)]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override AutoSizeMode AutoSizeMode { get => base.AutoSizeMode; set => base.AutoSizeMode = value; }

    [Category("Appearance")]
    [DefaultValue(BorderStyle.None)]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new BorderStyle BorderStyle { get => base.BorderStyle; set => base.BorderStyle = value; }

    [Category("Layout")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new Point Location { get => base.Location; set => base.Location = value; }

    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new string Name { get => base.Name; set => base.Name = value; }

    [Category("Layout")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new Size Size { get => base.Size; set => base.Size = value; }

    [Category("Behavior")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new int TabIndex { get => base.TabIndex; set => base.TabIndex = value; }

    [Category("Behavior")]
    [DefaultValue(false)]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new bool TabStop { get => base.TabStop; set => base.TabStop = value; }

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

    [Category("Property Changed")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? LocationChanged
    {
        add => base.LocationChanged += value;
        remove => base.LocationChanged -= value;
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
    public new event EventHandler? VisibleChanged
    {
        add => base.VisibleChanged += value;
        remove => base.VisibleChanged -= value;
    }

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Category("Layout")]
    [DefaultValue(false)]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override bool AutoSize { get => base.AutoSize; set => base.AutoSize = value; }

    [Category("Property Changed")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? AutoSizeChanged
    {
        add => base.AutoSizeChanged += value;
        remove => base.AutoSizeChanged -= value;
    }
}

/// <summary>Two panels separated by a draggable splitter. The panels are laid out by the container itself, not by Dock/Anchor.</summary>
[DefaultEvent("SplitterMoved")]
public class SplitContainer : ContainerControl, ISupportInitialize
{
    // Members WinForms hides from the designer on this control (Browsable(false)); checked against the real
    // WinForms by AttributeDiffTests.

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override ImageLayout BackgroundImageLayout
    {
        get => base.BackgroundImageLayout;
        set => base.BackgroundImageLayout = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? BackgroundImageLayoutChanged
    {
        add => base.BackgroundImageLayoutChanged += value;
        remove => base.BackgroundImageLayoutChanged -= value;
    }

    // The layout and the splitter follow dotnet/winforms' SplitContainer (MIT, decision 180): a working distance
    // (_splitterDistance, what the panels are laid out at and what a drag moves) beside the committed one
    // (_splitDistance, what SplitterDistance returns); the ratio of the width (height) to Panel1 taken when the
    // distance is set - not on every resize, so shrinking the window and growing it back puts the splitter back;
    // and Panel1MinSize, Panel2MinSize and SplitterWidth held back between BeginInit and EndInit.

    private readonly SplitterPanel _panel1;
    private readonly SplitterPanel _panel2;
    private Orientation _orientation = Orientation.Vertical;
    private BorderStyle _borderStyle = BorderStyle.None;
    private int _borderSize;
    private FixedPanel _fixedPanel = FixedPanel.None;
    private int _panel1MinSize = 25;
    private int _newPanel1MinSize = 25;
    private int _panel2MinSize = 25;
    private int _newPanel2MinSize = 25;
    private int _panelSize;
    private int _splitterDistance = 50;
    private int _splitDistance = 50;
    private int _splitterWidth = 4;
    private int _newSplitterWidth = 4;
    private int _splitterIncrement = 1;
    private double _ratioWidth;
    private double _ratioHeight;
    private bool _resizeCalled;
    private bool _setSplitterDistance;
    private bool _initializing;
    private bool _panel1Collapsed;
    private bool _panel2Collapsed;
    // A drag of the splitter: where it started, the distance then; the bar shows where it goes until it is let go.
    private bool _splitterClick;
    private bool _splitterDrag;
    private Point _anchor;
    private int _initialSplitterDistance;
    private bool _hot;

    public SplitContainer()
    {
        // Not selectable, but TabStop stays true, as in WinForms.
        SetStyle(ControlStyles.Selectable | ControlStyles.StandardClick, false);
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        _panel1 = new SplitterPanel(this) { Name = "Panel1" };
        _panel2 = new SplitterPanel(this) { Name = "Panel2" };
        base.Controls.Add(_panel1);
        base.Controls.Add(_panel2);
        UpdateSplitter();
    }

    protected override Size DefaultSize => new Size(150, 100);

    /// <summary>Unlike other containers, the SplitContainer shares its parent's binding context (WinForms).</summary>
    [Browsable(false)]
    [Description("The binding manager for the container control.  This manages all bindings of child controls to lists.")]
    public override BindingContext? BindingContext
    {
        get => BindingContextInternal;
        set => BindingContextInternal = value;
    }

    [Category("Behavior")]
    [Description("Occurs when the splitter is being moved.")]
    public event SplitterCancelEventHandler? SplitterMoving;

    [Category("Behavior")]
    [Description("Occurs when the splitter is done being moved.")]
    public event SplitterEventHandler? SplitterMoved;

    [Category("Appearance")]
    [Description("The Left or Top panel in the SplitContainer.")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public SplitterPanel Panel1 => _panel1;

    [Category("Appearance")]
    [Description("The Right or Bottom panel in the SplitContainer.")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public SplitterPanel Panel2 => _panel2;

    [Category("Behavior")]
    [Description("Determines if the splitter is vertical or horizontal.")]
    [DefaultValue(Orientation.Vertical)]
    [Localizable(true)]
    public Orientation Orientation
    {
        get => _orientation;
        set
        {
            if (!Enum.IsDefined(value)) throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(Orientation));
            if (_orientation == value) return;
            _orientation = value;
            // The distance is checked against the new direction.
            _splitDistance = 0;
            SplitterDistance = _splitterDistance;
            UpdateSplitter();
        }
    }

    [Category("Layout")]
    [Description("Determines pixel distance of the splitter from the left or top edge.")]
    [DefaultValue(50)]
    [Localizable(true)]
    [Bindable(true)]
    public int SplitterDistance
    {
        get => _splitDistance;
        set
        {
            if (value == _splitDistance) return;
            ArgumentOutOfRangeException.ThrowIfNegative(value, nameof(SplitterDistance));
            try
            {
                _setSplitterDistance = true;
                int extent = Extent;
                if (value < _panel1MinSize) value = _panel1MinSize;
                if (value + SplitterWidthInternal > extent - _panel2MinSize) value = extent - _panel2MinSize - SplitterWidthInternal;
                if (value < 0) throw new InvalidOperationException("SplitterDistance must be between Panel1MinSize and Width - Panel2MinSize.");
                _splitDistance = value;
                _splitterDistance = value;
                switch (_fixedPanel)
                {
                    case FixedPanel.Panel1:
                        _panelSize = value;
                        break;
                    case FixedPanel.Panel2:
                        _panelSize = extent - value - SplitterWidthInternal;
                        break;
                }
                UpdateSplitter();
            }
            finally
            {
                _setSplitterDistance = false;
            }
            var r = SplitterRectangle;
            OnSplitterMoved(new SplitterEventArgs(r.X + r.Width / 2, r.Y + r.Height / 2, r.X, r.Y));
        }
    }

    [Category("Layout")]
    [Description("Determines the thickness of the splitter.")]
    [DefaultValue(4)]
    [Localizable(true)]
    public int SplitterWidth
    {
        get => _splitterWidth;
        set
        {
            _newSplitterWidth = value;
            if (value != _splitterWidth && !_initializing) ApplySplitterWidth(value);
        }
    }

    [Category("Layout")]
    [Description("Determines the number of pixels the splitter moves in increments. Default is 1.")]
    [DefaultValue(1)]
    [Localizable(true)]
    public int SplitterIncrement
    {
        get => _splitterIncrement;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _splitterIncrement = value;
        }
    }

    [Category("Layout")]
    [Description("Determines the rectangle bounds of the splitter.")]
    [Browsable(false)]
    public Rectangle SplitterRectangle => SplitterRectangleAt(_splitterDistance, 0);

    [Category("Layout")]
    [Description("Determines the minimum distance of pixels of the splitter from the left or the top edge of Panel1.")]
    [DefaultValue(25)]
    [Localizable(true)]
    public int Panel1MinSize
    {
        get => _panel1MinSize;
        set
        {
            _newPanel1MinSize = value;
            if (value != _panel1MinSize && !_initializing) ApplyPanel1MinSize(value);
        }
    }

    [Category("Layout")]
    [Description("Determines the minimum distance of pixels of the splitter from the right or the bottom edge of Panel2.")]
    [DefaultValue(25)]
    [Localizable(true)]
    public int Panel2MinSize
    {
        get => _panel2MinSize;
        set
        {
            _newPanel2MinSize = value;
            if (value != _panel2MinSize && !_initializing) ApplyPanel2MinSize(value);
        }
    }

    [Category("Layout")]
    [Description("Indicates that a particular SplitContainer's Panel should remain fixed in size during resize events.")]
    [DefaultValue(FixedPanel.None)]
    public FixedPanel FixedPanel
    {
        get => _fixedPanel;
        set
        {
            if (!Enum.IsDefined(value)) throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(FixedPanel));
            if (_fixedPanel == value) return;
            _fixedPanel = value;
            _panelSize = value == FixedPanel.Panel2 ? Extent - _splitterDistance - SplitterWidthInternal : _splitterDistance;
        }
    }

    [Category("Layout")]
    [Description("Determines if the splitter can move.")]
    [DefaultValue(false)]
    [Localizable(true)]
    public bool IsSplitterFixed { get; set; }

    [Category("Layout")]
    [Description("This determines if Panel1 is collapsed.")]
    [DefaultValue(false)]
    public bool Panel1Collapsed
    {
        get => _panel1Collapsed;
        set
        {
            if (_panel1Collapsed == value) return;
            if (value && _panel2Collapsed) _panel2Collapsed = false;
            _panel1Collapsed = value;
            UpdateSplitter();
            Invalidate();
        }
    }

    [Category("Layout")]
    [Description("This determines if Panel2 is collapsed.")]
    [DefaultValue(false)]
    public bool Panel2Collapsed
    {
        get => _panel2Collapsed;
        set
        {
            if (_panel2Collapsed == value) return;
            if (value && _panel1Collapsed) _panel1Collapsed = false;
            _panel2Collapsed = value;
            UpdateSplitter();
            Invalidate();
        }
    }

    /// <summary>
    /// The border is drawn by the panels (WinForms' SetInnerMostBorder): each gets this style - a SplitContainer
    /// filling a panel with the same style takes it over, so nested ones show one line between them.
    /// </summary>
    [Category("Appearance")]
    [Description("The border type of the control.")]
    [DefaultValue(BorderStyle.None)]
    public BorderStyle BorderStyle
    {
        get => _borderStyle;
        set
        {
            if (!Enum.IsDefined(value)) throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(BorderStyle));
            if (_borderStyle != value)
            {
                _borderStyle = value;
                Invalidate();
                SetInnerMostBorder(this);
                if (Parent is SplitterPanel { Owner: var outer }) outer.SetInnerMostBorder(outer);
            }
            _borderSize = value switch { BorderStyle.FixedSingle => 1, BorderStyle.Fixed3D => 4, _ => 0 };
        }
    }

    public void BeginInit() => _initializing = true;

    public void EndInit()
    {
        _initializing = false;
        if (_newPanel1MinSize != _panel1MinSize) ApplyPanel1MinSize(_newPanel1MinSize);
        if (_newPanel2MinSize != _panel2MinSize) ApplyPanel2MinSize(_newPanel2MinSize);
        if (_newSplitterWidth != _splitterWidth) ApplySplitterWidth(_newSplitterWidth);
    }

    private int Extent => _orientation == Orientation.Vertical ? Width : Height;

    private bool CollapsedMode => _panel1Collapsed || _panel2Collapsed;

    private int SplitterWidthInternal => CollapsedMode ? 0 : _splitterWidth;

    private Rectangle SplitterRectangleAt(int distance, int minWeight)
    {
        int thickness = Math.Max(SplitterWidthInternal, minWeight);
        if (_orientation == Orientation.Vertical)
        {
            int x = RightToLeft == RightToLeft.Yes ? Width - distance - SplitterWidthInternal : distance;
            return new Rectangle(x, 0, thickness, Height);
        }
        return new Rectangle(0, distance, Width, thickness);
    }

    private void ApplyPanel1MinSize(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        // While designed, a value the control cannot keep is refused (VS shows the error).
        if (DesignMode && Extent != (_orientation == Orientation.Vertical ? DefaultSize.Width : DefaultSize.Height) && value + _panel2MinSize + _splitterWidth > Extent)
            throw new ArgumentOutOfRangeException(nameof(value), value, $"'{value}' is not a valid value for 'Panel1MinSize'.");
        _panel1MinSize = value;
        if (value > _splitterDistance) SplitterDistance = value;
    }

    private void ApplyPanel2MinSize(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        if (DesignMode && Extent != (_orientation == Orientation.Vertical ? DefaultSize.Width : DefaultSize.Height) && value + _panel1MinSize + _splitterWidth > Extent)
            throw new ArgumentOutOfRangeException(nameof(value), value, $"'{value}' is not a valid value for 'Panel2MinSize'.");
        _panel2MinSize = value;
        int panel2 = _orientation == Orientation.Vertical ? _panel2.Width : _panel2.Height;
        if (value > panel2)
        {
            // The splitter goes back far enough for Panel2 to have its minimum.
            _splitDistance = -1;
            SplitterDistance = Math.Max(0, Extent - value - SplitterWidthInternal);
        }
    }

    private void ApplySplitterWidth(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        if (DesignMode && value + _panel1MinSize + _panel2MinSize > Extent)
            throw new ArgumentOutOfRangeException(nameof(value), value, $"'{value}' is not a valid value for 'SplitterWidth'.");
        _splitterWidth = value;
        UpdateSplitter();
    }

    /// <summary>Lays the panels out at the working distance; set from code (not a resize), it also takes the ratio a resize keeps.</summary>
    private void UpdateSplitter()
    {
        int w = Width, h = Height;
        if (_panel1Collapsed || _panel2Collapsed)
        {
            var shown = _panel1Collapsed ? _panel2 : _panel1;
            var hidden = _panel1Collapsed ? _panel1 : _panel2;
            hidden.VisibleOwn = false;
            shown.VisibleOwn = true;
            shown.SetBoundsFromLayout(new Rectangle(0, 0, w, h));
            if (!_resizeCalled && _splitterDistance > 0)
            {
                if (_orientation == Orientation.Vertical) _ratioWidth = w / (double)_splitterDistance;
                else _ratioHeight = h / (double)_splitterDistance;
            }
            PerformLayoutOfPanels();
            Invalidate();
            return;
        }
        _panel1.VisibleOwn = true;
        _panel2.VisibleOwn = true;
        int d = Math.Max(0, _splitterDistance);
        int sw = SplitterWidthInternal;
        if (_orientation == Orientation.Vertical)
        {
            bool rtl = RightToLeft == RightToLeft.Yes;
            _panel1.SetBoundsFromLayout(new Rectangle(rtl ? w - d : 0, 0, d, h));
            _panel2.SetBoundsFromLayout(new Rectangle(rtl ? 0 : d + sw, 0, Math.Max(0, w - d - sw), h));
            if (!_resizeCalled && d > 0) _ratioWidth = w / (double)d;
        }
        else
        {
            _panel1.SetBoundsFromLayout(new Rectangle(0, 0, w, d));
            _panel2.SetBoundsFromLayout(new Rectangle(0, d + sw, w, Math.Max(0, h - d - sw)));
            if (!_resizeCalled && d > 0) _ratioHeight = h / (double)d;
        }
        PerformLayoutOfPanels();
        Invalidate();
    }

    private void PerformLayoutOfPanels()
    {
        _panel1.PerformLayout();
        _panel2.PerformLayout();
    }

    /// <summary>
    /// After a resize: a fixed panel keeps its size; with none fixed the splitter keeps its ratio (WinForms'
    /// ResizeSplitContainer). Panel2 never goes below its minimum, Panel1 never below its own.
    /// </summary>
    private void ResizeSplitContainer()
    {
        if (Width == 0 || Height == 0 || CollapsedMode)
        {
            UpdateSplitterOnResize();
            return;
        }
        int extent = Extent;
        switch (_fixedPanel)
        {
            case FixedPanel.Panel1:
                _splitterDistance = _panelSize;
                break;
            case FixedPanel.Panel2:
                _splitterDistance = Math.Max(extent - _panelSize - SplitterWidthInternal, _panel1MinSize);
                break;
            default:
                double ratio = _orientation == Orientation.Vertical ? _ratioWidth : _ratioHeight;
                // (A hair over, so 800 / (800 / 266.0) is 266 and not 265: an unchanged size moves nothing.)
                if (ratio != 0.0) _splitterDistance = Math.Max((int)Math.Floor(extent / ratio + 1e-9), _panel1MinSize);
                break;
        }
        UpdateSplitterOnResize();
        // The committed distance follows, through the setter's limits (WinForms' ApplySplitterDistance).
        try
        {
            _resizeCalled = true;
            _setSplitterDistance = true;
            int wanted = _splitterDistance;
            if (wanted != _splitDistance)
            {
                int value = Math.Max(wanted, _panel1MinSize);
                if (value + SplitterWidthInternal > extent - _panel2MinSize) value = extent - _panel2MinSize - SplitterWidthInternal;
                if (value >= 0)
                {
                    _splitDistance = value;
                    _splitterDistance = value;
                    UpdateSplitter();
                }
            }
        }
        finally
        {
            _resizeCalled = false;
            _setSplitterDistance = false;
        }
    }

    private void UpdateSplitterOnResize()
    {
        _resizeCalled = true;
        try { UpdateSplitter(); }
        finally { _resizeCalled = false; }
    }

    internal void SetInnerMostBorder(SplitContainer sc)
    {
        foreach (Control control in sc.Controls)
        {
            if (control is not SplitterPanel panel) continue;
            bool foundChild = false;
            foreach (Control child in panel.Controls)
            {
                if (child is SplitContainer inner && inner.Dock == DockStyle.Fill)
                {
                    if (inner.BorderStyle != BorderStyle) break;
                    panel.BorderStyle = BorderStyle.None;
                    SetInnerMostBorder(inner);
                    foundChild = true;
                }
            }
            if (!foundChild) panel.BorderStyle = BorderStyle;
        }
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        SetInnerMostBorder(this);
        if (!_setSplitterDistance) ResizeSplitContainer();
        base.OnLayout(levent);
    }

    /// <summary>It cannot be made smaller than its two minimum panel sizes and the splitter (WinForms).</summary>
    protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
    {
        int least = _panel1MinSize + SplitterWidthInternal + _panel2MinSize;
        if ((specified & BoundsSpecified.Height) != 0 && _orientation == Orientation.Horizontal && height < least) height = least;
        if ((specified & BoundsSpecified.Width) != 0 && _orientation == Orientation.Vertical && width < least) width = least;
        base.SetBoundsCore(x, y, width, height, specified);
    }

    protected override void OnRightToLeftChanged(EventArgs e)
    {
        base.OnRightToLeftChanged(e);
        UpdateSplitter();
    }

    public void OnSplitterMoving(SplitterCancelEventArgs e) => SplitterMoving?.Invoke(this, e);

    public void OnSplitterMoved(SplitterEventArgs e) => SplitterMoved?.Invoke(this, e);

    private bool IsSplitterMovable => !IsSplitterFixed && !CollapsedMode;

    /// <summary>The distance a drag from <see cref="_anchor"/> to (x, y) gives, within the panels' minimums.</summary>
    private int GetSplitterDistance(int x, int y)
    {
        int delta = _orientation == Orientation.Vertical ? x - _anchor.X : y - _anchor.Y;
        int size = _orientation == Orientation.Vertical
            ? Math.Max(RightToLeft == RightToLeft.Yes ? _panel1.Width - delta : _panel1.Width + delta, _borderSize)
            : Math.Max(_panel1.Height + delta, _borderSize);
        return Math.Max(Math.Min(size, Extent - _panel2MinSize), _panel1MinSize);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!IsSplitterMovable || !SplitterRectangle.Contains(e.Location) || !Enabled) return;
        if (e.Button == MouseButtons.Left && e.Clicks == 1)
        {
            _anchor = e.Location;
            _splitterDistance = GetSplitterDistance(e.X, e.Y);
            _initialSplitterDistance = _splitterDistance;
            _splitterClick = true;
            Capture = true;
            Invalidate();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!IsSplitterMovable) return;
        bool hot = SplitterRectangle.Contains(e.Location);
        Cursor = hot || _splitterClick ? (_orientation == Orientation.Vertical ? Cursors.VSplit : Cursors.HSplit) : Cursors.Default;
        if (hot != _hot)
        {
            _hot = hot;
            Invalidate(SplitterRectangle);
        }
        if (!_splitterClick) return;
        if ((e.Button & MouseButtons.Left) == 0)
        {
            SplitEnd(false);
            return;
        }
        _splitterDrag = true;
        int size = GetSplitterDistance(e.X, e.Y);
        int mod = (size - _initialSplitterDistance) % _splitterIncrement;
        if (size + SplitterWidthInternal <= Extent - _panel2MinSize - _borderSize) _splitterDistance = size - mod;
        var line = SplitterRectangleAt(_splitterDistance, 0);
        var args = new SplitterCancelEventArgs(e.X, e.Y, line.X, line.Y);
        OnSplitterMoving(args);
        if (args.Cancel) SplitEnd(false);
        // The bar where the splitter goes; the panels move when it is let go (WinForms).
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!_splitterClick) return;
        Capture = false;
        SplitEnd(_splitterDrag);
    }

    private void SplitEnd(bool accept)
    {
        bool wasDragging = _splitterClick;
        _splitterClick = false;
        _splitterDrag = false;
        if (!wasDragging) return;
        if (accept) SplitterDistance = _splitterDistance;
        else _splitterDistance = _splitDistance;
        _anchor = Point.Empty;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hot = false;
        if (!_splitterClick) Cursor = Cursors.Default;
        Invalidate(SplitterRectangle);
        base.OnMouseLeave(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (!CollapsedMode)
        {
            var bar = SplitterRectangleAt(_splitDistance, 0);
            using var brush = new SolidBrush(_hot && !_splitterClick ? Theme.ScrollThumbHot : BackColor);
            e.Graphics.FillRectangle(brush, bar);
        }
        base.OnPaint(e);
    }

    /// <summary>While the splitter is dragged: the bar where it will go, over the panels (WinForms draws it inverted).</summary>
    internal override void OnPaintOverlay(Graphics g)
    {
        base.OnPaintOverlay(g);
        if (!_splitterDrag) return;
        using var brush = new SolidBrush(Color.FromArgb(128, Theme.ScrollThumbPressed));
        g.FillRectangle(brush, SplitterRectangleAt(_splitterDistance, 3));
    }

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Category("Layout")]
    [DefaultValue(false)]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override bool AutoScroll { get => base.AutoScroll; set => base.AutoScroll = value; }

    [Category("Layout")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new Size AutoScrollMargin { get => base.AutoScrollMargin; set => base.AutoScrollMargin = value; }

    [Category("Layout")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new Size AutoScrollMinSize { get => base.AutoScrollMinSize; set => base.AutoScrollMinSize = value; }

    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new ControlCollection Controls => base.Controls;

    [Category("Layout")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new Padding Padding { get => base.Padding; set => base.Padding = value; }

    [Category("Appearance")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override string Text { get => base.Text; set => base.Text = value; }

    [Category("Behavior")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event ControlEventHandler? ControlAdded
    {
        add => base.ControlAdded += value;
        remove => base.ControlAdded -= value;
    }

    [Category("Behavior")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event ControlEventHandler? ControlRemoved
    {
        add => base.ControlRemoved += value;
        remove => base.ControlRemoved -= value;
    }

    [Category("Layout")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? PaddingChanged
    {
        add => base.PaddingChanged += value;
        remove => base.PaddingChanged -= value;
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

}
