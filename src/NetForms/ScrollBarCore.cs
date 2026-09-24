using System;
using System.Drawing;

namespace System.Windows.Forms;

/// <summary>
/// The working part of a scroll bar - range, thumb geometry, painting and mouse handling
/// inside a rectangle - shared by the <see cref="ScrollBar"/> controls and by the bars that
/// ListBox, TextBox and ScrollableControl embed in their own client area (WinForms has them
/// in the non-client area; we draw them ourselves).
/// </summary>
internal sealed class ScrollBarCore
{
    public const int Thickness = 17;
    private const int MinThumb = 8;

    private readonly Control _owner;
    private readonly Action<int, ScrollEventType> _setValue;
    private int _minimum;
    private int _maximum = 100;
    private int _value;
    private int _smallChange = 1;
    private int _largeChange = 10;
    private Part _hot = Part.None;
    private Part _pressed = Part.None;
    private int _dragOffset;
    private Timer? _repeat;

    public ScrollBarCore(Control owner, bool vertical, Action<int, ScrollEventType> setValue)
    {
        _owner = owner;
        Vertical = vertical;
        _setValue = setValue;
    }

    private enum Part { None, ArrowBack, TrackBack, Thumb, TrackForward, ArrowForward }

    public bool Vertical { get; }
    public Rectangle Bounds { get; set; }
    public bool Enabled { get; set; } = true;

    public int Minimum
    {
        get => _minimum;
        set { _minimum = value; if (_maximum < value) _maximum = value; Clamp(); }
    }

    public int Maximum
    {
        get => _maximum;
        set { _maximum = value; if (_minimum > value) _minimum = value; Clamp(); }
    }

    public int Value
    {
        get => _value;
        set => _value = Math.Clamp(value, _minimum, MaxValue);
    }

    public int SmallChange
    {
        get => _smallChange;
        set => _smallChange = Math.Max(0, value);
    }

    public int LargeChange
    {
        get => _largeChange;
        set => _largeChange = Math.Max(0, value);
    }

    /// <summary>The largest reachable value: Maximum − LargeChange + 1, as in Win32.</summary>
    public int MaxValue => Math.Max(_minimum, _maximum - Math.Max(1, _largeChange) + 1);

    private void Clamp() => _value = Math.Clamp(_value, _minimum, MaxValue);

    private int Length => Vertical ? Bounds.Height : Bounds.Width;
    private int ArrowSize => Math.Min(Thickness, Length / 2);
    private int TrackLength => Math.Max(0, Length - 2 * ArrowSize);

    private int ThumbLength
    {
        get
        {
            int range = _maximum - _minimum + 1;
            if (range <= 0 || TrackLength <= 0) return 0;
            int len = (int)((long)TrackLength * Math.Max(1, _largeChange) / range);
            return Math.Clamp(len, Math.Min(MinThumb, TrackLength), TrackLength);
        }
    }

    private int ThumbOffset
    {
        get
        {
            int travel = TrackLength - ThumbLength;
            int range = MaxValue - _minimum;
            if (travel <= 0 || range <= 0) return 0;
            return (int)((long)travel * (_value - _minimum) / range);
        }
    }

    public bool IsScrollable => MaxValue > _minimum;

    public Rectangle ThumbRectangle
    {
        get
        {
            if (!IsScrollable) return Rectangle.Empty;
            int start = ArrowSize + ThumbOffset;
            return Vertical
                ? new Rectangle(Bounds.X, Bounds.Y + start, Bounds.Width, ThumbLength)
                : new Rectangle(Bounds.X + start, Bounds.Y, ThumbLength, Bounds.Height);
        }
    }

    private Rectangle ArrowBackRectangle => Vertical
        ? new Rectangle(Bounds.X, Bounds.Y, Bounds.Width, ArrowSize)
        : new Rectangle(Bounds.X, Bounds.Y, ArrowSize, Bounds.Height);

    private Rectangle ArrowForwardRectangle => Vertical
        ? new Rectangle(Bounds.X, Bounds.Bottom - ArrowSize, Bounds.Width, ArrowSize)
        : new Rectangle(Bounds.Right - ArrowSize, Bounds.Y, ArrowSize, Bounds.Height);

    private Part HitTest(Point p)
    {
        if (!Bounds.Contains(p)) return Part.None;
        if (ArrowBackRectangle.Contains(p)) return Part.ArrowBack;
        if (ArrowForwardRectangle.Contains(p)) return Part.ArrowForward;
        var thumb = ThumbRectangle;
        if (thumb.Contains(p)) return Part.Thumb;
        int pos = Vertical ? p.Y : p.X;
        int thumbStart = Vertical ? thumb.Y : thumb.X;
        return pos < thumbStart ? Part.TrackBack : Part.TrackForward;
    }

    // --- input ---------------------------------------------------------------------

    private void Change(int delta, ScrollEventType type)
    {
        int newValue = Math.Clamp(_value + delta, _minimum, MaxValue);
        if (newValue != _value) _setValue(newValue, type);
    }

    public void MouseDown(Point p)
    {
        if (!Enabled) return;
        var part = HitTest(p);
        _pressed = part;
        switch (part)
        {
            case Part.ArrowBack: Change(-_smallChange, ScrollEventType.SmallDecrement); StartRepeat(); break;
            case Part.ArrowForward: Change(_smallChange, ScrollEventType.SmallIncrement); StartRepeat(); break;
            case Part.TrackBack: Change(-_largeChange, ScrollEventType.LargeDecrement); StartRepeat(); break;
            case Part.TrackForward: Change(_largeChange, ScrollEventType.LargeIncrement); StartRepeat(); break;
            case Part.Thumb:
                {
                    var thumb = ThumbRectangle;
                    _dragOffset = Vertical ? p.Y - thumb.Y : p.X - thumb.X;
                    break;
                }
        }
        _owner.Invalidate(Bounds);
    }

    public void MouseMove(Point p, bool leftDown)
    {
        if (_pressed == Part.Thumb && leftDown)
        {
            int travel = TrackLength - ThumbLength;
            if (travel > 0)
            {
                int pos = (Vertical ? p.Y - Bounds.Y : p.X - Bounds.X) - ArrowSize - _dragOffset;
                int newValue = _minimum + (int)Math.Round((double)Math.Clamp(pos, 0, travel) * (MaxValue - _minimum) / travel);
                if (newValue != _value) _setValue(newValue, ScrollEventType.ThumbTrack);
            }
            return;
        }
        var hot = Enabled ? HitTest(p) : Part.None;
        if (hot != _hot)
        {
            _hot = hot;
            _owner.Invalidate(Bounds);
        }
    }

    public void MouseUp(Point p)
    {
        StopRepeat();
        if (_pressed == Part.Thumb) _setValue(_value, ScrollEventType.ThumbPosition);
        if (_pressed != Part.None) _setValue(_value, ScrollEventType.EndScroll);
        _pressed = Part.None;
        _hot = Enabled ? HitTest(p) : Part.None;
        _owner.Invalidate(Bounds);
    }

    public void MouseLeave()
    {
        if (_hot != Part.None)
        {
            _hot = Part.None;
            _owner.Invalidate(Bounds);
        }
    }

    public void MouseWheel(int delta)
    {
        if (!Enabled || delta == 0) return;
        int notches = delta / 120;
        if (notches == 0) notches = Math.Sign(delta);
        Change(-notches * _smallChange * 3, notches > 0 ? ScrollEventType.SmallDecrement : ScrollEventType.SmallIncrement);
    }

    private void StartRepeat()
    {
        _repeat ??= new Timer();
        _repeat.Tick -= RepeatTick;
        _repeat.Tick += RepeatTick;
        _repeat.Interval = 400;
        _repeat.Start();
    }

    private void RepeatTick(object? sender, EventArgs e)
    {
        _repeat!.Interval = 60;
        switch (_pressed)
        {
            case Part.ArrowBack: Change(-_smallChange, ScrollEventType.SmallDecrement); break;
            case Part.ArrowForward: Change(_smallChange, ScrollEventType.SmallIncrement); break;
            case Part.TrackBack: Change(-_largeChange, ScrollEventType.LargeDecrement); break;
            case Part.TrackForward: Change(_largeChange, ScrollEventType.LargeIncrement); break;
            default: StopRepeat(); break;
        }
    }

    private void StopRepeat() => _repeat?.Stop();

    public void Dispose() => _repeat?.Dispose();

    // --- painting ------------------------------------------------------------------

    public void Paint(Graphics g)
    {
        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        using (var track = new SolidBrush(Theme.ScrollTrack)) g.FillRectangle(track, bounds);

        PaintArrow(g, ArrowBackRectangle, Part.ArrowBack, backward: true);
        PaintArrow(g, ArrowForwardRectangle, Part.ArrowForward, backward: false);

        var thumb = ThumbRectangle;
        if (thumb.Width > 0 && thumb.Height > 0)
        {
            var color = !Enabled ? Theme.ScrollThumbDisabled
                : _pressed == Part.Thumb ? Theme.ScrollThumbPressed
                : _hot == Part.Thumb ? Theme.ScrollThumbHot
                : Theme.ScrollThumb;
            var inner = thumb;
            inner.Inflate(Vertical ? -1 : 0, Vertical ? 0 : -1);
            using var b = new SolidBrush(color);
            g.FillRectangle(b, inner);
        }
    }

    private void PaintArrow(Graphics g, Rectangle rect, Part part, bool backward)
    {
        if (rect.Width <= 0 || rect.Height <= 0) return;
        bool enabled = Enabled && IsScrollable;
        if (_pressed == part && enabled)
        {
            using var b = new SolidBrush(Theme.ScrollThumbPressed);
            g.FillRectangle(b, rect);
        }
        else if (_hot == part && enabled)
        {
            using var b = new SolidBrush(Theme.ScrollThumbHot);
            g.FillRectangle(b, rect);
        }

        var color = enabled ? Theme.ScrollArrow : Theme.ScrollThumbDisabled;
        int cx = rect.X + rect.Width / 2, cy = rect.Y + rect.Height / 2;
        const int s = 3;
        PointF[] pts = Vertical
            ? backward
                ? new[] { new PointF(cx - s, cy + s / 2f + 1), new PointF(cx, cy - s / 2f), new PointF(cx + s, cy + s / 2f + 1) }
                : new[] { new PointF(cx - s, cy - s / 2f), new PointF(cx, cy + s / 2f + 1), new PointF(cx + s, cy - s / 2f) }
            : backward
                ? new[] { new PointF(cx + s / 2f + 1, cy - s), new PointF(cx - s / 2f, cy), new PointF(cx + s / 2f + 1, cy + s) }
                : new[] { new PointF(cx - s / 2f, cy - s), new PointF(cx + s / 2f + 1, cy), new PointF(cx - s / 2f, cy + s) };
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using (var brush = new SolidBrush(color)) g.FillPolygon(brush, pts);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.Default;
    }
}

public enum ScrollEventType
{
    SmallDecrement = 0,
    SmallIncrement = 1,
    LargeDecrement = 2,
    LargeIncrement = 3,
    ThumbPosition = 4,
    ThumbTrack = 5,
    First = 6,
    Last = 7,
    EndScroll = 8,
}

public enum ScrollOrientation
{
    HorizontalScroll = 0,
    VerticalScroll = 1,
}

public delegate void ScrollEventHandler(object? sender, ScrollEventArgs e);

public class ScrollEventArgs : EventArgs
{
    public ScrollEventArgs(ScrollEventType type, int newValue) : this(type, -1, newValue, ScrollOrientation.HorizontalScroll) { }

    public ScrollEventArgs(ScrollEventType type, int newValue, ScrollOrientation scroll) : this(type, -1, newValue, scroll) { }

    public ScrollEventArgs(ScrollEventType type, int oldValue, int newValue) : this(type, oldValue, newValue, ScrollOrientation.HorizontalScroll) { }

    public ScrollEventArgs(ScrollEventType type, int oldValue, int newValue, ScrollOrientation scroll)
    {
        Type = type;
        OldValue = oldValue;
        NewValue = newValue;
        ScrollOrientation = scroll;
    }

    public ScrollEventType Type { get; }
    public int OldValue { get; }
    public int NewValue { get; set; }
    public ScrollOrientation ScrollOrientation { get; }
}
