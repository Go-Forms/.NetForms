using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NetForms.ExtraControls;

/// <summary>
/// A row of stars for a rating: <see cref="Value"/> of <see cref="Maximum"/> are lit. A click on a star sets the
/// value (a click on the lit last star clears it); the mouse previews the value it would set; the arrow keys step it.
/// </summary>
[ToolboxBitmap(typeof(RatingStars), "RatingStars.png")]
[DefaultEvent(nameof(ValueChanged))]
[DefaultProperty(nameof(Value))]
[DefaultBindingProperty(nameof(Value))]
[Description("A row of stars for giving or showing a rating.")]
public class RatingStars : Control
{
    private int _value;
    private int _maximum = 5;
    private int _hover = -1;
    private Color _starColor = Color.Gold;
    private Color _emptyColor = Color.Gainsboro;
    private bool _readOnly;

    public RatingStars()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        TabStop = true;
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    protected override Size DefaultSize => new(120, 24);

    /// <summary>Transparent by default: the control sits on whatever its parent paints (a GradientPanel, say).</summary>
    [DefaultValue(typeof(Color), "Transparent")]
    public override Color BackColor
    {
        get => base.BackColor;
        set => base.BackColor = value;
    }

    /// <summary>How many stars are lit, 0 to <see cref="Maximum"/>.</summary>
    [Category("Behavior")]
    [DefaultValue(0)]
    [Bindable(true)]
    [Description("How many stars are lit.")]
    public int Value
    {
        get => _value;
        set
        {
            if (value < 0 || value > _maximum)
                throw new ArgumentOutOfRangeException(nameof(value), value, $"Value must be between 0 and Maximum ({_maximum}).");
            if (_value == value) return;
            _value = value;
            Invalidate();
            OnValueChanged(EventArgs.Empty);
        }
    }

    /// <summary>The number of stars.</summary>
    [Category("Behavior")]
    [DefaultValue(5)]
    [Description("The number of stars.")]
    public int Maximum
    {
        get => _maximum;
        set
        {
            if (value < 1 || value > 20) throw new ArgumentOutOfRangeException(nameof(value), value, "Maximum must be between 1 and 20.");
            _maximum = value;
            if (_value > value) Value = value;
            Invalidate();
        }
    }

    [Category("Appearance")]
    [DefaultValue(typeof(Color), "Gold")]
    [Description("The colour of a lit star.")]
    public Color StarColor
    {
        get => _starColor;
        set { _starColor = value; Invalidate(); }
    }

    [Category("Appearance")]
    [DefaultValue(typeof(Color), "Gainsboro")]
    [Description("The colour of an unlit star.")]
    public Color EmptyStarColor
    {
        get => _emptyColor;
        set { _emptyColor = value; Invalidate(); }
    }

    /// <summary>Shows the rating without letting the user change it.</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    [Description("Whether the user can change the rating.")]
    public bool ReadOnly
    {
        get => _readOnly;
        set { _readOnly = value; _hover = -1; Invalidate(); }
    }

    [Category("Action")]
    [Description("Occurs when Value changes.")]
    public event EventHandler? ValueChanged;

    protected virtual void OnValueChanged(EventArgs e) => ValueChanged?.Invoke(this, e);

    private float StarSize => Math.Min(ClientSize.Height - 2, (ClientSize.Width - 2) / (float)_maximum);

    /// <summary>The star under <paramref name="x"/> (1-based), or 0 left of the first.</summary>
    internal int StarAt(int x) => Math.Clamp((int)(x / Math.Max(1f, StarSize)) + 1, 1, _maximum);

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_readOnly) return;
        int star = StarAt(e.X);
        if (star != _hover)
        {
            _hover = star;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = -1;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (_readOnly || e.Button != MouseButtons.Left) return;
        Focus();
        int star = StarAt(e.X);
        Value = star == _value ? 0 : star;
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_readOnly) return;
        switch (e.KeyCode)
        {
            case Keys.Right or Keys.Up when _value < _maximum:
                Value++;
                e.Handled = true;
                break;
            case Keys.Left or Keys.Down when _value > 0:
                Value--;
                e.Handled = true;
                break;
            case Keys.Home:
                Value = 0;
                break;
            case Keys.End:
                Value = _maximum;
                break;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float size = StarSize;
        int lit = _hover > 0 ? _hover : _value;
        for (int i = 0; i < _maximum; i++)
        {
            var color = i < lit ? (_hover > 0 ? ControlPaint.Light(_starColor, 0.3f) : _starColor) : _emptyColor;
            if (!Enabled) color = ControlPaint.Light(Color.Gray, 0.8f);
            using var brush = new SolidBrush(color);
            using var pen = new Pen(ControlPaint.Dark(color, 0.1f));
            var star = Star(1 + i * size, 1, size);
            g.FillPolygon(brush, star);
            g.DrawPolygon(pen, star);
        }
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g, ClientRectangle);
    }

    private static PointF[] Star(float x, float y, float size)
    {
        var points = new PointF[10];
        float cx = x + size / 2, cy = y + size / 2 + size * 0.04f;
        for (int i = 0; i < 10; i++)
        {
            double r = (i % 2 == 0 ? 0.48 : 0.2) * size;
            double a = -Math.PI / 2 + i * Math.PI / 5;
            points[i] = new PointF(cx + (float)(r * Math.Cos(a)), cy + (float)(r * Math.Sin(a)));
        }
        return points;
    }
}
