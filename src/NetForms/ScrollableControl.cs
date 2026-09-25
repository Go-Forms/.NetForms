using System.ComponentModel;
using System;
using System.Drawing;

namespace System.Windows.Forms;

/// <summary>
/// A container that can scroll its content. With AutoScroll on, the display rectangle is
/// the union of the client area and the children's extent, its origin moves with the scroll
/// position, and children are physically moved when scrolling - exactly the WinForms model,
/// so a scrolled child reports a negative Left/Top. The scroll bars are drawn inside the
/// client area, over the children.
/// </summary>
public partial class ScrollableControl : Control
{
    private bool _autoScroll;
    private Size _autoScrollMargin;
    private Size _autoScrollMinSize;
    private Point _scroll;
    private Size _content;
    private ScrollBarCore? _vbar;
    private ScrollBarCore? _hbar;
    private bool _vVisible;
    private bool _hVisible;
    private readonly HScrollProperties _horizontalScroll;
    private readonly VScrollProperties _verticalScroll;
    private bool _layingOut;

    public ScrollableControl()
    {
        SetStyle(ControlStyles.ContainerControl, true);
        _horizontalScroll = new HScrollProperties(this);
        _verticalScroll = new VScrollProperties(this);
    }

    [Category("Action")]
    [Description("Occurs when the user moves the scroll box.")]
    public event ScrollEventHandler? Scroll;

    [Category("Layout")]
    [Description("Indicates whether scroll bars automatically appear when the control contents are larger than its visible area.")]
    [DefaultValue(false)]
    [Localizable(true)]
    public virtual bool AutoScroll
    {
        get => _autoScroll;
        set
        {
            if (_autoScroll == value) return;
            _autoScroll = value;
            if (!value)
            {
                SetDisplayRectLocation(0, 0);
                _vVisible = _hVisible = false;
            }
            PerformLayout(this, nameof(AutoScroll));
            Invalidate();
        }
    }

    [Category("Layout")]
    [Description("The margin around controls during auto scroll.")]
    [Localizable(true)]
    public Size AutoScrollMargin
    {
        get => _autoScrollMargin;
        set
        {
            if (value.Width < 0 || value.Height < 0) throw new ArgumentOutOfRangeException(nameof(value));
            _autoScrollMargin = value;
            PerformLayout(this, nameof(AutoScrollMargin));
        }
    }

    [Category("Layout")]
    [Description("The minimum logical size for the auto scroll region.")]
    [Localizable(true)]
    public Size AutoScrollMinSize
    {
        get => _autoScrollMinSize;
        set
        {
            _autoScrollMinSize = value;
            if (!_autoScroll && !value.IsEmpty) _autoScroll = true;
            PerformLayout(this, nameof(AutoScrollMinSize));
        }
    }

    internal bool ShouldSerializeAutoScrollMargin() => _autoScrollMargin != Size.Empty;

    internal bool ShouldSerializeAutoScrollMinSize() => _autoScrollMinSize != Size.Empty;

    /// <summary>Negative of the scroll offset (WinForms' convention). Setting it scrolls to the absolute position given.</summary>
    [Category("Layout")]
    [Description("The current position of the auto-scrolling scroll bar.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Point AutoScrollPosition
    {
        get => new Point(-_scroll.X, -_scroll.Y);
        set
        {
            // As in WinForms (if (Created) ...): a control that is not created yet does not scroll -
            // set in a constructor it is ignored, in Load (the window exists) it works.
            if (IsHandleCreated) SetDisplayRectLocation(-Math.Abs(value.X), -Math.Abs(value.Y));
        }
    }

    [Category("Layout")]
    [Description("Gets the horizontal scroll bar for this ScrollableControl.")]
    [Browsable(false)]
    public HScrollProperties HorizontalScroll => _horizontalScroll;

    [Category("Layout")]
    [Description("Gets the vertical scroll bar for this ScrollableControl.")]
    [Browsable(false)]
    public VScrollProperties VerticalScroll => _verticalScroll;

    internal ScrollBarCore? VBar => _vbar;
    internal ScrollBarCore? HBar => _hbar;
    internal bool VBarVisible => _vVisible;
    internal bool HBarVisible => _hVisible;
    internal Point ScrollOffset => _scroll;

    /// <summary>The client area minus the visible scroll bars.</summary>
    internal Rectangle ScrollClientRectangle
    {
        get
        {
            var r = ClientRectangle;
            if (_vVisible) r.Width = Math.Max(0, r.Width - ScrollBarCore.Thickness);
            if (_hVisible) r.Height = Math.Max(0, r.Height - ScrollBarCore.Thickness);
            return r;
        }
    }

    [Description("Retrieves the display rectangle of this control.")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override Rectangle DisplayRectangle
    {
        get
        {
            // Scroll bars take client area only from a created control: one not created yet reports
            // the whole client area, as WinForms does (autoscroll/display). A designed control counts
            // as created, as on a VS design surface. The bars' own geometry is not affected.
            var r = IsHandleCreated || DesignMode ? ScrollClientRectangle : ClientRectangle;
            var p = Padding;
            var display = new Rectangle(r.X + p.Left, r.Y + p.Top, Math.Max(0, r.Width - p.Horizontal), Math.Max(0, r.Height - p.Vertical));
            if (!_autoScroll) return display;
            display.Width = Math.Max(display.Width, _content.Width - p.Horizontal);
            display.Height = Math.Max(display.Height, _content.Height - p.Vertical);
            display.Offset(-_scroll.X, -_scroll.Y);
            return display;
        }
    }

    // --- scrolling -----------------------------------------------------------------

    /// <summary>Scroll so the display rectangle's origin is at (x, y) (both ≤ 0): children move by the difference.</summary>
    protected void SetDisplayRectLocation(int x, int y)
    {
        var client = ScrollClientRectangle;
        int maxX = Math.Max(0, _content.Width - client.Width);
        int maxY = Math.Max(0, _content.Height - client.Height);
        int newX = Math.Clamp(-x, 0, maxX);
        int newY = Math.Clamp(-y, 0, maxY);
        int dx = _scroll.X - newX;
        int dy = _scroll.Y - newY;
        if (dx == 0 && dy == 0) return;
        _scroll = new Point(newX, newY);
        foreach (var child in Controls)
        {
            child.SetBoundsFromLayout(new Rectangle(child.Left + dx, child.Top + dy, child.Width, child.Height));
        }
        if (_vbar != null) _vbar.Value = newY;
        if (_hbar != null) _hbar.Value = newX;
        Invalidate();
    }

    public void ScrollControlIntoView(Control? activeControl)
    {
        if (!_autoScroll || activeControl == null || !Contains(activeControl)) return;
        // Through the virtual, as WinForms: a panel overriding ScrollToControl (the classic "stop jumping to
        // the focused control" fix returns DisplayRectangle.Location) decides where it scrolls.
        var location = ScrollToControl(activeControl);
        SetDisplayRectLocation(location.X, location.Y);
    }

    /// <summary>Where the display rectangle has to be for <paramref name="activeControl"/> to be in view.</summary>
    protected virtual Point ScrollToControl(Control activeControl)
    {
        ArgumentNullException.ThrowIfNull(activeControl);
        var bounds = activeControl.Bounds;
        for (var c = activeControl.Parent; c != null && c != this; c = c.Parent) bounds.Offset(c.Left, c.Top);
        var client = ScrollClientRectangle;
        int x = -_scroll.X, y = -_scroll.Y;
        if (bounds.Right > client.Right) x -= bounds.Right - client.Right;
        if (bounds.Left < client.Left) x += client.Left - bounds.Left;
        if (bounds.Bottom > client.Bottom) y -= bounds.Bottom - client.Bottom;
        if (bounds.Top < client.Top) y += client.Top - bounds.Top;
        return new Point(x, y);
    }

    protected virtual void OnScroll(ScrollEventArgs se) => Scroll?.Invoke(this, se);

    internal void ScrollByUser(int value, ScrollEventType type, bool vertical)
    {
        int old = vertical ? _scroll.Y : _scroll.X;
        var e = new ScrollEventArgs(type, old, value, vertical ? ScrollOrientation.VerticalScroll : ScrollOrientation.HorizontalScroll);
        OnScroll(e);
        if (vertical) SetDisplayRectLocation(-_scroll.X, -e.NewValue);
        else SetDisplayRectLocation(-e.NewValue, -_scroll.Y);
    }

    // --- layout --------------------------------------------------------------------

    protected override void OnLayout(LayoutEventArgs levent)
    {
        if (_layingOut)
        {
            base.OnLayout(levent);
            return;
        }
        _layingOut = true;
        try
        {
            base.OnLayout(levent);
            if (_autoScroll) UpdateScrollState(levent);
        }
        finally
        {
            _layingOut = false;
        }
    }

    private Size MeasureContent()
    {
        int right = 0, bottom = 0;
        foreach (var child in Controls)
        {
            if (!child.VisibleOwn) continue;
            // Docked and right/bottom anchored children follow the viewport; only the others define the extent.
            if (child.Dock != DockStyle.None) continue;
            var anchor = child.Anchor;
            if ((anchor & AnchorStyles.Left) != 0 || (anchor & (AnchorStyles.Left | AnchorStyles.Right)) == 0) right = Math.Max(right, child.Right + _scroll.X);
            if ((anchor & AnchorStyles.Top) != 0 || (anchor & (AnchorStyles.Top | AnchorStyles.Bottom)) == 0) bottom = Math.Max(bottom, child.Bottom + _scroll.Y);
        }
        var p = Padding;
        var size = new Size(right + _autoScrollMargin.Width + p.Right, bottom + _autoScrollMargin.Height + p.Bottom);
        return new Size(Math.Max(size.Width, _autoScrollMinSize.Width), Math.Max(size.Height, _autoScrollMinSize.Height));
    }

    private void UpdateScrollState(LayoutEventArgs levent)
    {
        var content = MeasureContent();
        var client = ClientRectangle;
        bool v = content.Height > client.Height;
        bool h = content.Width > client.Width - (v ? ScrollBarCore.Thickness : 0);
        if (h && !v) v = content.Height > client.Height - ScrollBarCore.Thickness;

        bool changed = v != _vVisible || h != _hVisible || content != _content;
        _content = content;
        _vVisible = v;
        _hVisible = h;

        if (v) _vbar ??= new ScrollBarCore(this, vertical: true, (value, type) => ScrollByUser(value, type, vertical: true));
        if (h) _hbar ??= new ScrollBarCore(this, vertical: false, (value, type) => ScrollByUser(value, type, vertical: false));

        var visible = ScrollClientRectangle;
        if (_vbar != null)
        {
            _vbar.Minimum = 0;
            _vbar.Maximum = Math.Max(0, content.Height - 1);
            _vbar.LargeChange = Math.Max(1, visible.Height);
            _vbar.SmallChange = Math.Max(1, visible.Height / 10);
            _vbar.Bounds = new Rectangle(client.Right - ScrollBarCore.Thickness, 0, ScrollBarCore.Thickness, visible.Height);
            _vbar.Enabled = Enabled;
        }
        if (_hbar != null)
        {
            _hbar.Minimum = 0;
            _hbar.Maximum = Math.Max(0, content.Width - 1);
            _hbar.LargeChange = Math.Max(1, visible.Width);
            _hbar.SmallChange = Math.Max(1, visible.Width / 10);
            _hbar.Bounds = new Rectangle(0, client.Bottom - ScrollBarCore.Thickness, visible.Width, ScrollBarCore.Thickness);
            _hbar.Enabled = Enabled;
        }

        // Content shrank or the viewport grew: pull the scroll position back into range.
        int maxX = Math.Max(0, content.Width - visible.Width);
        int maxY = Math.Max(0, content.Height - visible.Height);
        if (_scroll.X > maxX || _scroll.Y > maxY)
        {
            SetDisplayRectLocation(-Math.Min(_scroll.X, maxX), -Math.Min(_scroll.Y, maxY));
        }
        if (_vbar != null) _vbar.Value = _scroll.Y;
        if (_hbar != null) _hbar.Value = _scroll.X;

        if (changed)
        {
            // The display rectangle changed with the bars: lay the docked/anchored children out again.
            base.OnLayout(new LayoutEventArgs(this, "ScrollBars"));
            Invalidate();
        }
    }

    // --- input on the bars ---------------------------------------------------------

    internal override bool IsOverlayPoint(Point p) =>
        (_vVisible && _vbar != null && _vbar.Bounds.Contains(p)) || (_hVisible && _hbar != null && _hbar.Bounds.Contains(p));

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            if (_vVisible && _vbar != null && _vbar.Bounds.Contains(e.Location)) _vbar.MouseDown(e.Location);
            else if (_hVisible && _hbar != null && _hbar.Bounds.Contains(e.Location)) _hbar.MouseDown(e.Location);
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        bool left = (e.Button & MouseButtons.Left) != 0;
        if (_vVisible) _vbar?.MouseMove(e.Location, left);
        if (_hVisible) _hbar?.MouseMove(e.Location, left);
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            if (_vVisible) _vbar?.MouseUp(e.Location);
            if (_hVisible) _hbar?.MouseUp(e.Location);
        }
        base.OnMouseUp(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _vbar?.MouseLeave();
        _hbar?.MouseLeave();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (_autoScroll && _vVisible)
        {
            int notches = e.Delta / 120;
            if (notches == 0) notches = Math.Sign(e.Delta);
            int step = _vbar?.SmallChange * 3 ?? 30;
            ScrollByUser(Math.Clamp(_scroll.Y - notches * step, 0, Math.Max(0, _content.Height - ScrollClientRectangle.Height)), notches > 0 ? ScrollEventType.SmallDecrement : ScrollEventType.SmallIncrement, vertical: true);
        }
        base.OnMouseWheel(e);
    }

    internal override void OnPaintOverlay(Graphics g)
    {
        if (_vVisible && _vbar != null) _vbar.Paint(g);
        if (_hVisible && _hbar != null) _hbar.Paint(g);
        if (_vVisible && _hVisible)
        {
            using var corner = new SolidBrush(SystemColors.Control);
            g.FillRectangle(corner, new Rectangle(Width - ScrollBarCore.Thickness, Height - ScrollBarCore.Thickness, ScrollBarCore.Thickness, ScrollBarCore.Thickness));
        }
        base.OnPaintOverlay(g);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _vbar?.Dispose();
            _hbar?.Dispose();
        }
        base.Dispose(disposing);
    }
}

/// <summary>Read/write access to one of the container's scroll bars (WinForms' HorizontalScroll/VerticalScroll).</summary>
public abstract class ScrollProperties
{
    private readonly ScrollableControl _owner;
    private readonly bool _vertical;

    private protected ScrollProperties(ScrollableControl container, bool vertical)
    {
        _owner = container;
        _vertical = vertical;
    }

    private ScrollBarCore? Bar => _vertical ? _owner.VBar : _owner.HBar;

    public bool Enabled
    {
        get => Bar?.Enabled ?? false;
        set { if (Bar != null) Bar.Enabled = value; }
    }

    public bool Visible
    {
        get => _vertical ? _owner.VBarVisible : _owner.HBarVisible;
        set { }
    }

    public int Value
    {
        get => _vertical ? _owner.ScrollOffset.Y : _owner.ScrollOffset.X;
        set
        {
            var current = _owner.AutoScrollPosition;
            _owner.AutoScrollPosition = _vertical ? new Point(-current.X, value) : new Point(value, -current.Y);
        }
    }

    public int Minimum
    {
        get => Bar?.Minimum ?? 0;
        set { }
    }

    public int Maximum
    {
        get => Bar?.Maximum ?? 0;
        set { }
    }

    public int SmallChange
    {
        get => Bar?.SmallChange ?? 1;
        set { if (Bar != null) Bar.SmallChange = value; }
    }

    public int LargeChange
    {
        get => Bar?.LargeChange ?? 10;
        set { if (Bar != null) Bar.LargeChange = value; }
    }
}

public class HScrollProperties : ScrollProperties
{
    public HScrollProperties(ScrollableControl container) : base(container, vertical: false) { }
}

public class VScrollProperties : ScrollProperties
{
    public VScrollProperties(ScrollableControl container) : base(container, vertical: true) { }
}
