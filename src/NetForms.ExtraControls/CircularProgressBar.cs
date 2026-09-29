using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NetForms.ExtraControls;

/// <summary>
/// A round progress indicator: an arc from twelve o'clock, as long as <see cref="Value"/> is far from
/// <see cref="Minimum"/> to <see cref="Maximum"/>, with the percentage (or the control's text) in the middle.
/// </summary>
[ToolboxBitmap(typeof(CircularProgressBar), "CircularProgressBar.png")]
[DefaultEvent(nameof(ValueChanged))]
[DefaultProperty(nameof(Value))]
[DefaultBindingProperty(nameof(Value))]
[Description("A round progress indicator.")]
public class CircularProgressBar : Control
{
    private int _minimum;
    private int _maximum = 100;
    private int _value;
    private int _lineWidth = 8;
    private Color _progressColor = Color.ForestGreen;
    private Color _trackColor = Color.Gainsboro;
    private bool _showPercentage = true;

    public CircularProgressBar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
        BackColor = Color.Transparent;
    }

    protected override Size DefaultSize => new(96, 96);

    /// <summary>Transparent by default: the control sits on whatever its parent paints (a GradientPanel, say).</summary>
    [DefaultValue(typeof(Color), "Transparent")]
    public override Color BackColor
    {
        get => base.BackColor;
        set => base.BackColor = value;
    }

    /// <summary>An indicator is not a tab stop (as ProgressBar).</summary>
    [DefaultValue(false)]
    public new bool TabStop
    {
        get => base.TabStop;
        set => base.TabStop = value;
    }

    [Category("Behavior")]
    [DefaultValue(0)]
    [Description("The lower bound of Value.")]
    public int Minimum
    {
        get => _minimum;
        set
        {
            _minimum = value;
            if (_maximum < value) _maximum = value;
            if (_value < value) _value = value;
            Invalidate();
        }
    }

    [Category("Behavior")]
    [DefaultValue(100)]
    [Description("The upper bound of Value.")]
    public int Maximum
    {
        get => _maximum;
        set
        {
            _maximum = value;
            if (_minimum > value) _minimum = value;
            if (_value > value) _value = value;
            Invalidate();
        }
    }

    /// <summary>The progress, from <see cref="Minimum"/> to <see cref="Maximum"/>.</summary>
    [Category("Behavior")]
    [DefaultValue(0)]
    [Bindable(true)]
    [Description("The current progress.")]
    public int Value
    {
        get => _value;
        set
        {
            if (value < _minimum || value > _maximum)
                throw new ArgumentOutOfRangeException(nameof(value), value, $"Value must be between Minimum ({_minimum}) and Maximum ({_maximum}).");
            if (_value == value) return;
            _value = value;
            Invalidate();
            OnValueChanged(EventArgs.Empty);
        }
    }

    [Category("Appearance")]
    [DefaultValue(8)]
    [Description("The thickness of the ring, in pixels.")]
    public int LineWidth
    {
        get => _lineWidth;
        set
        {
            if (value < 1) throw new ArgumentOutOfRangeException(nameof(value), value, "LineWidth must be at least 1.");
            _lineWidth = value;
            Invalidate();
        }
    }

    [Category("Appearance")]
    [DefaultValue(typeof(Color), "ForestGreen")]
    [Description("The colour of the done part of the ring.")]
    public Color ProgressColor
    {
        get => _progressColor;
        set { _progressColor = value; Invalidate(); }
    }

    [Category("Appearance")]
    [DefaultValue(typeof(Color), "Gainsboro")]
    [Description("The colour of the rest of the ring.")]
    public Color TrackColor
    {
        get => _trackColor;
        set { _trackColor = value; Invalidate(); }
    }

    /// <summary>Writes the percentage in the middle; with it off, the control's text is written instead.</summary>
    [Category("Appearance")]
    [DefaultValue(true)]
    [Description("Whether the percentage is written in the middle (otherwise the text).")]
    public bool ShowPercentage
    {
        get => _showPercentage;
        set { _showPercentage = value; Invalidate(); }
    }

    /// <summary>How far the progress is, 0 to 1.</summary>
    [Browsable(false)]
    public double Fraction => _maximum == _minimum ? 1 : (_value - _minimum) / (double)(_maximum - _minimum);

    [Category("Action")]
    [Description("Occurs when Value changes.")]
    public event EventHandler? ValueChanged;

    protected virtual void OnValueChanged(EventArgs e) => ValueChanged?.Invoke(this, e);

    /// <summary>Adds <paramref name="step"/> to the value, within the bounds (as ProgressBar.Increment).</summary>
    public void Increment(int step) => Value = Math.Clamp(_value + step, _minimum, _maximum);

    protected override void OnTextChanged(EventArgs e)
    {
        Invalidate();
        base.OnTextChanged(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int side = Math.Min(ClientSize.Width, ClientSize.Height) - _lineWidth - 2;
        if (side <= 0) return;
        var ring = new RectangleF((ClientSize.Width - side) / 2f, (ClientSize.Height - side) / 2f, side, side);
        using (var track = new Pen(_trackColor, _lineWidth))
        {
            g.DrawEllipse(track, ring);
        }
        float sweep = (float)(360 * Fraction);
        if (sweep > 0)
        {
            using var progress = new Pen(Enabled ? _progressColor : SystemColors.GrayText, _lineWidth) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawArc(progress, ring, -90, sweep);
        }
        string text = _showPercentage ? $"{Math.Round(Fraction * 100)}%" : Text;
        if (!string.IsNullOrEmpty(text))
        {
            TextRenderer.DrawText(g, text, Font, Rectangle.Round(ring), Enabled ? ForeColor : SystemColors.GrayText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }
}
