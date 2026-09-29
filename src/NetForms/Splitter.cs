// Adapted from dotnet/winforms (src/System.Windows.Forms/System/Windows/Forms/Controls/Splitter/Splitter.cs).
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace System.Windows.Forms;

/// <summary>
/// The docked splitter of WinForms 1.0: docked to an edge next to another docked control (its target), it resizes
/// the target when dragged. While the mouse moves, a halftone bar shows where the split will go (Win32 inverts the
/// parent's pixels; here it is a bar painted above the parent's children); the target is resized on release, and
/// Escape cancels. The layout and events are WinForms': FindTarget, MinSize, MinExtra, SplitterMoving, SplitterMoved.
/// </summary>
[DefaultEvent(nameof(SplitterMoved))]
[DefaultProperty(nameof(Dock))]
[Description("Allows the user to resize docked controls.")]
public class Splitter : Control
{
    private const int DefaultWidth = 3;

    private BorderStyle _borderStyle = BorderStyle.None;
    private int _minSize = 25;
    private int _minExtra = 25;
    private Point _anchor = Point.Empty;
    private Control? _splitTarget;
    private int _splitSize = -1;
    private int _splitterThickness = 3;
    private int _initTargetSize;
    private int _maxSize;
    private SplitBar? _bar;

    public Splitter()
    {
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
        Dock = DockStyle.Left;
    }

    /// <summary>A splitter is docked; it has no anchors.</summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DefaultValue(AnchorStyles.None)]
    public override AnchorStyles Anchor
    {
        get => AnchorStyles.None;
        set { }
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override bool AllowDrop
    {
        get => base.AllowDrop;
        set => base.AllowDrop = value;
    }

    protected override Size DefaultSize => new(DefaultWidth, DefaultWidth);

    protected override ImeMode DefaultImeMode => ImeMode.Disable;

    protected override Cursor DefaultCursor => Dock switch
    {
        DockStyle.Top or DockStyle.Bottom => Cursors.HSplit,
        DockStyle.Left or DockStyle.Right => Cursors.VSplit,
        _ => base.DefaultCursor,
    };

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override Color ForeColor
    {
        get => base.ForeColor;
        set => base.ForeColor = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? ForeColorChanged
    {
        add => base.ForeColorChanged += value;
        remove => base.ForeColorChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override Image? BackgroundImage
    {
        get => base.BackgroundImage;
        set => base.BackgroundImage = value;
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

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override Font Font
    {
        get => base.Font;
        set => base.Font = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? FontChanged
    {
        add => base.FontChanged += value;
        remove => base.FontChanged -= value;
    }

    [DefaultValue(BorderStyle.None)]
    [Category("Appearance")]
    [Description("Controls the type of border the splitter has.")]
    public BorderStyle BorderStyle
    {
        get => _borderStyle;
        set
        {
            if (!Enum.IsDefined(value)) throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(BorderStyle));
            if (_borderStyle == value) return;
            _borderStyle = value;
            Invalidate();
        }
    }

    [Localizable(true)]
    [DefaultValue(DockStyle.Left)]
    public override DockStyle Dock
    {
        get => base.Dock;
        set
        {
            if (value is not (DockStyle.Top or DockStyle.Bottom or DockStyle.Left or DockStyle.Right))
            {
                throw new ArgumentException("A Splitter control must be docked left, right, top, or bottom.");
            }
            int requestedSize = _splitterThickness;
            base.Dock = value;
            switch (Dock)
            {
                case DockStyle.Top:
                case DockStyle.Bottom:
                    if (_splitterThickness != -1) Height = requestedSize;
                    break;
                case DockStyle.Left:
                case DockStyle.Right:
                    if (_splitterThickness != -1) Width = requestedSize;
                    break;
            }
        }
    }

    private bool Horizontal => Dock is DockStyle.Left or DockStyle.Right;

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new ImeMode ImeMode
    {
        get => base.ImeMode;
        set => base.ImeMode = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? ImeModeChanged
    {
        add => base.ImeModeChanged += value;
        remove => base.ImeModeChanged -= value;
    }

    /// <summary>The room the other controls keep: the target cannot grow into the last <c>MinExtra</c> pixels.</summary>
    [Category("Behavior")]
    [Localizable(true)]
    [DefaultValue(25)]
    [Description("Specifies the minimum size of the undocked area.")]
    public int MinExtra
    {
        get => _minExtra;
        set => _minExtra = Math.Max(0, value);
    }

    /// <summary>The smallest the target can be made.</summary>
    [Category("Behavior")]
    [Localizable(true)]
    [DefaultValue(25)]
    [Description("Specifies the minimum size of the control being resized.")]
    public int MinSize
    {
        get => _minSize;
        set => _minSize = Math.Max(0, value);
    }

    /// <summary>The size of the target (its width for a left or right splitter), or -1 without a target.</summary>
    [Category("Layout")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Description("The current position of the splitter, or -1 if it is not bound to a control.")]
    public int SplitPosition
    {
        get
        {
            if (_splitSize == -1) _splitSize = CalcSplitSize();
            return _splitSize;
        }
        set
        {
            var target = CalcSplitBounds();
            // Not an else-if: MinSize wins over the maximum.
            if (value > _maxSize) value = _maxSize;
            if (value < _minSize) value = _minSize;
            _splitSize = value;
            HideBar();
            if (target is null)
            {
                _splitSize = -1;
                return;
            }
            var bounds = target.Bounds;
            switch (Dock)
            {
                case DockStyle.Top:
                    bounds.Height = value;
                    break;
                case DockStyle.Bottom:
                    bounds.Y += bounds.Height - _splitSize;
                    bounds.Height = value;
                    break;
                case DockStyle.Left:
                    bounds.Width = value;
                    break;
                case DockStyle.Right:
                    bounds.X += bounds.Width - _splitSize;
                    bounds.Width = value;
                    break;
            }
            target.Bounds = bounds;
            OnSplitterMoved(new SplitterEventArgs(Left, Top, Left + bounds.Width / 2, Top + bounds.Height / 2));
        }
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new bool TabStop
    {
        get => base.TabStop;
        set => base.TabStop = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? TabStopChanged
    {
        add => base.TabStopChanged += value;
        remove => base.TabStopChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Bindable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override string Text
    {
        get => base.Text;
        set => base.Text = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? TextChanged
    {
        add => base.TextChanged += value;
        remove => base.TextChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? Enter
    {
        add => base.Enter += value;
        remove => base.Enter -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event KeyEventHandler? KeyUp
    {
        add => base.KeyUp += value;
        remove => base.KeyUp -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event KeyEventHandler? KeyDown
    {
        add => base.KeyDown += value;
        remove => base.KeyDown -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event KeyPressEventHandler? KeyPress
    {
        add => base.KeyPress += value;
        remove => base.KeyPress -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? Leave
    {
        add => base.Leave += value;
        remove => base.Leave -= value;
    }

    [Category("Behavior")]
    [Description("Occurs when the splitter is being moved.")]
    public event SplitterEventHandler? SplitterMoving;

    [Category("Behavior")]
    [Description("Occurs when the splitter is done being moved.")]
    public event SplitterEventHandler? SplitterMoved;

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var r = ClientRectangle;
        if (r.Width <= 0 || r.Height <= 0) return;
        switch (_borderStyle)
        {
            case BorderStyle.FixedSingle:
                e.Graphics.DrawRectangle(SystemPens.WindowFrame, r.X, r.Y, r.Width - 1, r.Height - 1);
                break;
            case BorderStyle.Fixed3D:
                ControlPaint.DrawBorder3D(e.Graphics, r, Border3DStyle.Sunken);
                break;
        }
    }

    /// <summary>Where the bar is drawn for a split of <paramref name="splitSize"/>, in the parent's client coordinates.</summary>
    private Rectangle CalcSplitLine(Control splitTarget, int splitSize, int minWeight)
    {
        Rectangle r = Bounds;
        Rectangle bounds = splitTarget.Bounds;
        switch (Dock)
        {
            case DockStyle.Top:
                if (r.Height < minWeight) r.Height = minWeight;
                r.Y = bounds.Y + splitSize;
                break;
            case DockStyle.Bottom:
                if (r.Height < minWeight) r.Height = minWeight;
                r.Y = bounds.Y + bounds.Height - splitSize - r.Height;
                break;
            case DockStyle.Left:
                if (r.Width < minWeight) r.Width = minWeight;
                r.X = bounds.X + splitSize;
                break;
            case DockStyle.Right:
                if (r.Width < minWeight) r.Width = minWeight;
                r.X = bounds.X + bounds.Width - splitSize - r.Width;
                break;
        }
        return r;
    }

    private int CalcSplitSize()
    {
        var target = FindTarget();
        if (target is null) return -1;
        return Dock switch
        {
            DockStyle.Top or DockStyle.Bottom => target.Height,
            DockStyle.Left or DockStyle.Right => target.Width,
            _ => -1,
        };
    }

    /// <summary>The target, and the largest it may become (<see cref="_maxSize"/>): the parent less the other docked controls and MinExtra.</summary>
    private Control? CalcSplitBounds()
    {
        var target = FindTarget();
        if (target is null) return null;
        _initTargetSize = target.Dock is DockStyle.Left or DockStyle.Right ? target.Width : target.Height;
        var parent = Parent;
        if (parent is null) return target;
        int dockWidth = 0, dockHeight = 0;
        foreach (Control ctl in parent.Controls)
        {
            if (ctl == target) continue;
            switch (ctl.Dock)
            {
                case DockStyle.Left:
                case DockStyle.Right:
                    dockWidth += ctl.Width;
                    break;
                case DockStyle.Top:
                case DockStyle.Bottom:
                    dockHeight += ctl.Height;
                    break;
            }
        }
        var clientSize = parent.ClientSize;
        _maxSize = Horizontal ? clientSize.Width - dockWidth - _minExtra : clientSize.Height - dockHeight - _minExtra;
        return target;
    }

    /// <summary>The sibling that touches the splitter on the side it is docked to.</summary>
    private Control? FindTarget()
    {
        var parent = Parent;
        if (parent is null) return null;
        foreach (Control target in parent.Controls)
        {
            if (target == this) continue;
            switch (Dock)
            {
                case DockStyle.Top when target.Bottom == Top:
                case DockStyle.Bottom when target.Top == Bottom:
                case DockStyle.Left when target.Right == Left:
                case DockStyle.Right when target.Left == Right:
                    return target;
            }
        }
        return null;
    }

    private int GetSplitSize(Control splitTarget, int x, int y)
    {
        int delta = Horizontal ? x - _anchor.X : y - _anchor.Y;
        int size = Dock switch
        {
            DockStyle.Top => splitTarget.Height + delta,
            DockStyle.Bottom => splitTarget.Height - delta,
            DockStyle.Left => splitTarget.Width + delta,
            DockStyle.Right => splitTarget.Width - delta,
            _ => 0,
        };
        return Math.Max(Math.Min(size, _maxSize), _minSize);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_splitTarget is not null && e.KeyCode == Keys.Escape) SplitEnd(false);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left && e.Clicks == 1) SplitBegin(e.X, e.Y);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_splitTarget is null) return;
        var r = CalcSplitLine(_splitTarget, GetSplitSize(_splitTarget, e.X, e.Y), 0);
        OnSplitterMoving(new SplitterEventArgs(e.X + Left, e.Y + Top, r.X, r.Y));
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_splitTarget is not null) SplitEnd(true);
    }

    protected virtual void OnSplitterMoving(SplitterEventArgs sevent)
    {
        SplitterMoving?.Invoke(this, sevent);
        if (_splitTarget is not null) SplitMove(_splitTarget, sevent.SplitX, sevent.SplitY);
    }

    protected virtual void OnSplitterMoved(SplitterEventArgs sevent)
    {
        SplitterMoved?.Invoke(this, sevent);
        if (_splitTarget is not null) SplitMove(_splitTarget, sevent.SplitX, sevent.SplitY);
    }

    protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
    {
        if (Horizontal)
        {
            if (width < 1) width = 3;
            _splitterThickness = width;
        }
        else
        {
            if (height < 1) height = 3;
            _splitterThickness = height;
        }
        base.SetBoundsCore(x, y, width, height, specified);
    }

    private void SplitBegin(int x, int y)
    {
        var target = CalcSplitBounds();
        if (target is null || _minSize >= _maxSize) return;
        _anchor = new Point(x, y);
        _splitTarget = target;
        _splitSize = GetSplitSize(target, x, y);
        Capture = true;
        ShowBar();
    }

    private void SplitEnd(bool accept)
    {
        HideBar();
        _splitTarget = null;
        Capture = false;
        if (accept) SplitPosition = _splitSize;
        else if (_splitSize != _initTargetSize) SplitPosition = _initTargetSize;
        _anchor = Point.Empty;
    }

    private void SplitMove(Control splitTarget, int x, int y)
    {
        int size = GetSplitSize(splitTarget, x - Left + _anchor.X, y - Top + _anchor.Y);
        if (_splitSize == size) return;
        _splitSize = size;
        ShowBar();
    }

    /// <summary>The halftone bar at the current split, above the parent's children (WinForms' DrawSplitBar).</summary>
    private void ShowBar()
    {
        var parent = Parent;
        if (_splitTarget is null || parent is null) return;
        var r = CalcSplitLine(_splitTarget, _splitSize, 3);
        if (_bar == null)
        {
            _bar = new SplitBar();
            parent.AddAdornment(_bar);
        }
        else
        {
            parent.Invalidate(_bar.Bounds);
        }
        _bar.SetBoundsFromLayout(r);
        parent.Invalidate(r);
    }

    private void HideBar()
    {
        if (_bar == null) return;
        _bar.Parent?.RemoveAdornment(_bar);
        _bar.Dispose();
        _bar = null;
    }

    /// <summary>True while the bar is shown (a drag is under way); for the tests.</summary>
    internal Rectangle? SplitBarBounds => _bar?.Bounds;

    public override string ToString() => $"{base.ToString()}, MinExtra: {MinExtra}, MinSize: {MinSize}";

    /// <summary>The drag feedback: Win32's halftone PATINVERT, drawn as a dark checkered bar.</summary>
    private sealed class SplitBar : Control
    {
        public SplitBar()
        {
            SetStyle(ControlStyles.Selectable, false);
        }

        internal override bool AdornmentContains(Point p) => false;

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            using var brush = new HatchBrush(HatchStyle.Percent50, Color.Black, Color.FromArgb(160, Color.White));
            e.Graphics.FillRectangle(brush, ClientRectangle);
        }
    }
}
