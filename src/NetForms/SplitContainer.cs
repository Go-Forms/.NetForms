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
    internal SplitterPanel(SplitContainer owner) => Owner = owner;

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

    private readonly SplitterPanel _panel1;
    private readonly SplitterPanel _panel2;
    private Orientation _orientation = Orientation.Vertical;
    private int _splitterDistance = 50;
    private int _splitterWidth = 4;
    private int _panel1MinSize = 25;
    private int _panel2MinSize = 25;
    private FixedPanel _fixedPanel = FixedPanel.None;
    private bool _isSplitterFixed;
    private bool _panel1Collapsed;
    private bool _panel2Collapsed;
    private bool _dragging;
    private int _dragOffset;
    private int _lastExtent;
    private bool _hot;

    public SplitContainer()
    {
        // Not selectable, but TabStop stays true, as in WinForms.
        SetStyle(ControlStyles.Selectable | ControlStyles.StandardClick, false);
        SetStyle(ControlStyles.ResizeRedraw, true);
        _panel1 = new SplitterPanel(this) { Name = "Panel1" };
        _panel2 = new SplitterPanel(this) { Name = "Panel2" };
        base.Controls.Add(_panel1);
        base.Controls.Add(_panel2);
        _lastExtent = Extent;
        LayoutPanels();
    }

    protected override Size DefaultSize => new Size(150, 100);

    /// <summary>Unlike other containers, the SplitContainer shares its parent's binding context (WinForms).</summary>
    [Browsable(false)]
    [Description("The binding manager for the container control.")]
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
            if (_orientation == value) return;
            _orientation = value;
            _lastExtent = Extent;
            _splitterDistance = Math.Clamp(_splitterDistance, _panel1MinSize, Math.Max(_panel1MinSize, Extent - _splitterWidth - _panel2MinSize));
            LayoutPanels();
            Invalidate();
        }
    }

    [Category("Layout")]
    [Description("Determines pixel distance of the splitter from the left or top edge.")]
    [DefaultValue(50)]
    [Localizable(true)]
    public int SplitterDistance
    {
        get => _splitterDistance;
        set
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            value = ClampDistance(value);
            if (_splitterDistance == value) return;
            _splitterDistance = value;
            LayoutPanels();
            Invalidate();
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
            if (value < 1) throw new ArgumentOutOfRangeException(nameof(value));
            _splitterWidth = value;
            LayoutPanels();
            Invalidate();
        }
    }

    [Category("Layout")]
    [Description("Determines the number of pixels the splitter moves in increments. Default is 1.")]
    [DefaultValue(1)]
    [Localizable(true)]
    public int SplitterIncrement { get; set; } = 1;

    [Category("Layout")]
    [Description("Determines the rectangle bounds of the splitter.")]
    [Browsable(false)]
    public Rectangle SplitterRectangle => _orientation == Orientation.Vertical
        ? new Rectangle(_splitterDistance, 0, _splitterWidth, Height)
        : new Rectangle(0, _splitterDistance, Width, _splitterWidth);

    [Category("Layout")]
    [Description("Determines the minimum distance of pixels of the splitter from the left or the top edge of Panel1.")]
    [DefaultValue(25)]
    [Localizable(true)]
    public int Panel1MinSize
    {
        get => _panel1MinSize;
        set
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            _panel1MinSize = value;
            SplitterDistance = _splitterDistance;
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
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            _panel2MinSize = value;
            SplitterDistance = _splitterDistance;
        }
    }

    [Category("Layout")]
    [Description("Indicates that a particular SplitContainer's Panel should remain fixed in size during resize events.")]
    [DefaultValue(FixedPanel.None)]
    public FixedPanel FixedPanel
    {
        get => _fixedPanel;
        set => _fixedPanel = value;
    }

    [Category("Layout")]
    [Description("Determines if the splitter can move.")]
    [DefaultValue(false)]
    [Localizable(true)]
    public bool IsSplitterFixed
    {
        get => _isSplitterFixed;
        set => _isSplitterFixed = value;
    }

    [Category("Layout")]
    [Description("This determines if Panel1 is collapsed.")]
    [DefaultValue(false)]
    public bool Panel1Collapsed
    {
        get => _panel1Collapsed;
        set
        {
            if (_panel1Collapsed == value) return;
            _panel1Collapsed = value;
            if (value) _panel2Collapsed = false;
            LayoutPanels();
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
            _panel2Collapsed = value;
            if (value) _panel1Collapsed = false;
            LayoutPanels();
            Invalidate();
        }
    }

    [Category("Appearance")]
    [Description("The border type of the control.")]
    [DefaultValue(BorderStyle.None)]
    public BorderStyle BorderStyle { get; set; } = BorderStyle.None;

    private int Extent => _orientation == Orientation.Vertical ? Width : Height;

    private int ClampDistance(int value)
    {
        int max = Extent - _splitterWidth - _panel2MinSize;
        if (max < _panel1MinSize) return Math.Max(0, Math.Min(value, Extent - _splitterWidth));
        return Math.Clamp(value, _panel1MinSize, max);
    }

    private void LayoutPanels()
    {
        int w = Width, h = Height;
        if (_panel1Collapsed)
        {
            _panel1.SetBoundsFromLayout(Rectangle.Empty);
            _panel1.VisibleOwn = false;
            _panel2.VisibleOwn = true;
            _panel2.SetBoundsFromLayout(new Rectangle(0, 0, w, h));
            return;
        }
        if (_panel2Collapsed)
        {
            _panel2.SetBoundsFromLayout(Rectangle.Empty);
            _panel2.VisibleOwn = false;
            _panel1.VisibleOwn = true;
            _panel1.SetBoundsFromLayout(new Rectangle(0, 0, w, h));
            return;
        }
        _panel1.VisibleOwn = true;
        _panel2.VisibleOwn = true;
        int d = _splitterDistance;
        if (_orientation == Orientation.Vertical)
        {
            _panel1.SetBoundsFromLayout(new Rectangle(0, 0, Math.Max(0, d), h));
            _panel2.SetBoundsFromLayout(new Rectangle(d + _splitterWidth, 0, Math.Max(0, w - d - _splitterWidth), h));
        }
        else
        {
            _panel1.SetBoundsFromLayout(new Rectangle(0, 0, w, Math.Max(0, d)));
            _panel2.SetBoundsFromLayout(new Rectangle(0, d + _splitterWidth, w, Math.Max(0, h - d - _splitterWidth)));
        }
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        LayoutPanels();
        base.OnLayout(levent);
    }

    protected override void OnResize(EventArgs e)
    {
        int extent = Extent;
        int delta = extent - _lastExtent;
        _lastExtent = extent;
        if (delta != 0)
        {
            // FixedPanel decides who absorbs the change: Panel2 fixed → Panel1 grows; None → proportional.
            switch (_fixedPanel)
            {
                case FixedPanel.Panel2:
                    _splitterDistance = ClampDistance(_splitterDistance + delta);
                    break;
                case FixedPanel.Panel1:
                    _splitterDistance = ClampDistance(_splitterDistance);
                    break;
                default:
                    if (extent - delta > 0)
                    {
                        _splitterDistance = ClampDistance((int)Math.Round((double)_splitterDistance * extent / (extent - delta)));
                    }
                    break;
            }
        }
        base.OnResize(e);
    }

    protected virtual void OnSplitterMoving(SplitterCancelEventArgs e) => SplitterMoving?.Invoke(this, e);
    protected virtual void OnSplitterMoved(SplitterEventArgs e) => SplitterMoved?.Invoke(this, e);

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && !_isSplitterFixed && !_panel1Collapsed && !_panel2Collapsed && SplitterRectangle.Contains(e.Location))
        {
            _dragging = true;
            _dragOffset = (_orientation == Orientation.Vertical ? e.X : e.Y) - _splitterDistance;
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        bool hot = SplitterRectangle.Contains(e.Location) && !_isSplitterFixed;
        Cursor = hot || _dragging ? (_orientation == Orientation.Vertical ? Cursors.VSplit : Cursors.HSplit) : Cursors.Default;
        if (hot != _hot)
        {
            _hot = hot;
            Invalidate(SplitterRectangle);
        }
        if (_dragging && (e.Button & MouseButtons.Left) != 0)
        {
            int pos = (_orientation == Orientation.Vertical ? e.X : e.Y) - _dragOffset;
            pos = ClampDistance(pos);
            if (SplitterIncrement > 1) pos -= pos % SplitterIncrement;
            if (pos != _splitterDistance)
            {
                var args = new SplitterCancelEventArgs(e.X, e.Y, _orientation == Orientation.Vertical ? pos : e.X, _orientation == Orientation.Vertical ? e.Y : pos);
                OnSplitterMoving(args);
                if (!args.Cancel)
                {
                    _splitterDistance = ClampDistance(_orientation == Orientation.Vertical ? args.SplitX : args.SplitY);
                    LayoutPanels();
                    Invalidate();
                }
            }
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (_dragging)
        {
            _dragging = false;
            OnSplitterMoved(new SplitterEventArgs(e.X, e.Y, _orientation == Orientation.Vertical ? _splitterDistance : e.X, _orientation == Orientation.Vertical ? e.Y : _splitterDistance));
            Invalidate();
        }
        base.OnMouseUp(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hot = false;
        Cursor = Cursors.Default;
        Invalidate(SplitterRectangle);
        base.OnMouseLeave(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (!_panel1Collapsed && !_panel2Collapsed)
        {
            using var brush = new SolidBrush(_dragging ? Theme.ScrollThumbPressed : _hot ? Theme.ScrollThumbHot : BackColor);
            e.Graphics.FillRectangle(brush, SplitterRectangle);
        }
        if (BorderStyle != BorderStyle.None) Panel.PaintBorder(e.Graphics, ClientRectangle, BorderStyle);
        base.OnPaint(e);
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

    public void BeginInit() { }

    public void EndInit() { }
}
