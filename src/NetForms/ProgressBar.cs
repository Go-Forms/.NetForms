using System.ComponentModel;
using System;
using System.Drawing;

namespace System.Windows.Forms;

public enum ProgressBarStyle
{
    Blocks = 0,
    Continuous = 1,
    Marquee = 2,
}

[DefaultProperty("Value")]
public class ProgressBar : Control
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
    public new event EventHandler? CausesValidationChanged
    {
        add => base.CausesValidationChanged += value;
        remove => base.CausesValidationChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? ImeModeChanged
    {
        add => base.ImeModeChanged += value;
        remove => base.ImeModeChanged -= value;
    }

    private int _minimum;
    private int _maximum = 100;
    private int _value;
    private int _step = 10;
    private ProgressBarStyle _style = ProgressBarStyle.Blocks;
    private int _marqueeSpeed = 100;
    private int _marqueePosition;
    private Timer? _marqueeTimer;

    public ProgressBar()
    {
        // Not selectable, but TabStop stays true, as in WinForms: the flag is only consulted for
        // controls that can take focus in the first place.
        SetStyle(ControlStyles.Selectable, false);
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    protected override Size DefaultSize => new Size(100, 23);

    [Category("Behavior")]
    [Description("The lower bound of the range this ProgressBar is working with.")]
    [DefaultValue(0)]
    public int Minimum
    {
        get => _minimum;
        set
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            if (_minimum == value) return;
            _minimum = value;
            if (_maximum < value) _maximum = value;
            if (_value < value) _value = value;
            Invalidate();
        }
    }

    [Category("Behavior")]
    [Description("The upper bound of the range this ProgressBar is working with.")]
    [DefaultValue(100)]
    public int Maximum
    {
        get => _maximum;
        set
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            if (_maximum == value) return;
            _maximum = value;
            if (_minimum > value) _minimum = value;
            if (_value > value) _value = value;
            Invalidate();
        }
    }

    [Category("Behavior")]
    [Description("The current value for the ProgressBar, in the range specified by the minimum and maximum properties.")]
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
        }
    }

    [Category("Behavior")]
    [Description("The amount to increment the current value of the control by when the PerformStep() method is called.")]
    [DefaultValue(10)]
    public int Step
    {
        get => _step;
        set => _step = value;
    }

    [Category("Behavior")]
    [Description("This property allows the user to set the style of the ProgressBar.")]
    [DefaultValue(ProgressBarStyle.Blocks)]
    public ProgressBarStyle Style
    {
        get => _style;
        set
        {
            if (_style == value) return;
            _style = value;
            UpdateMarquee();
            Invalidate();
        }
    }

    [Category("Behavior")]
    [Description("The speed of the marquee animation in milliseconds.")]
    [DefaultValue(100)]
    public int MarqueeAnimationSpeed
    {
        get => _marqueeSpeed;
        set
        {
            _marqueeSpeed = Math.Max(0, value);
            UpdateMarquee();
        }
    }

    public void Increment(int value)
    {
        int v = Math.Clamp(_value + value, _minimum, _maximum);
        if (v != _value)
        {
            _value = v;
            Invalidate();
        }
    }

    public void PerformStep() => Increment(_step);

    private void UpdateMarquee()
    {
        bool run = _style == ProgressBarStyle.Marquee && _marqueeSpeed > 0 && IsHandleCreated;
        if (run)
        {
            _marqueeTimer ??= new Timer();
            _marqueeTimer.Interval = _marqueeSpeed;
            _marqueeTimer.Tick -= MarqueeTick;
            _marqueeTimer.Tick += MarqueeTick;
            _marqueeTimer.Start();
        }
        else
        {
            _marqueeTimer?.Stop();
        }
    }

    private void MarqueeTick(object? sender, EventArgs e)
    {
        _marqueePosition = (_marqueePosition + 8) % Math.Max(1, Width + Width / 3);
        Invalidate();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        UpdateMarquee();
    }

    private ProgressBarBarState _barState;

    /// <summary>The native bar state (PBM_SETSTATE): green, red or yellow. Not public in WinForms; TaskDialog uses it.</summary>
    internal ProgressBarBarState BarState
    {
        get => _barState;
        set
        {
            if (_barState == value) return;
            _barState = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var rect = ClientRectangle;
        if (rect.Width <= 0 || rect.Height <= 0) return;

        using (var track = new SolidBrush(Theme.ProgressTrack)) g.FillRectangle(track, rect);
        using (var border = new Pen(Theme.ProgressTrackBorder)) g.DrawRectangle(border, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);

        var inner = new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2);
        using var fill = new SolidBrush(!Enabled ? Theme.ButtonBorderDisabled : BarState switch
        {
            ProgressBarBarState.Error => Theme.ProgressFillError,
            ProgressBarBarState.Paused => Theme.ProgressFillPaused,
            _ => Theme.ProgressFill,
        });
        if (_style == ProgressBarStyle.Marquee)
        {
            int chunk = Math.Max(10, inner.Width / 3);
            int x = inner.X + _marqueePosition - chunk;
            var chunkRect = Rectangle.Intersect(inner, new Rectangle(x, inner.Y, chunk, inner.Height));
            if (chunkRect.Width > 0) g.FillRectangle(fill, chunkRect);
        }
        else
        {
            int range = _maximum - _minimum;
            int width = range <= 0 ? inner.Width : (int)((long)inner.Width * (_value - _minimum) / range);
            if (width > 0) g.FillRectangle(fill, new Rectangle(inner.X, inner.Y, width, inner.Height));
        }
        base.OnPaint(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _marqueeTimer?.Dispose();
        base.Dispose(disposing);
    }

    public override string ToString() => base.ToString() + $", Minimum: {_minimum}, Maximum: {_maximum}, Value: {_value}";

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Category("Behavior")]
    [DefaultValue(false)]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new bool AllowDrop { get => base.AllowDrop; set => base.AllowDrop = value; }

    [Category("Focus")]
    [DefaultValue(true)]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new bool CausesValidation { get => base.CausesValidation; set => base.CausesValidation = value; }

    [Category("Appearance")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override Font Font { get => base.Font; set => base.Font = value; }

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

    [Category("Behavior")]
    [DefaultValue(true)]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new bool TabStop { get => base.TabStop; set => base.TabStop = value; }

    [Category("Appearance")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override string Text { get => base.Text; set => base.Text = value; }

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
}

/// <summary>PBST_NORMAL / PBST_ERROR / PBST_PAUSED.</summary>
internal enum ProgressBarBarState
{
    Normal,
    Error,
    Paused,
}
