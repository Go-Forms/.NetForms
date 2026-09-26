using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace ControlLibrary;

/// <summary>A control that paints itself: a bar filled to <see cref="Value"/> percent.</summary>
[ToolboxBitmap(typeof(Gauge), "Gauge.bmp")]
[DefaultEvent(nameof(ValueChanged))]
public class Gauge : Control
{
    private int _value;

    public Gauge()
    {
        Size = new Size(120, 24);
    }

    [Category("Behavior")]
    [DefaultValue(0)]
    [Description("How full the gauge is, 0 to 100.")]
    public int Value
    {
        get => _value;
        set
        {
            if (value < 0 || value > 100) throw new System.ArgumentOutOfRangeException(nameof(value));
            if (_value == value) return;
            _value = value;
            Invalidate();
            ValueChanged?.Invoke(this, System.EventArgs.Empty);
        }
    }

    [Category("Appearance")]
    [DefaultValue(typeof(Color), "Green")]
    public Color BarColor { get; set; } = Color.Green;

    public event System.EventHandler? ValueChanged;

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var brush = new SolidBrush(BarColor);
        e.Graphics.FillRectangle(brush, 0, 0, Width * _value / 100, Height);
        e.Graphics.DrawRectangle(Pens.Black, 0, 0, Width - 1, Height - 1);
    }
}
