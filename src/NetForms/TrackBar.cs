using System.ComponentModel;
using System;
using System.Drawing;

namespace System.Windows.Forms;

public enum Orientation
{
    Horizontal = 0,
    Vertical = 1,
}

public enum TickStyle
{
    None = 0,
    TopLeft = 1,
    BottomRight = 2,
    Both = 3,
}

[DefaultEvent("Scroll")]
[DefaultProperty("Value")]
public class TrackBar : Control, ISupportInitialize
{
    // Members WinForms hides from the designer on this control (Browsable(false)); checked against the real
    // WinForms by AttributeDiffTests.

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override Image? BackgroundImage
    {
        get => base.BackgroundImage;
        set => base.BackgroundImage = value;
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
    public new event EventHandler? ImeModeChanged
    {
        add => base.ImeModeChanged += value;
        remove => base.ImeModeChanged -= value;
    }

    private const int ThumbLength = 11;
    private const int ThumbThickness = 22;
    private const int TrackThickness = 4;
    private const int EdgePadding = 8;

    private int _minimum;
    private int _maximum = 10;
    private int _value;
    private int _smallChange = 1;
    private int _largeChange = 5;
    private int _tickFrequency = 1;
    private Orientation _orientation = Orientation.Horizontal;
    private TickStyle _tickStyle = TickStyle.BottomRight;
    private bool _dragging;
    private bool _hot;

    public TrackBar()
    {
        SetStyle(ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, false);
        SetStyle(ControlStyles.ResizeRedraw, true);
        base.AutoSize = true;
    }

    protected override Size DefaultSize => new Size(104, 45);

    [Category("Behavior")]
    [Description("Occurs when the TrackBar slider moves.")]
    public event EventHandler? Scroll;

    [Category("Action")]
    [Description("Occurs when the value of the control changes.")]
    public event EventHandler? ValueChanged;

    [Category("Behavior")]
    [Description("The minimum value for the position of the slider on the TrackBar.")]
    [DefaultValue(0)]
    public int Minimum
    {
        get => _minimum;
        set
        {
            if (_minimum == value) return;
            _minimum = value;
            if (_maximum < value) _maximum = value;
            _value = Math.Clamp(_value, _minimum, _maximum);
            Invalidate();
        }
    }

    [Category("Behavior")]
    [Description("The maximum value for the position of the slider on the TrackBar.")]
    [DefaultValue(10)]
    public int Maximum
    {
        get => _maximum;
        set
        {
            if (_maximum == value) return;
            _maximum = value;
            if (_minimum > value) _minimum = value;
            _value = Math.Clamp(_value, _minimum, _maximum);
            Invalidate();
        }
    }

    [Category("Behavior")]
    [Description("The position of the slider.")]
    [DefaultValue(0)]
    public int Value
    {
        get => _value;
        set
        {
            if (value < _minimum || value > _maximum) throw new ArgumentOutOfRangeException(nameof(value), "Value must be between Minimum and Maximum.");
            if (_value == value) return;
            _value = value;
            Invalidate();
            OnValueChanged(EventArgs.Empty);
        }
    }

    [Category("Behavior")]
    [Description("The number of positions the slider moves in response to keyboard input (arrow keys).")]
    [DefaultValue(1)]
    public int SmallChange
    {
        get => _smallChange;
        set => _smallChange = Math.Max(0, value);
    }

    [Category("Behavior")]
    [Description("The number of positions the slider moves in response to mouse clicks or the PAGE UP and PAGE DOWN keys.")]
    [DefaultValue(5)]
    public int LargeChange
    {
        get => _largeChange;
        set => _largeChange = Math.Max(0, value);
    }

    [Category("Appearance")]
    [Description("The number of positions between tick marks.")]
    [DefaultValue(1)]
    public int TickFrequency
    {
        get => _tickFrequency;
        set
        {
            _tickFrequency = Math.Max(1, value);
            Invalidate();
        }
    }

    [Category("Appearance")]
    [Description("The orientation of the control.")]
    [DefaultValue(Orientation.Horizontal)]
    [Localizable(true)]
    public Orientation Orientation
    {
        get => _orientation;
        set
        {
            if (_orientation == value) return;
            _orientation = value;
            // WinForms swaps the size when the orientation flips on a created control - on a design
            // surface too, where controls have handles. Before that (new TrackBar { Orientation = Vertical })
            // the size stays 104x45 and the designer's written Size sets it.
            if (IsHandleCreated || DesignMode) SetBounds(Left, Top, Height, Width, BoundsSpecified.Size);
            Invalidate();
        }
    }

    [Category("Appearance")]
    [Description("Indicates where the ticks appear on the TrackBar.")]
    [DefaultValue(TickStyle.BottomRight)]
    public TickStyle TickStyle
    {
        get => _tickStyle;
        set
        {
            _tickStyle = value;
            Invalidate();
        }
    }

    [Category("Behavior")]
    [Description("Indicates whether the control will resize itself automatically based on a computation of the default scroll bar dimensions.")]
    [DefaultValue(true)]
    [Localizable(true)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override bool AutoSize
    {
        get => base.AutoSize;
        set => base.AutoSize = value;
    }

    public void SetRange(int minValue, int maxValue)
    {
        _minimum = minValue;
        _maximum = Math.Max(minValue, maxValue);
        _value = Math.Clamp(_value, _minimum, _maximum);
        Invalidate();
    }

    public void BeginInit() { }

    public void EndInit() { }

    protected virtual void OnScroll(EventArgs e) => Scroll?.Invoke(this, e);
    protected virtual void OnValueChanged(EventArgs e) => ValueChanged?.Invoke(this, e);

    private bool Horizontal => _orientation == Orientation.Horizontal;

    private int TrackLength => Math.Max(1, (Horizontal ? Width : Height) - 2 * EdgePadding - ThumbLength);

    private int TrackStart => EdgePadding + ThumbLength / 2;

    private int ValueToPosition(int value)
    {
        int range = _maximum - _minimum;
        if (range <= 0) return TrackStart;
        int pos = TrackStart + (int)Math.Round((double)TrackLength * (value - _minimum) / range);
        return Horizontal ? pos : (Height - pos);
    }

    private int PositionToValue(int pos)
    {
        if (!Horizontal) pos = Height - pos;
        int range = _maximum - _minimum;
        double t = Math.Clamp((pos - TrackStart) / (double)TrackLength, 0, 1);
        return _minimum + (int)Math.Round(t * range);
    }

    private int CrossCenter
    {
        get
        {
            // The track sits in the middle of the cross axis, shifted away from the tick side.
            int extent = Horizontal ? Height : Width;
            int center = extent / 2;
            if (_tickStyle == TickStyle.BottomRight) center -= 4;
            else if (_tickStyle == TickStyle.TopLeft) center += 4;
            return center;
        }
    }

    private Rectangle ThumbRectangle
    {
        get
        {
            int pos = ValueToPosition(_value);
            int c = CrossCenter;
            return Horizontal
                ? new Rectangle(pos - ThumbLength / 2, c - ThumbThickness / 2, ThumbLength, ThumbThickness)
                : new Rectangle(c - ThumbThickness / 2, pos - ThumbLength / 2, ThumbThickness, ThumbLength);
        }
    }

    private void SetValueFromUser(int value)
    {
        value = Math.Clamp(value, _minimum, _maximum);
        if (value == _value) return;
        _value = value;
        Invalidate();
        OnScroll(EventArgs.Empty);
        OnValueChanged(EventArgs.Empty);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            if (CanFocus) Focus();
            if (ThumbRectangle.Contains(e.Location))
            {
                _dragging = true;
            }
            else
            {
                int pos = Horizontal ? e.X : e.Y;
                int thumbPos = ValueToPosition(_value);
                bool forward = Horizontal ? pos > thumbPos : pos < thumbPos;
                SetValueFromUser(_value + (forward ? _largeChange : -_largeChange));
            }
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_dragging && (e.Button & MouseButtons.Left) != 0)
        {
            SetValueFromUser(PositionToValue(Horizontal ? e.X : e.Y));
        }
        bool hot = ThumbRectangle.Contains(e.Location);
        if (hot != _hot)
        {
            _hot = hot;
            Invalidate();
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _dragging = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hot = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        int notches = e.Delta / 120;
        if (notches == 0) notches = Math.Sign(e.Delta);
        SetValueFromUser(_value + notches * _smallChange);
        base.OnMouseWheel(e);
    }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Left or Keys.Down: SetValueFromUser(_value - _smallChange); e.Handled = true; break;
            case Keys.Right or Keys.Up: SetValueFromUser(_value + _smallChange); e.Handled = true; break;
            case Keys.PageDown: SetValueFromUser(_value - _largeChange); e.Handled = true; break;
            case Keys.PageUp: SetValueFromUser(_value + _largeChange); e.Handled = true; break;
            case Keys.Home: SetValueFromUser(_minimum); e.Handled = true; break;
            case Keys.End: SetValueFromUser(_maximum); e.Handled = true; break;
        }
        base.OnKeyDown(e);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        Invalidate();
        base.OnGotFocus(e);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        Invalidate();
        base.OnLostFocus(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        bool enabled = Enabled;
        int c = CrossCenter;
        int start = TrackStart, end = start + TrackLength;

        // Track.
        var track = Horizontal
            ? new Rectangle(start, c - TrackThickness / 2, TrackLength, TrackThickness)
            : new Rectangle(c - TrackThickness / 2, Height - end, TrackThickness, TrackLength);
        using (var b = new SolidBrush(Theme.ProgressTrack)) g.FillRectangle(b, track);
        using (var p = new Pen(Theme.ProgressTrackBorder)) g.DrawRectangle(p, track.X, track.Y, track.Width - 1, track.Height - 1);

        // Ticks.
        if (_tickStyle != TickStyle.None && _maximum > _minimum)
        {
            using var tick = new Pen(enabled ? Theme.ScrollArrow : Theme.ScrollThumbDisabled);
            for (int v = _minimum; v <= _maximum; v += _tickFrequency)
            {
                DrawTick(g, tick, v, c);
            }
            if ((_maximum - _minimum) % _tickFrequency != 0) DrawTick(g, tick, _maximum, c);
        }

        // Thumb.
        var thumb = ThumbRectangle;
        var face = !enabled ? Theme.ButtonFaceDisabled : _dragging ? Theme.ButtonBorderPressed : _hot ? Theme.ButtonBorderHot : Theme.Accent;
        using (var b = new SolidBrush(face)) g.FillRectangle(b, thumb);

        if (Focused && ShowFocusCues)
        {
            ControlPaint.DrawFocusRectangle(g, ClientRectangle, ForeColor, BackColor);
        }
        base.OnPaint(e);
    }

    private void DrawTick(Graphics g, Pen pen, int value, int center)
    {
        int pos = ValueToPosition(value);
        int offset = ThumbThickness / 2 + 3;
        bool topLeft = _tickStyle is TickStyle.TopLeft or TickStyle.Both;
        bool bottomRight = _tickStyle is TickStyle.BottomRight or TickStyle.Both;
        if (Horizontal)
        {
            if (topLeft) g.DrawLine(pen, pos, center - offset - 3, pos, center - offset);
            if (bottomRight) g.DrawLine(pen, pos, center + offset, pos, center + offset + 3);
        }
        else
        {
            if (topLeft) g.DrawLine(pen, center - offset - 3, pos, center - offset, pos);
            if (bottomRight) g.DrawLine(pen, center + offset, pos, center + offset + 3, pos);
        }
    }

    public override string ToString() => base.ToString() + $", Minimum: {_minimum}, Maximum: {_maximum}, Value: {_value}";

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Category("Appearance")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override Font Font { get => base.Font; set => base.Font = value; }

    [Category("Appearance")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override Color ForeColor { get => base.ForeColor; set => base.ForeColor = value; }

    [Category("Behavior")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new ImeMode ImeMode { get => base.ImeMode; set => base.ImeMode = value; }

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

    [Category("Property Changed")]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? AutoSizeChanged
    {
        add => base.AutoSizeChanged += value;
        remove => base.AutoSizeChanged -= value;
    }

    [Category("Action")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? Click
    {
        add => base.Click += value;
        remove => base.Click -= value;
    }

    [Category("Action")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? DoubleClick
    {
        add => base.DoubleClick += value;
        remove => base.DoubleClick -= value;
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

    [Category("Property Changed")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? ForeColorChanged
    {
        add => base.ForeColorChanged += value;
        remove => base.ForeColorChanged -= value;
    }

    [Category("Action")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event MouseEventHandler? MouseClick
    {
        add => base.MouseClick += value;
        remove => base.MouseClick -= value;
    }

    [Category("Action")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event MouseEventHandler? MouseDoubleClick
    {
        add => base.MouseDoubleClick += value;
        remove => base.MouseDoubleClick -= value;
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

    [Category("Appearance")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event PaintEventHandler? Paint
    {
        add => base.Paint += value;
        remove => base.Paint -= value;
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
